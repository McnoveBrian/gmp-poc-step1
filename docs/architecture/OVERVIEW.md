# Architecture overview

```
  Revit add-in (per year)         Version-neutral              AutoCAD engine
  +---------------------+         +------------------+         +--------------------+
  | GmpPlatform.Revit   | writes  | PieceTicket JSON | read by | GmpPlatform.Acad   |
  | R24 / R25 / R26     |-------> | SchemaVersion 1  |-------> | (accoreconsole)    |
  +----------+----------+         +--------^---------+         +----------+---------+
             |                             |                              |
             +------------- GmpPlatform.Core -----------------------------+
                            settings · standards · AcadRunner · reports
```

| Project | Role |
|---------|------|
| `GmpPlatform.Core` | `netstandard2.0` — ticket model, settings, standards profile, `AcadRunner`, reports. No Revit or AutoCAD API. |
| `GmpPlatform.Revit` | Add-in host. One build configuration per Revit year. Extracts geometry into `PieceTicket` and hands off to the engine. |
| `GmpPlatform.Acad` | Headless drawing engine. Draws into a **copy** of the client seed DWG; never loads into the Revit process. |
| `GmpPlatform.Core.Tests` | Convention and contract tests (stations, openings, hardware, standards). |

**Product path:** Ribbon **Ticket Drawing** → export ticket JSON → `AcadRunner` → DWG + PDF under `%ProgramData%\GMP\Platform\exports\`.

**Developer path (off by default):** Revit-sheet placeholders behind `ShowDeveloperTools`. Not for client use.

**Config:** `%ProgramData%\GMP\Platform\config\` — see [`config/`](../../config/) for examples.
