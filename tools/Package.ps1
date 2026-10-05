<#
.SYNOPSIS
    Builds the add-in for every supported Revit year and zips one installable package.

.DESCRIPTION
    For each year in -Years: builds GmpPlatform.sln in Release.R<yy> (without deploying to this
    machine), then lays the output out as

        dist\GmpPlatform-<version>\
            Install.ps1
            2024\GmpPlatform.Revit.addin
            2024\GmpPlatform.Revit\*.dll      (add-in, Core, AutoCAD engine, dependencies)
            2025\...
            2026\...

    and zips it to dist\GmpPlatform-<version>.zip. The core tests run first unless -SkipTests.
    A build that fails, or output missing the add-in or the AutoCAD engine, stops the script.

.PARAMETER Years
    Revit years to package. Default 2024, 2025, 2026. 2027 needs the .NET 10 SDK and the
    Release.R27 configuration (see RUNBOOK, "Annual Revit upgrade").

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\Package.ps1
#>
[CmdletBinding()]
param(
    [int[]]  $Years = @(2024, 2025, 2026),
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repo 'GmpPlatform.sln'
$addinProject = Join-Path $repo 'source\GmpPlatform.Revit'

[xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No <Version> in Directory.Build.props.' }

function Invoke-Dotnet([string[]] $arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($arguments -join ' ') failed (exit $LASTEXITCODE)." }
}

if (-not $SkipTests) {
    Write-Host '== Tests'
    Invoke-Dotnet @('test', (Join-Path $repo 'tests\GmpPlatform.Core.Tests\GmpPlatform.Core.Tests.csproj'),
                    '-c', 'Release', '-nologo', '-v:quiet')
}

$name = "GmpPlatform-$version"
$dist = Join-Path $repo 'dist'
$stage = Join-Path $dist $name
$zip = Join-Path $dist "$name.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

foreach ($year in $Years) {
    $config = 'Release.R' + ($year % 100).ToString('00')
    Write-Host "== Revit $year ($config)"
    Invoke-Dotnet @('build', $solution, '-c', $config, '-p:DeployAddin=false', '-nologo', '-v:minimal')

    $bin = Join-Path $addinProject "bin\$config"
    foreach ($required in 'GmpPlatform.Revit.dll', 'GmpPlatform.Core.dll', 'GmpPlatform.Acad.dll') {
        if (-not (Test-Path (Join-Path $bin $required))) { throw "Revit ${year}: $required is missing from $bin." }
    }

    $target = Join-Path $stage "$year\GmpPlatform.Revit"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem $bin -File | Where-Object { $_.Extension -ne '.pdb' } |
        Copy-Item -Destination $target
    Copy-Item (Join-Path $addinProject 'GmpPlatform.Revit.addin') (Join-Path $stage $year)
}

Copy-Item (Join-Path $PSScriptRoot 'Install.ps1') $stage

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host ''
Write-Host "Package: $zip"
Get-ChildItem $stage -Directory | ForEach-Object {
    $count = @(Get-ChildItem (Join-Path $_.FullName 'GmpPlatform.Revit') -File).Count
    Write-Host ("  Revit {0}: {1} files" -f $_.Name, $count)
}
