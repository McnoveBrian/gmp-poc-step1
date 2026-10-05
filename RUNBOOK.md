# Runbook — build, install, run, test

Everything needed to take this from a clone to a plotted shop ticket, and to check it still
works. Verified on this machine on 2026-10-06 (Revit 2024 and 2025, AutoCAD 2025).

---

## 0. Prerequisites

| Need | Why | Check |
|---|---|---|
| **.NET 8 SDK** | builds everything | `dotnet --version` → 8.x |
| **Revit 2024, 2025 or 2026** | the add-in is built for each (§1) | `C:\Program Files\Autodesk\Revit 20xx\Revit.exe` |
| **AutoCAD 2025 or 2026** | the drawing engine runs in `accoreconsole` | `C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe` |

AutoCAD does **not** need to be installed to *build* — the engine compiles against Autodesk's
`AutoCAD.NET` reference package. It is needed to *run* a ticket.

**Revit 2027 and AutoCAD 2027 may also be installed.** Never open the client RVT with Revit
2027 — it upgrades the file irreversibly and there is no way back. Always Revit 2025, always on
a copy of the model, never the original under `samples/`.

---

## 1. Build

One codebase, one build configuration per Revit year:

| Configuration | Revit | Runtime | Notes |
|---|---|---|---|
| `Release.R24` / `Debug.R24` | 2024 | .NET Framework 4.8 | polyfills in `Compatibility/` |
| `Release.R25` / `Debug.R25` | 2025 | .NET 8 | the client's current release |
| `Release.R26` / `Debug.R26` | 2026 | .NET 8 | compiles; not run here (Revit 2026 not installed) |

```bat
dotnet build GmpPlatform.sln -c Release.R25
```

