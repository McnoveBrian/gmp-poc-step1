# GMP Production Platform

Revit add-in demo for GMP Architecture — precast wall shop-ticket automation (Revit → sheets → DWG).

| | |
|---|---|
| **Client** | GMP Architecture LLC |
| **Primary Revit** | 2025 (also builds for 2024) |
| **Demo version** | 0.1.0 |
| **Proposal** | [docs/PHASE1_PROPOSAL.md](docs/PHASE1_PROPOSAL.md) |

This repository includes the Phase 1 proposal materials and a **working demo add-in** you can load in Revit 2025 to show the basic end-to-end workflow on a simple wall panel.

## What the demo does

1. Select a wall panel in the model (Structural Framing wall panel or Wall).  
2. Run **GMP Platform → Shop Tickets → Wall Ticket**.  
3. The add-in creates **three sheets** (TIF / BIF / Sections+BOM) with:
   - Drafting layouts sized from the panel bounds  
   - Sample **Detail Lines** (panel outline, demo opening, joint/connection placeholder)  
   - A **section cut** through the panel when Revit allows it  
   - Production notes and a demo BOM block on sheet 3  
4. Optionally run **Export DWG** on a sheet to write a DWG under `%ProgramData%\GMP\Platform\exports`.  
5. A run report (HTML + JSON) is written under `%ProgramData%\GMP\Platform\reports`.

This is a **framework demo**, not the full AFIP-1035 production match. Full connection Detail Line rules, client dimension styles, and opening matrices come in the funded Phase 1 build.

## Repository layout

```
12-GMP Production Platform/
├── source/
│   ├── GmpPlatform.Core/      # Models, planner, paths, logging, reports (no Revit)
│   └── GmpPlatform.Revit/     # Add-in host, ribbon, commands, generators
├── docs/                      # Proposal, requirements, user guide
├── samples/                   # LOCKED client originals (RVT / PDF / DWG) — do not delete
├── references/                # Meeting screenshots, PDF page renders
├── deliverables/              # Working outputs (optional)
├── GmpPlatform.sln
├── Directory.Build.props
└── README.md
```

Architecture follows the same Core / Revit split used on the SMH Production Platform (Nice3point Revit SDK, Application-type `.addin`, ribbon commands).

## Build

Requires **.NET SDK 8** and Visual Studio 2022 (or Build Tools).

```powershell
dotnet build source/GmpPlatform.Revit/GmpPlatform.Revit.csproj -c Release.R25
```

With `DeployAddin=true`, the build copies the add-in to:

`%AppData%\Autodesk\Revit\Addins\2025\`

For Revit 2024:

```powershell
dotnet build source/GmpPlatform.Revit/GmpPlatform.Revit.csproj -c Release.R24
```

## Quick start (Revit 2025)

1. Build with `Release.R25` (see above).  
2. Start **Revit 2025** and open the Pilgrim’s RTE sample (or any model with a wall panel).  
3. Select one `WALL_PANEL_INSULATED_VERTICAL` (or similar) instance.  
4. Ribbon: **GMP Platform → Wall Ticket → Yes**.  
5. Review the three new sheets in the project browser.  
6. Open a sheet and run **Export DWG** if you want a demo DWG file.  
7. **Help → Reports** opens the report folder.

Full steps: [docs/user-guides/demo-usage-guide.md](docs/user-guides/demo-usage-guide.md)

## Data folders

| Path | Purpose |
|------|---------|
| `%ProgramData%\GMP\Platform\logs` | Daily log files |
| `%ProgramData%\GMP\Platform\reports` | Per-run HTML/JSON reports |
| `%ProgramData%\GMP\Platform\exports` | DWG export output |

Override root with environment variable `GMP_PLATFORM_ROOT` if needed.

## Important notes

- **Do not delete or overwrite** files under `samples/`.  
- Demo sheets intentionally use simplified Detail Lines; production detailing rules will be agreed with GMP.  
- DWG export does not yet apply the full client standards bridge described in the proposal.

## Documentation

- [Phase 1 proposal](docs/PHASE1_PROPOSAL.md)  
- [Requirements](docs/PROJECT_REQUIREMENTS.md)  
- [Abbreviations](docs/ABBREVIATIONS.md)  
- [Meeting summary](docs/GMP_Revit_to_AutoCAD_Precast_Automation_Meeting_Summary_2026_09.md)  
- [Demo usage guide](docs/user-guides/demo-usage-guide.md)  
