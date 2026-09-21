# Demo usage guide — GMP Production Platform

**Add-in:** GMP Production Platform 0.1.0 (demo)  
**Revit:** 2025 (primary)  
**Purpose:** Accompany the Phase 1 proposal with a runnable wall shop-ticket workflow

---

## 1. Install / build

From the repository root:

```powershell
dotnet build source/GmpPlatform.Revit/GmpPlatform.Revit.csproj -c Release.R25
```

Confirm these exist after a successful build:

- `%AppData%\Autodesk\Revit\Addins\2025\GmpPlatform.Revit.addin`
- `%AppData%\Autodesk\Revit\Addins\2025\GmpPlatform.Revit\GmpPlatform.Revit.dll`

Close Revit before rebuilding if the DLL is locked.

---

## 2. Open a model

1. Start **Revit 2025**.  
2. Open the sample model, for example:  
   `samples/45431_Pilgrims RTE Main Plant R25.rvt`  
   (or any project that contains insulated wall panels).  
3. Find a wall panel instance such as **WALL_PANEL_INSULATED_VERTICAL** type **D-70**.

---

## 3. Choose a title block (recommended)

1. Open the target Revit model (so its title-block families are available).  
2. **GMP Platform → Shop Tickets → Settings**.  
3. Pick the title-block family/type that should appear on generated sheets.  
4. Optionally adjust **note clearance** and **section cell gap** (larger = less overlap).  
5. Click **Save**.

Settings are stored at `%ProgramData%\GMP\Platform\config\settings.json`.

---

## 4. Generate a shop ticket

1. Select **one** wall panel (Structural Framing or Wall).  
2. On the ribbon, open the **GMP Platform** tab.  
3. Click **Shop Tickets → Wall Ticket**.  
4. Review the confirmation dialog (mark, family, size, **title block**, warnings).  
5. Click **Yes**.

### What gets created

| Sheet | Content |
|-------|---------|
| `{Mark}-1` TIF | Sandwich panel elevation outline, opening, side thickness strip, joint callout; notes in a clear left column; live section viewport when creation succeeds; selected title block |
| `{Mark}-2` BIF | Finish elevation with reveal, same outline treatment and non-overlapping notes; selected title block |
| `{Mark}-3` SECTIONS | AFIP-style sandwich section cells (A/B/C/…) in a spaced grid, BOM column to the right; selected title block |

Also created:

- Drafting views named `{Mark}_TifFabrication`, `{Mark}_BifFinish`, `{Mark}_SectionsAndBom`  
- Optional section view `{Mark}_SEC`  
- Run report under `%ProgramData%\GMP\Platform\reports\GMP-yyyyMMdd-HHmmss\`

---

## 5. Export a DWG (optional)

1. Open one of the new sheets (or its drafting/section view).  
2. Click **Shop Tickets → Export DWG**.  
3. Find the file under `%ProgramData%\GMP\Platform\exports\<timestamp>\`.

**Note:** This export is a technical DWG only. Matching Tindall/client dimension and text styles is a later “standards bridge” step described in the proposal.

---

## 6. Reports and logs

| Action | Location |
|--------|----------|
| Ribbon **Help → Reports** | Opens the reports folder |
| HTML report | `run-report.html` (open in a browser) |
| JSON report | `run-report.json` (for support) |
| Logs | `%ProgramData%\GMP\Platform\logs\gmp-platform-*.log` |
| Settings | `%ProgramData%\GMP\Platform\config\settings.json` |

---

## 7. Tips for a clean demo to the client

1. Use **Settings** first and pick the project title block so sheets match the manual Pilgrim’s layout frame.  
2. Use a panel with a filled **Mark** parameter so sheet numbers look realistic.  
3. Select a panel that sits between neighbors if you want neighbor marks listed.  
4. Show Sheet 1 (elevation + section) then Sheet 3 (section grid + BOM).  
5. Mention clearly: outlines and layout are improved demo fidelity toward AFIP-1035; full connection rules and AutoCAD standards remain Phase 1 delivery work.  
6. Keep `samples/` originals untouched; generate sheets inside a working copy of the model if you want to avoid saving over the sample.

---

## 8. Troubleshooting

| Issue | What to try |
|-------|-------------|
| No **GMP Platform** tab | Confirm add-in files under Addins\2025; restart Revit; check Journal for load errors |
| “Select exactly one wall panel” | Clear multi-select; pick one Structural Framing / Wall instance |
| Sheets have no title block | Run **Settings**, pick a title-block type loaded in the model, Save, then generate again |
| Notes still feel tight | Increase note clearance / section cell gap in **Settings** |
| Section not created | Some orientations fail section box creation; sheets still generate with drafting views |
| DWG export fails | Ensure a sheet/drafting/section is active; check Revit DWG export availability |
| DLL locked on rebuild | Close Revit, then rebuild |

---

## 9. Ribbon map

```
GMP Platform
├── Shop Tickets
│   ├── Wall Ticket     Generate 3-sheet package (title block + outlines)
│   ├── Export DWG      Export active sheet/view
│   └── Settings        Title block + layout clearances
└── Help
    ├── About           Version and data paths
    └── Reports         Open reports folder
```
