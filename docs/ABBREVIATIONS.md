# Abbreviations Glossary — AFIP-1035 Shop Ticket

**Source:** `samples/45430_AFIP1035.pdf` (Sheets 1–3)  
**Project:** Pilgrim’s RTE · Tindall Corporation · Mark AFIP-1035  

Meanings below are the standard industry / drawing usage for this ticket. Items marked **(inferred)** are not spelled out on the sheet but are conventional in precast shop drawings; confirm with GMP/Tindall if needed for automation mapping.

---

## 1. Form orientation (casting position)

| Abbreviation | Meaning | Where it appears |
|--------------|---------|------------------|
| **TIF** | **Top In Form** — face that is up while casting | Notes (“ROCK ALL TIF EDGES”), part callouts `(TIF)`, Sheet 1 production notes |
| **BIF** | **Bottom In Form** — face against the form bottom while casting | “FINISH (BIF)”, “BIF VIEW CONCRETE REMOVED”, part callouts `(BIF)` |
| **SIF** | **Side In Form** — side face in the form | “SIF VIEW”, section centerline notes (e.g. SIF CTR’D) |
| **T/F** | **Top / Form** face (same idea as TIF; short form) | Sheet 2 note: “ROCK ALL T/F EDGES” |
| **TOP** | Top (placement / mix layer) | BOM: `CAN0760010D (TOP)` |
| **BOT** | Bottom (placement / mix layer) | BOM: `CAA0610010E (BOT)` |

---

## 2. Geometry, views & drafting shorthand

