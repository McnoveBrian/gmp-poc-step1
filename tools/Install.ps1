<#
.SYNOPSIS
    Installs (or removes) the GMP Production Platform Revit add-in for every Revit year it
    supports that is installed on this machine.

.DESCRIPTION
    Run from the unzipped package produced by tools\Package.ps1. The package holds one folder
    per Revit year (2024, 2025, 2026, ...), each with GmpPlatform.Revit.addin and a
    GmpPlatform.Revit\ folder. For each year that has both a package folder and an installed
    Revit, the add-in is copied to %APPDATA%\Autodesk\Revit\Addins\<year>\ (or the all-users
    %ProgramData% location with -AllUsers).

    Only this add-in's own files are touched: GmpPlatform.Revit.addin and the GmpPlatform.Revit
    folder. Other add-ins in the same folder are left alone.

.PARAMETER Years
    Install only these Revit years, e.g. -Years 2025,2026. Default: every packaged year whose
    Revit is installed.

.PARAMETER Force
    Install for the requested years even when that Revit is not detected (e.g. it will be
    installed later).

.PARAMETER AllUsers
    Install to %ProgramData%\Autodesk\Revit\Addins\<year> for every user. Needs an elevated
    (Run as administrator) PowerShell.

.PARAMETER Uninstall
    Remove the add-in from the selected years instead of installing it.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install.ps1 -Years 2026 -Force
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [int[]]  $Years,
    [switch] $Force,
    [switch] $AllUsers,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$AddinFile   = 'GmpPlatform.Revit.addin'
$AddinFolder = 'GmpPlatform.Revit'
$AcadReleases = @(2025, 2026)   # keep in step with AcadRunner.SupportedReleases

function Get-RevitInstalled([int] $year) {
    $exe = Join-Path $env:ProgramFiles "Autodesk\Revit $year\Revit.exe"
    return (Test-Path $exe)
}

function Get-AddinsRoot([int] $year) {
    if ($AllUsers) { return Join-Path $env:ProgramData "Autodesk\Revit\Addins\$year" }
    return Join-Path $env:APPDATA "Autodesk\Revit\Addins\$year"
}

# ---- what is packaged ---------------------------------------------------------------------
$packaged = @(Get-ChildItem -Path $PSScriptRoot -Directory |
    Where-Object { $_.Name -match '^\d{4}$' -and (Test-Path (Join-Path $_.FullName $AddinFile)) } |
    ForEach-Object { [int]$_.Name } | Sort-Object)

if (-not $Uninstall -and $packaged.Count -eq 0) {
    throw "No packaged Revit years found next to this script ($PSScriptRoot). Run it from the unzipped package."
}

# ---- which years to act on ----------------------------------------------------------------
if ($Years) {
    $targets = @($Years | Sort-Object -Unique)
} elseif ($Uninstall) {
    $targets = @(2024..2030 | Where-Object { Test-Path (Join-Path (Get-AddinsRoot $_) $AddinFile) })
} else {
    $targets = @($packaged | Where-Object { Get-RevitInstalled $_ })
}

if ($targets.Count -eq 0) {
    if ($Uninstall) { Write-Host 'The add-in is not installed for any Revit year. Nothing to do.'; return }
    $found = @(2020..2030 | Where-Object { Get-RevitInstalled $_ })
    $foundText = if ($found.Count) { $found -join ', ' } else { 'none' }
    throw ("No installed Revit matches a packaged year. Packaged: $($packaged -join ', '). " +
           "Installed Revit: $foundText. Use -Years <year> -Force to install anyway.")
}

# ---- Revit must be closed: it locks the add-in DLLs ----------------------------------------
$running = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw 'Revit is running. Close every Revit window, then run this again.'
}

if ($AllUsers) {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) { throw '-AllUsers needs an elevated PowerShell (Run as administrator).' }
}

# ---- act ----------------------------------------------------------------------------------
$done = @()
foreach ($year in $targets) {
    $root = Get-AddinsRoot $year
    $manifest = Join-Path $root $AddinFile
    $folder = Join-Path $root $AddinFolder

    if ($Uninstall) {
        if (Test-Path $manifest) { Remove-Item $manifest -Force }
        if (Test-Path $folder)   { Remove-Item $folder -Recurse -Force }
        Write-Host "Revit ${year}: removed from $root"
        $done += $year
        continue
    }

    if ($packaged -notcontains $year) {
        Write-Warning "Revit ${year}: this package has no build for it (packaged: $($packaged -join ', ')). Skipped."
        continue
    }
    if (-not (Get-RevitInstalled $year) -and -not $Force) {
        Write-Warning "Revit ${year}: not installed here. Skipped (add -Force to install anyway)."
        continue
    }

    $source = Join-Path $PSScriptRoot $year
    New-Item -ItemType Directory -Path $root -Force | Out-Null

    # Replace the add-in folder wholesale so files dropped from a newer build do not linger.
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
    Copy-Item (Join-Path $source $AddinFolder) $folder -Recurse -Force
    Copy-Item (Join-Path $source $AddinFile) $manifest -Force

    $engine = Join-Path $folder 'GmpPlatform.Acad.dll'
    if (-not (Test-Path $engine)) { throw "Revit ${year}: the AutoCAD drawing engine is missing from the package ($engine)." }

    Write-Host "Revit ${year}: installed to $root"
    $done += $year
}

if ($done.Count -eq 0) { throw 'Nothing was installed or removed.' }
if ($Uninstall) { return }

# ---- the AutoCAD half ---------------------------------------------------------------------
$acad = @($AcadReleases | Where-Object { Test-Path (Join-Path $env:ProgramFiles "Autodesk\AutoCAD $_\accoreconsole.exe") })
if ($acad.Count -gt 0) {
    Write-Host "AutoCAD found for the drawing engine: $($acad -join ', ')"
} else {
    Write-Warning ("No supported AutoCAD ($($AcadReleases -join ' or ')) was found. Ticket Drawing needs one; " +
                   'set AutoCadConsolePath in the add-in settings if it is installed somewhere unusual.')
}

# Revit ties "Always Load" for an unsigned add-in to the exact DLL, so it asks again after
# every update (verified on Revit 2025). Code-signing the add-in is what removes the prompt.
Write-Host 'On the next Revit launch, answer the "Unsigned Add-In" prompt with "Always Load", then close'
Write-Host 'Revit normally once so the choice is kept. Revit asks again after each update of the add-in.'
