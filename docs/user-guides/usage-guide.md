# Usage guide — GMP Production Platform

**Add-in:** GMP Production Platform 0.2.0  
**Revit:** 2024 / 2025 / 2026  
**Product path:** Ticket Drawing (AutoCAD seed DWG → PDF)

---

## 1. Install

**From a package** (preferred on a client machine):

```powershell
powershell -ExecutionPolicy Bypass -File tools\Package.ps1
# unzip dist\GmpPlatform-0.2.0.zip, then:
powershell -ExecutionPolicy Bypass -File Install.ps1
```

**From a local build:**

```powershell
dotnet build GmpPlatform.sln -c Release.R25
```

Close Revit before rebuilding or reinstalling if the DLL is locked. On first load of a new DLL, choose **Always Load** on the unsigned-add-in prompt.

---

## 2. Configure once per machine

1. Copy [`config/settings.example.json`](../../config/settings.example.json) to  
   `%ProgramData%\GMP\Platform\config\settings.json`.
2. Set:
   - **`SeedDwgPath`** — the client's approved seed DWG (engine draws into a **copy**).
   - **`FontMapPath`** — usually the repo `assets\gmp.fmp` (see [`assets/README.md`](../../assets/README.md)).
3. Leave `ShowDeveloperTools` and `DrawGeneratedRebar` **false** unless you are developing.
4. Optional: copy `standards.example.json` → `standards.json` only when layer/block names differ from the built-in Tindall defaults.

Or use **GMP Platform → Shop Tickets → Settings** after the add-in loads (seed path and related options).

---

## 3. Open a model

1. Start the matching Revit year.
2. Open a **working copy** of the project (never the locked original under `samples/`).
3. Select a wall panel instance (e.g. `WALL_PANEL_INSULATED_VERTICAL`).

---

## 4. Generate a ticket

1. Ribbon: **GMP Platform → Shop Tickets → Ticket Drawing**.
2. Confirm the dialog; wait for AutoCAD Core Console to finish (typically under a minute).
3. Open the folder shown in the success dialog (under `%ProgramData%\GMP\Platform\exports\`).
4. Review:
   - `*_1_of_3.pdf` / `*_2_of_3.pdf` / `*_3_of_3.pdf`
   - The `.dwg`
   - The `.draw.log` (openings, assembly hardware, missing blocks, derived items)

---

## 5. Ribbon map (product)

| Panel | Button | Purpose |
|-------|--------|---------|
| Shop Tickets | **Ticket Drawing** | Production path — AutoCAD ticket |
| Shop Tickets | **Settings** | Seed DWG, font map, options |
| Help | **About** | Version, data root |
| Help | **Reports** | Opens the reports folder |

Developer buttons (*Wall Ticket*, *Export DWG*, *Verify*) appear only when `"ShowDeveloperTools": true`. Do not enable them for client sessions.

---

## 6. Tips

1. Always work on a model copy.
2. If dimensions plot blank, fix `FontMapPath`.
3. If line weights look wrong, install the client's `FINAL PRINT.ctb` (when provided).
4. Items listed as assembly hardware (e.g. EMS on INS087) are intentionally off the BOM — confirm with GMP if unsure.
5. Full troubleshooting: [RUNBOOK.md](../../RUNBOOK.md).