| Abbreviation | Meaning | Notes |
|--------------|---------|--------|
| **B.O.** / **B/O** | **Block Out** — opening or void formed in the concrete | Also used as “form B/O” (block-out form). Distinct from “by others” unless context says otherwise. |
| **B.O.-2**, **B.O.-3** | Numbered block-out instances | Dimension strings reference specific openings |
| **SEC.** / **SECTION** | Cross-section view | Sections A–N, Section “S” |
| **DET.** / **DETAIL** | Enlarged detail view | Detail “P”, Details G–U |
| **TYP.** | **Typical** — applies to all similar locations | e.g. “(TYP. @ ALL SIDES OF B.O.)” |
| **U.N.O.** | **Unless Noted Otherwise** | General drawing convention |
| **N.T.S.** | **Not To Scale** | On some section dimensions |
| **MIN.** | **Minimum** | e.g. MIN. RADIUS, MIN. LAP |
| **MAX.** | **Maximum** | Standard companion term (if present) |
| **DIM** / **DIMS.** | **Dimension(s)** | “DIRECTION OF STUB DIMS.” |
| **CTR’D** | **Centered** | Alignment of inserts/bars |
| **CL** | **Centerline** | Symbol ℄ / “CL (n) RSX…” |
| **THK.** | **Thick / Thickness** | “1\" THK. BEHIND …” |
| **LAP** | Lap splice length of reinforcement | e.g. “(LAP 2'-1")” |
| **W/** | **With** | Common note shorthand |
| **@** | **At** | “TERMINATED @ PAS032” |
| **T/** | **Top of** | Prefix on elevation refs (see below) |
| **REQ’D** | **Required** | “NO. REQ’D” = quantity of pieces to cast |
| **NO.** | **Number** | Number required, design no., etc. |
| **QTY** | **Quantity** | Parts list column |
| **PROD.** / **PROD. NOTE** | **Production** note for the plant | Clouded callouts; fabrication instructions |
| **INSUL.** | **Insulation** | Sandwich-panel foam core |
| **RECESS** | Recessed pocket in concrete surface | Full word, common callout |
| **CHAMFER** | Beveled edge | Full word; often with size e.g. 1/2\" |
| **REVEAL** | Architectural groove / reveal | “T/(2) REVEALS (BIF)” |
| **CORBEL** | Projecting concrete ledge | Sections M / M1 |
| **POCKET** | Local recess / cast pocket | Section C family |
| **SOLID ZONE** | Region without insulation (solid concrete) | Section A |

### Elevation / “top of” prefixes

| Abbreviation | Meaning |
|--------------|---------|
| **T/BRG PLATE** | Top of bearing plate |
| **T/CORBEL** / **T/CORBEL DIM.** | Top of corbel / corbel dimension line |
| **T/REVEAL** | Top of reveal (or reveal at that face) |

---

## 3. Materials, strength & finish

| Abbreviation | Meaning | Notes |
|--------------|---------|--------|
| **PSI** | Pounds per square inch | RELEASE 4000 / 28 DAY 6500 |
| **LBS** | Pounds (weight) | WEIGHT IN LBS: 42,800 |
| **AS** | **Approved Sample** (finish) | Legend: light sandblast per approved sample; diamond/hex finish codes |
| **Grade 'A' Finish** | Finish grade classification | Shop notes (Sheet 3) |
| **Chair Strand** | Strand chair / support for prestressing strands | Finish/shop legend |
| **STRIPPING** | Removing the piece from the form | “NOT FOR STRIPPING. FOR FIELD ONLY.” |
| **FIELD** | Installed / used at the jobsite (not in plant stripping) | Same note |
| **ROCK** | Roughen / rock edges (texture for bonding) | “ROCK ALL TIF EDGES” |
| **SANDBLAST** | Abrasive blast finish | Chamfer/reveal production notes |

Diamond/hex **numeric finish codes** (e.g. 5, 13, 1, 8, 12, 15) are plant finish/face codes for TOP IN FORM / FORMED SIDES / ENDS — exact legend is plant-standard, not fully spelled on this sheet.

---

## 4. Title block & project identity

| Abbreviation | Meaning | Example |
|--------------|---------|---------|
| **MARK** | Piece / panel identifier | AFIP-1035 |
| **AFIP** | Mark series prefix **(inferred)** — architectural / insulated wall-panel mark family used by this producer | Confirm exact expansion with GMP/Tindall |
| **PID** | Piece / product ID number | PID: 1035 (matches mark suffix) |
| **PROJ. LEAD** | Project Lead | Initials MS |
| **ENGR. CHKD** | Engineer Checked | Initials PB |
| **RTE** | **Ready-To-Eat** **(inferred)** — facility type in “Pilgrim’s RTE” | Food-plant project naming; not “route” |
| **D-70A** | Design number | Title block |
| **CODE** | Internal shop/design code | 523 |

Drafter initials on this ticket: **VS** (Drawn), **FF** (Checked), **MS** (Proj. Lead), **PB** (Engr. Chkd).

---

## 5. Part-number prefixes (BOM / callouts)

These are **catalog / plant part series**, not English abbreviations. Treat as IDs to pull from the model/BOM.

| Prefix | Typical role on this ticket **(inferred from use)** | Examples |
|--------|------------------------------------------------------|----------|
| **RSX** | Reinforcing bars / rebar mark series | RSX03023, RSX03143, RSX05480 |
| **SPS** | Special / structural bar or plate series | SPS025 |
| **PAS** | Plate / assembly / embed plate series | PAS032, PAS018 |
| **INS** | Insert (cast-in hardware) | INS087 |
| **INX** | Insert / connection accessory series | INX014 |
| **SIX** | Insert / sleeve / related embed series | SIX300, SIX001 |
| **SNX** | Sandwich / insulation-related material or accessory | SNX100 |
| **WMX** | Wire mesh / welded wire series | WMX007 |
| **PSX** | Prestressing strand / related | PSX500 |
| **CAN** / **CAA** | Concrete mix designations (top/bot layers) | CAN0760010D (TOP), CAA0610010E (BOT) |

Exact dictionary definitions of each prefix should come from Tindall/GMP standards books (not fully defined on this PDF alone).

---

## 6. Units & symbols (for reading dims)

| Symbol | Meaning |
|--------|---------|
| **'** | Feet |
| **"** | Inches |
| **Ø** / circle marks | Often openings, bars, or detail references |
| Cloud / revision cloud | Highlights a **PROD. NOTE** or critical location |

---

## 7. Quick “read the note” cheat sheet

| You see… | Read as… |
|----------|----------|
| `RSX03143 (TIF)` | Part RSX03143 placed on the **top-in-form** face |
| `1/2" CHAMFER (BIF)` | Chamfer on the **bottom-in-form** face |
| `SEE SHEET "2" FOR BIF VIEW CONCRETE REMOVED` | Sheet 2 shows the bottom face with concrete graphics removed (finish layout) |
| `REDUCE INSUL. TO 1" THK. BEHIND PAS032` | Thin insulation to 1\" locally behind embed PAS032 |
| `NOT FOR STRIPPING. FOR FIELD ONLY.` | Hardware/detail is for site use, not plant stripping lifts |

---

## 8. Uncertain — confirm with client

| Term | Best guess | Action |
|------|------------|--------|
| **AFIP** | Architectural Finish Insulated Panel (or similar mark family) | Ask GMP for official mark legend |
| **FIP** (seen in related file names, not this mark) | Often “floor insulated panel” family | Confirm if/when those marks are automated |
| Numeric finish diamonds (5, 13, …) | Plant finish codes | Obtain finish legend / standards book |
| Exact RSX/PAS/INS… series names | Internal catalog | Map from BOM + standards, not English expansion |

---

*Compiled for Step 1 comprehension of `45430_AFIP1035.pdf`. Safe to extend as more marks (columns, beams, etc.) are added.*