One command does everything: it builds the AutoCAD drawing engine first, the Revit add-in
second, copies the engine next to the add-in, and **deploys the add-in to that Revit year**
(`%APPDATA%\Autodesk\Revit\Addins\2025\`). For a year that is not installed, or while Revit is
open, add `-p:DeployAddin=false`.

Each year compiles against its own Revit API: the Nice3point and `Nice3point.Revit.Api.*`
packages are pinned per year in `GmpPlatform.Revit.csproj`. Keep it that way — a shared pin
once made the R24 build compile against the 2025 API.

Confirm the engine actually shipped — this is the piece that used to go missing silently:

```bat
dir "%APPDATA%\Autodesk\Revit\Addins\2025\GmpPlatform.Revit\GmpPlatform.Acad.dll"
```

If that file is absent the add-in will load but report *"Drawing engine missing"* when you
press the button.

### Install package for another machine

```bat
powershell -ExecutionPolicy Bypass -File tools\Package.ps1
```

Runs the tests, builds every year and writes `dist\GmpPlatform-<version>.zip`: one folder per
Revit year plus `Install.ps1`. On the target machine, unzip and run

```bat
powershell -ExecutionPolicy Bypass -File Install.ps1
```

It installs into every packaged Revit year that is installed there, leaves other add-ins alone,
and warns if no supported AutoCAD is found. `-Years 2026 -Force` installs for a year not yet
installed; `-Uninstall` removes it; `-AllUsers` (elevated) installs for every user.

---

## 2. Tests

```bat
dotnet test tests\GmpPlatform.Core.Tests\GmpPlatform.Core.Tests.csproj
```

77 tests, about a second. They cover the hand-off contract, the unit rounding, the client's
drafting conventions — the station direction, the piece placement, the callout offset — which
layout gets which sheet, and how a standards profile is read. If one
of the `SheetConventions` tests fails, the sheet is about to come out mirrored or offset;
that is what they are there to stop.

---

## 3. Settings (once per machine)

`%ProgramData%\GMP\Platform\config\settings.json` — created on first run, then edit these:

```json
{
  "SeedDwgPath":   "C:\\...\\45430_AFIP1035.dwg",
  "FontMapPath":   "C:\\...\\gmp-poc-step1\\assets\\gmp.fmp",
  "PlotAfterDrawing": true
}
```

- **`SeedDwgPath`** is the only one that must be right. The engine opens a **copy** of this
  drawing and draws into it, which is how the output inherits GMP's layers, styles, blocks,
  title blocks and layouts. Point it at the client's approved DWG.
- **`FontMapPath`** substitutes the client's SHX fonts, which we do not have. See
  [`assets/README.md`](assets/README.md) — without it their text does not render at all.
- `AutoCadConsolePath` may be left out; the runner finds a supported AutoCAD itself (2025
  first, then 2026 — `AcadRunner.SupportedReleases`) and never prefers a newer, unsupported
  2027 install. If only an unsupported release is present it still runs and says so.
- `ShowDeveloperTools` (default `false`) adds a *Developer* panel with the retired *Wall
  Ticket* route, *Export DWG* and *Verify*. **Leave it off on the demo machine**: *Verify*
  generates placeholder Revit sheets, and clicking it in front of a client puts them on screen.
- `TifLayoutName`, `BifLayoutName`, `SectionsLayoutName` (optional) name the seed layouts that
  carry each sheet. Left out, the layouts are taken in **tab order**: the first two layouts with
  a main viewport are TIF and BIF, the next layout with viewports is the sections sheet. Set
  them when a seed's tabs are in a different order; a name that is missing or unusable is
  reported in the draw log (`sheet roles: ...`) and tab order fills the gap.
- `DrawGeneratedRebar` (default `false`): reinforcement is the drafter's job, as agreed in the
  meeting. `true` brings back the rule-generated bars (marked `*` and listed in the derived
  note) for pieces whose model carries no RSX bars.
- `StandardsProfilePath` (optional) points at a drafting-standards profile; see below.

The file is read case-insensitively, so `seedDwgPath` also works.

### Drafting standards profile (`standards.json`)

Every layer, style, block, attribute tag and fixed label the engine writes comes from a
standards profile. With no file, the built-in profile is the approved Tindall / AFIP-1035
seed, and the output is exactly what it has always been. A different seed, plant or client
standard is a JSON file, not a code change:

- Where: `StandardsProfilePath` in `settings.json`, else
  `%ProgramData%\GMP\Platform\config\standards.json`, else built-in.
- List only what differs; everything left out keeps the Tindall default. Comments and trailing
  commas are allowed.
- The draw log says which profile was used: `standards: <name> (<path>)`. A file that is
  missing or does not parse falls back to the built-in profile with a warning — it never stops
  the drawing.

```jsonc
{
  "name": "Plant B standard",
  "layers":     { "text": "A-TEXT", "dimensions": "A-DIMS" },
  "styles":     { "dimension": "PlantB-4", "insulationHatchPattern": "ANSI31" },
  "blocks":     { "bomRow": "PB_BOM", "titleBlockPrefix": "PB_TITLE" },
  "titleBlock": { "jobNumber": "JOBNO" },
  "labels":     { "solidZone": "SOLID" }
}
```

Sections: `layers` (panel, dimensions, text, insert, insulation, insulationHatch, tags,
derived), `styles` (dimension, text, insulationHatchPattern, insulationHatchScale), `blocks`
(sectionMarker, sectionMarkerLetterTag, rebar, titleBlockPrefix, bomRow, bomPartNumberTag,
bomQuantityTag), `titleBlock` (one attribute tag per ticket field) and `labels` (the fixed
wording on the sheets). The full default file, as a starting point, is what
`StandardsStore.DefaultsJson()` returns. Names should exist in the seed: a missing layer is
created; a missing dimension or text style falls back to the drawing's current style with a
warning; a missing section-marker, title-block or BOM block is reported in the draw log and
that content is left out.

---

## 4. First Revit launch — clear the security prompt once

The add-in is unsigned, so Revit asks about it. Do this **before** the demo, not during, and
**again after every rebuild or update**:

1. Start Revit (each year separately).
2. On **"Security — Unsigned Add-In"**, choose **Always Load**.
3. Close Revit normally.

Revit keeps "Always Load" for that **exact DLL**: a rebuilt add-in whose bytes differ is asked
about again, even though `HKCU\Software\Autodesk\Revit\Autodesk Revit <year>\CodeSigning` still
lists `c7a91e2b-4f58-4d0a-9e33-8b1c6d2a7f10 = 1` (verified on Revit 2024 and 2025, 2026-10-06).
Writing that registry value yourself therefore does not suppress the prompt. A rebuild with no
source change is byte-identical (deterministic build) and is not asked about.

The permanent fix is to **code-sign** `GmpPlatform.Revit.dll`; Revit then trusts the publisher
and updates load without a prompt.

If the prompt returns on every launch for the *same* build, Revit is not shutting down cleanly —
check that no other unsigned add-in (the old `GmpRevitSurvey` analysis utility, if it is still
registered) is crashing it on exit.

---

## 5. Run it

1. Open the model **copy** in Revit 2025 and wait for it to finish loading.
2. Select a wall panel. *(The ribbon is inert until a document is open — Revit greys every tab
   on its Home screen.)*
3. **GMP Platform** tab → **Ticket Drawing**.
4. Confirm the dialog. It shows the mark, the size, how many parts were read from the model and
   anything that will be flagged as derived.
5. A few seconds later (about 10 s for draw and plot) a dialog offers the finished sheets.

Output lands in `%ProgramData%\GMP\Platform\exports\<timestamp>-<mark>\`:

| File | What |
|---|---|
| `<mark>.ticket.json` | the Revit → AutoCAD hand-off |
| `<mark>.dwg` | the drawing, inside a copy of the seed |
| `<mark>_1_of_3.pdf` … `_3_of_3.pdf` | plotted sheets |
| `<mark>.draw.log` | what the engine did, and everything it could not resolve |

**Read the draw log.** It is where the engine reports what it derived rather than read, which
parts had no block, and what needs confirming. Nothing is silently assumed.

---

## 6. Iterating on the drawing engine without Revit

A full Revit run means opening the model; the AutoCAD half takes about 10 seconds. Once a
ticket JSON exists you can re-run just the drawing engine against it, which is how all the
drawing work was done:

```bat
dotnet build GmpPlatform.sln -c Release.R25
```

then invoke `GmpPlatform.Core.Interop.AcadRunner.Run(...)` with the existing
`<mark>.ticket.json`, the seed DWG and the built `GmpPlatform.Acad.dll`. The engine is
stateless with respect to Revit — it only reads the JSON.

---

## 7. Checking fidelity against the approved ticket

Two measures, both worth having, because they disagree:

```bat
REM ink coverage, per sheet
cd analysis\tools\SheetCompare
dotnet run -c Release -- <approved.pdf> <generated_1_of_3.pdf> <outDir> --page 1

REM object counts inside the sheet window
cd analysis\tools\CoverageReport
dotnet run -c Release -- <approved.full.json> <generated.full.json> <draw.log>
```

`SheetCompare` also writes a stacked comparison and a red-on-grey overlay. **The overlay is the
single most useful artefact for the demo** — the generated piece lies on top of the approved
one.

Ink rewards drawing *something*; object counts reward drawing the right *number* of things.
Quoting only one has already been misleading once.

---

## 8. Known gotchas

| Symptom | Cause |
|---|---|
| *"Drawing engine missing"* | `GmpPlatform.Acad.dll` not beside the add-in — rebuild the solution (§1) |
| Ribbon tab greyed out | no document open; Revit disables all tabs on the Home screen |
| Security prompt every launch | Revit is not exiting cleanly, so the trust never persists (§4) |
| Dimensions plot with no text | the font map is not set (§3) |
| Line weights look wrong | `FINAL PRINT.ctb` is not installed; plots fall back to `monochrome.ctb` |
| Build says "succeeded" but nothing changed | a stray `accoreconsole.exe` or Revit is holding the DLLs — kill them and rebuild |
| Build fails with "file is locked by Autodesk Revit" | Revit is running with the add-in loaded; close Revit and rebuild. To check that the code compiles while Revit is open, add `-p:DeployAddin=false` |
| Only *Ticket Drawing*, *Settings*, *About* and *Reports* on the ribbon | intended — see `ShowDeveloperTools` in §3 |
| Security prompt again after an update | expected for an unsigned add-in — Revit trusts the exact DLL (§4) |
| Ticket Drawing freezes Revit for ~5 minutes | an old build: plotting left the drawing modified and `QUIT` waited on a hidden prompt. Fixed in `AcadRunner`; rebuild |
| Settings ignored, defaults used | `settings.json` failed to parse; the reason is in the platform log and in the *seed not set* dialog |
| A cut-out has no *B.O.-n* stub, width or BLOCKOUT column | it touches a panel end (an end pocket, like FIP-1171's two 8" pockets). Only inner loops and cut-outs from the top or bottom edge are blockouts; end cut-outs belong to the edge details. The draw log's `openings:` line lists what was numbered |

The *"Build says succeeded but nothing changed"* row is worth taking seriously. More than once in
this project a step reported success without doing anything. **When a build or a run claims
success, check the artefact it was supposed to produce, not the exit code.**

---

## 9. Annual Revit upgrade (adding a new year)

The client moves Revit release every year or two, per project. What changes from year to year
is only the thin Revit layer; the ticket contract (`PieceTicket` JSON), `GmpPlatform.Core` and
the AutoCAD engine do not depend on the Revit year. Typical effort: 1–3 days, most of it testing.

1. **Toolchain.** Check the new year's runtime. Revit 2025–2026 run .NET 8; **Revit 2027 runs
   .NET 10**, so install the .NET 10 SDK and widen `global.json` before adding R27.
2. **Configuration.** Add `Debug.R<yy>;Release.R<yy>` to `Configurations` in
   `GmpPlatform.Revit.csproj`, add the year to the `GmpRequireSupportedRevit` guard, and add an
   `ItemGroup` pinning `Nice3point.Revit.Toolkit`, `Nice3point.Revit.Extensions` and
   `Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` to that year's versions. Add both solution
   configurations to `GmpPlatform.sln` (Core, engine and tests in Release for `Release.*`).
3. **Compile and fix.** Build `Release.R<yy>` with `-p:DeployAddin=false`. Fix removed or
   changed Revit APIs behind `#if REVIT20xx_OR_GREATER` (the SDK defines these per year).
   Keep `Release.R24`/`R25` building — earlier years stay supported while projects use them.
4. **Package.** Add the year to `-Years` in `tools/Package.ps1`.
5. **Test in the new Revit** on a model *saved in that year*: add-in loads (platform log shows
   `starting. Revit <year>` and `Settings read from ...`), then a full *Ticket Drawing* run,
   comparing the PDFs with the previous year's output for the same panel.
6. **AutoCAD.** Only if the client also moves AutoCAD to a new *API family*: 2025 and 2026 share
   one (no work); 2027 is a new family on .NET 10 and needs a second engine build against
   `AutoCAD.NET` 26.x, plus its releases in `AcadRunner.SupportedReleases` and `Install.ps1`.
7. **Ship.** Bump `<Version>` in `Directory.Build.props`, package, install, and answer the
   unsigned add-in prompt once per year (§4).

Never open a client model in a newer Revit to "try it": the upgrade is irreversible. Test on a
copy, and keep the original under `samples/` untouched.
