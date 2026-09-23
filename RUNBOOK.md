# Runbook — build, install, run, test

Everything needed to take this from a clone to a plotted shop ticket, and to check it still
works. Verified on this machine on 2026-09-22.

---

## 0. Prerequisites

| Need | Why | Check |
|---|---|---|
| **.NET 8 SDK** | builds everything | `dotnet --version` → 8.x |
| **Revit 2025** | the add-in targets it | `C:\Program Files\Autodesk\Revit 2025\Revit.exe` |
| **AutoCAD 2025** | the drawing engine runs in `accoreconsole` | `C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe` |

**Revit 2027 and AutoCAD 2027 may also be installed.** Never open the client RVT with Revit
2027 — it upgrades the file irreversibly and there is no way back. Always Revit 2025, always on
a copy of the model, never the original under `samples/`.

---

## 1. Build

```bat
cd gmp-poc-step1
dotnet build GmpPlatform.sln -c Release.R25
```

One command does everything: it builds the AutoCAD drawing engine first, the Revit add-in
second, copies the engine next to the add-in, and **deploys the add-in to Revit**
(`%APPDATA%\Autodesk\Revit\Addins\2025\`). There is no separate install step.

Confirm the engine actually shipped — this is the piece that used to go missing silently:

```bat
dir "%APPDATA%\Autodesk\Revit\Addins\2025\GmpPlatform.Revit\GmpPlatform.Acad.dll"
```

If that file is absent the add-in will load but report *"Drawing engine missing"* when you
press the button.

---

## 2. Tests

```bat
dotnet test tests\GmpPlatform.Core.Tests\GmpPlatform.Core.Tests.csproj
```

43 tests, about a second. They cover the hand-off contract, the unit rounding, and the client's
drafting conventions — the station direction, the piece placement, the callout offset. If one
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
- `AutoCadConsolePath` may be left out; the runner finds AutoCAD 2025 itself and prefers it
  over any 2027 install.
- `ShowDeveloperTools` (default `false`) adds a *Developer* panel with the retired *Wall
  Ticket* route, *Export DWG* and *Verify*. **Leave it off on the demo machine**: *Verify*
  generates placeholder Revit sheets, and clicking it in front of a client puts them on screen.

The file is read case-insensitively, so `seedDwgPath` also works.

---

## 4. First Revit launch — clear the security prompt once

The add-in is unsigned, so Revit asks about it. Do this **before** the demo, not during:

1. Start Revit 2025.
2. On **"Security — Unsigned Add-In"**, choose **Always Load**.
3. Close Revit normally.

Revit records the trust against the add-in's `AddInId` on a clean exit and never asks again.
Verify:

```bat
reg query "HKCU\Software\Autodesk\Revit\Autodesk Revit 2025\CodeSigning"
```

You should see `c7a91e2b-4f58-4d0a-9e33-8b1c6d2a7f10 = 1`. If the key is empty, Revit did not
shut down cleanly — check that no other unsigned add-in (the old `GmpRevitSurvey` analysis
utility, if it is still registered) is crashing it on exit.

---

## 5. Run it

1. Open the model **copy** in Revit 2025 and wait for it to finish loading.
2. Select a wall panel. *(The ribbon is inert until a document is open — Revit greys every tab
   on its Home screen.)*
3. **GMP Platform** tab → **Ticket Drawing**.
4. Confirm the dialog. It shows the mark, the size, how many parts were read from the model and
   anything that will be flagged as derived.
5. About a minute later a dialog offers the finished sheets.

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

A full Revit run takes ~10 minutes; the AutoCAD half takes ~1. Once a ticket JSON exists you
can re-run just the drawing engine against it, which is how all the drawing work was done:

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

That last one is worth taking seriously. More than once in this project a step reported success
without doing anything. **When a build or a run claims success, check the artefact it was
supposed to produce, not the exit code.**
