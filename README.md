# GMP Production Platform

Revit-to-AutoCAD piece-ticket automation for GMP Architecture — wall panels drawn into the client seed DWG and plotted to PDF.

| | |
|---|---|
| **Client** | GMP Architecture LLC |
| **Revit** | 2024 · 2025 · 2026 (one build each) |
| **AutoCAD engine** | 2025 / 2026 (`accoreconsole`) |
| **Version** | 0.2.0 |
| **Proposal** | [docs/TWO_PHASE_PROPOSAL.md](docs/TWO_PHASE_PROPOSAL.md) |

## What it does

1. Select a wall panel in Revit (Structural Framing wall panel or Wall).
2. Run **GMP Platform → Shop Tickets → Ticket Drawing**.
3. The add-in writes a `PieceTicket` JSON hand-off, then headless AutoCAD draws into a **copy** of the client's seed DWG and plots PDF sheets (TIF / BIF / Sections+BOM).
4. Exports and the `.draw.log` land under `%ProgramData%\GMP\Platform\exports\`.

This is the **product** path. A retired Revit-sheet placeholder route remains behind `ShowDeveloperTools` for engineering only — leave it off for clients.

Phase 1 still has content items waiting on GMP (section-cut rules, edge details G–U, notes, fonts/CTB). See [CHANGELOG.md](CHANGELOG.md).

## Repository layout

```
gmp-production-platform/
├── source/
│   ├── GmpPlatform.Core/      # Models, settings, AcadRunner, reports (no Revit)
│   ├── GmpPlatform.Revit/     # Add-in host, ribbon, commands, exporters
│   └── GmpPlatform.Acad/      # Headless AutoCAD drawing engine
├── tests/GmpPlatform.Core.Tests/
├── tools/                     # Package.ps1, Install.ps1
├── config/                    # settings/standards JSON examples
├── assets/                    # Font map (gmp.fmp)
├── docs/                      # Proposal, requirements, usage, architecture
├── samples/                   # LOCKED client originals — do not delete
├── GmpPlatform.sln
├── Directory.Build.props
├── RUNBOOK.md
└── README.md
```

Architecture: [docs/architecture/OVERVIEW.md](docs/architecture/OVERVIEW.md).

## Build

Full instructions — build, install, run, test, gotchas — are in **[RUNBOOK.md](RUNBOOK.md)**.

Requires **.NET SDK 8** (see `global.json`) and Visual Studio 2022 or Build Tools.

```powershell
dotnet build GmpPlatform.sln -c Release.R25
```

Build the **solution**, not the add-in project alone: the solution also builds the AutoCAD engine and copies it next to the add-in.

Package all years:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Package.ps1
```

Install from the unzipped package with `Install.ps1` (Revit must be closed).

## Quick start (Revit 2025)

1. Copy [`config/settings.example.json`](config/settings.example.json) to `%ProgramData%\GMP\Platform\config\settings.json` and set `SeedDwgPath` / `FontMapPath`.
2. Build `Release.R25` or install from `tools\Package.ps1`.
3. Start **Revit 2025** and open a **copy** of the client model (never the original under `samples/`).
4. Select one `WALL_PANEL_INSULATED_VERTICAL` (or similar).
5. Ribbon: **GMP Platform → Ticket Drawing**.
6. Check the PDFs and `.draw.log` under `%ProgramData%\GMP\Platform\exports\`.

Usage: [docs/user-guides/usage-guide.md](docs/user-guides/usage-guide.md).

## Data folders

| Path | Purpose |
|------|---------|
| `%ProgramData%\GMP\Platform\config` | `settings.json`, optional `standards.json` |
| `%ProgramData%\GMP\Platform\logs` | Daily log files |
| `%ProgramData%\GMP\Platform\reports` | Per-run HTML/JSON reports |
| `%ProgramData%\GMP\Platform\exports` | Ticket DWG / PDF output |

Override the root with environment variable `GMP_PLATFORM_ROOT` if needed.

## Important notes

- **Do not delete or overwrite** files under `samples/`.
- Never open the client RVT with a newer Revit year than it was last saved in (irreversible upgrade).
- The add-in is unsigned until code signing is arranged; answer Revit's "Always Load" once after each DLL update.

## Documentation

- [Runbook](RUNBOOK.md)
- [Usage guide](docs/user-guides/usage-guide.md)
- [Architecture](docs/architecture/OVERVIEW.md)
- [Changelog](CHANGELOG.md)
- [Two-phase proposal](docs/TWO_PHASE_PROPOSAL.md)
- [Requirements](docs/PROJECT_REQUIREMENTS.md)
- [Abbreviations](docs/ABBREVIATIONS.md)
