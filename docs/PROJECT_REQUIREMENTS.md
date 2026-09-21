# GMP Production Platform — Systematized Project Requirements

**Client:** GMP Architecture LLC ([gmparchitecture.com](https://gmparchitecture.com/))  
**Domain:** Precast concrete shop-ticket drafting for producers (sample producer: Tindall Corporation)  
**Engagement type:** Side/contract development — Revit + coding expertise  
**Document status:** Aligned to formal meeting summary `GMP_Revit_to_AutoCAD_Precast_Automation_Meeting_Summary_2026_09.md` (18 Sep 2026) + golden sample AFIP-1035  
**Sample lock:** Originals in `samples/` must never be deleted or overwritten  
**Proposal:** `docs/PHASE1_PROPOSAL.md` (revised 2026-09-21)

---

## 1. Problem statement

GMP produces shop tickets (fabrication drawings) for precast elements. Models and erection info live in Revit; fabrication tickets are still largely manual and finish in AutoCAD (cleanup + PDF). The internal team has experimented with ticketing automation (~50%) but lacks bandwidth. They need a developer with **both Revit/BIM and API development** skills.

**Target workflow:**  
`Revit → Automation → Shop-ticket docs → DWG → 10–15% AutoCAD cleanup → PDF`

**Target operating model (meeting):** ~**85–90%** automated production; **10–15%** human drafting/checking/cleanup. **100% automation is not required.**

---

## 2. Goal hierarchy

| Level | Goal |
|-------|------|
| **Business** | Faster, more consistent shop tickets for picky precast clients; free AutoCAD drafters from repetitive upstream layout |
| **Product** | Modular precast documentation platform (walls first; later beams, columns, slabs, …) |
| **Immediate (analysis)** | Feasibility + technical approach + timeframe + price from real Revit/DWG/PDF sample |
| **Phase 1 Development** | One representative wall (AFIP-1035) end-to-end at ~85–90% + reusable modular foundation |

---

## 3. Phased delivery (roadmap)

| Phase | Scope | Success criteria | Status |
|-------|--------|------------------|--------|
| **Analysis / Step One** | Review Revit 2025 + DWG + PDF; propose approach, time, price, questions | GMP can decide go/no-go | **Complete → see PHASE1_PROPOSAL** |
| **Phase 1 Development** | Automate representative wall shop ticket | Client-standard look; views/BOM; clean DWG; 10–15% cleanup; modular foundation | After GMP approval |
| **Phase 2+** | More wall conditions; then columns/beams/slabs; PH team workflow | Agreed commercials + backlog | After Phase 1 demo |
| **Later** | Broader platform (batch, validation, QA/QC, rule config) | Production use | Future |

---

## 4. Functional requirements

### 4.1 End-to-end pipeline (must)

| ID | Requirement |
|----|-------------|
| FR-01 | Read element data from Autodesk **Revit 2025** (C# / Revit API primary; Python/Dynamo secondary) |
| FR-02 | Classify element and apply **component-specific** drafting rules (wall first; modular for later types) |
| FR-03 | Generate required views/sections/details, dims, annotations, notes, BOM, sheet composition in Revit |
| FR-03a | Include **adjacent wall joints and connection components** in detail documentation (not subject panel alone) |
| FR-03b | After section/view generation, apply automated **Detail Line** (and related 2D) drafting for assembly graphics not fully modeled in 3D |
| FR-03c | Section/detail **cut locations** driven by agreed rules (clarified with GMP) |
| FR-04 | Export **DWG** and reconcile client standards (dimstyles, text, layers, colors, linetypes, weights, blocks, symbols) |
| FR-05 | Multi-sheet package consistent with producer standards (sample: **3 sheets**) |
| FR-06 | Include TIF / BIF / SIF / MARK END and feature-driven sections/details as in golden sample |
| FR-07 | Include **Bill of Materials** (part number + quantity) and fabrication metadata |
| FR-08 | Include concrete / engineering summary (strengths, weight, face codes) |
| FR-09 | Include title block metadata (project, mark, sheet #, design no., personnel, dates) |
| FR-10 | Output quality target: **~85–90%** complete; **10–15%** AutoCAD cleanup intentional |
| FR-11 | **Exception handling:** flag unexpected conditions for manual review rather than emit incorrect tickets |

### 4.2 Element types (product backlog)

| Priority | Element type | Phase 1 Development? |
|----------|--------------|----------------------|
| P0 | Walls (precast panels / AFIP-class) | **Yes** — AFIP-1035 |
| P1 | Columns | No |
| P1 | Beams | No |
| P1 | Slabs / double tees (“double pieces”) | No |
| P2 | Other shapes as needed by producer jobs | No |

### 4.3 Golden sample — AFIP-1035 (acceptance reference)

| Field | Value |
|-------|--------|
| Producer | Tindall Corporation |
| Project | Pilgrim’s RTE |
| Project no. | 45430 |
| Mark | AFIP-1035 |
| Design no. | D-70A |
| Sheets | 3 of 3 |
| Approx. size | ~46'-3" × ~12'-1¾" (panel); weight 42,800 lbs |
| Source model | `samples/45431_Pilgrims RTE Main Plant R25.rvt` |
| Target DWG | `samples/45430_AFIP1035.dwg` |
| Target PDF | `samples/45430_AFIP1035.pdf` |

#### Sheet 1 — Top-in-form (TIF) plan

- Main plan with full dimensional control and MARK END / stub direction
- Rebar / insert callouts (RSX, INS, etc.) with TIF/BIF orientation
- Blockouts (B.O.), chamfers, production notes
- Section **S** (e.g. concrete mix transition)
- Side / tie elevation
- Detail **P** (local reinforcement / bearing)

#### Sheet 2 — Bottom finish layout (BIF), concrete removed

- Finish, reveals, chamfers (sandblast notes where shown)
- Edge profile details (G, H, J, K, L, P, U)
- Cross-refs to sheets 1 and 3

#### Sheet 3 — Sections + BOM + concrete info

- Sections A–N (pockets, recesses, insulation reductions, corbel, fastening holes, etc.)
- Parts list + quantities
- Release / 28-day PSI, weight, TOP IN FORM / formed sides / ends codes
- Shop notes (finish grade, sandblast, strand chairs, etc.)

### 4.4 Style / fidelity requirements (client-critical)

| ID | Requirement |
|----|-------------|
| ST-01 | Dimension style must closely match sample (client described as “pretty picky”) |
| ST-02 | Layering, text heights, title block arrangement should feel like producer standards |
| ST-03 | Production notes, callout language (TIF/BIF, PROD. NOTE, etc.) should match conventions |
| ST-04 | Phase 1 judged by closeness to golden PDF/DWG and usability for AutoCAD finishing |

---

## 5. Non-functional requirements

| ID | Category | Requirement |
|----|----------|-------------|
| NFR-01 | Accuracy | ~85–90% automation; 10–15% cleanup; not 100% |
| NFR-02 | Platforms | Revit **2025** (do not upgrade sample for Phase 1); AutoCAD DWG + PDF finish |
| NFR-03 | Languages | **C# / .NET + Revit API** primary; Python/Dynamo supporting only |
| NFR-04 | Architecture | Rule-based modular (shared services + per-element-type rules) |
| NFR-05 | Collaboration | Independent analysis first; then PH Revit team; ask GMP—do not assume production rules |
| NFR-06 | Work mode | Flexible / side job OK; async email + screen-share as needed |
| NFR-07 | Data integrity | Never delete or overwrite originals in `samples/` |
| NFR-08 | Presentation | Client-approved visual standard is a first-class acceptance criterion |

---

## 6. Explicit non-goals (Phase 1 Development)

- Full multi-element production system (beams/columns/slabs in Phase 1)
- Zero manual drafting / 100% automation
- Replacing GMP’s modeling / QC process
- Supporting Revit versions other than **2025** for the sample
- Silent generation of incorrect tickets on exception cases

---

## 7. Actors & collaboration

| Actor | Role |
|-------|------|
| Greg / GMP owners | Business scope, go/no-go, engineering rules when asked |
| Marchin Sikorsky | Meeting stakeholder (18 Sep 2026) |
| Brian | Analysis, C# Revit API development, DWG bridge |
| GMP Philippines Revit/drafting team | Modeling conventions, families, params, templates (after initial review) |
| AutoCAD drafters | Final 10–15% cleanup and PDF |

---

## 8. Inputs & outputs

### Inputs (analysis + Phase 1)

| Artifact | Location / note |
|----------|-----------------|
| Meeting summary | `docs/GMP_Revit_to_AutoCAD_Precast_Automation_Meeting_Summary_2026_09.md` |
| Revit model R25 | `samples/45431_Pilgrims RTE Main Plant R25.rvt` |
| Golden DWG | `samples/45430_AFIP1035.dwg` |
| Golden PDF | `samples/45430_AFIP1035.pdf` |
| Meeting visuals | `references/meeting-0918/` |
| PDF page renders | `references/shop-ticket-renders/` |

### Outputs

| Stage | Artifact |
|-------|----------|
| **Analysis (now)** | Technical approach, timeframe, price bands, clarification questions → `PHASE1_PROPOSAL.md` |
| **Phase 1 Development** | Generated DWG shop ticket for AFIP-1035-class wall; gap list (10–15%); modular wall foundation |

---

## 9. Acceptance criteria (Phase 1 Development)

1. Pipeline reads from the Revit 2025 model and emits an AutoCAD-ready package for the wall mark.
2. Output includes the **three-sheet** information architecture (TIF, BIF finish, sections + BOM).
3. Dimensioning and graphical standards are **closely aligned** with the golden DWG/PDF (via DWG standards bridge).
4. BOM / weight / concrete summary fields are populated from model data where available.
5. Residual work is plausible as **10–15%** AutoCAD cleanup (not missing whole sheets or core views).
6. Unexpected conditions are **flagged** rather than silently wrong.
7. Implementation is modular enough to extend to other wall conditions / later element types.
8. GMP can decide Phase 2 go/no-go from the demo.

---

## 10. Open questions

See **`docs/PHASE1_PROPOSAL.md` §10** (clarification questions for GMP / PH Revit team). Highlights:

- BOM part-number parameter mapping (RSX, PAS, INS, …)
- TIF/BIF/SIF face storage; dim origin / stub-dim convention
- Finish-code legend; insulation-reduce / field-only as params vs rules
- Notch parametric?; existing view templates from internal ~50% experiment
- Title-block personnel fields; DWG packaging; Phase 2 priority order

Stack preference is settled by meeting: **C# Revit API primary**.

---

## 11. Workspace map (for implementation)

```
docs/           requirements, meeting summary, proposal, abbreviations
samples/        LOCKED originals (RVT, PDF, DWG) — never delete
references/     meeting screenshots + PDF page renders
src/revit/      C# Revit API wall module
src/autocad/    DWG standards bridge
src/shared/     schemas, BOM models, style constants
deliverables/   generated outputs / working copies only
```
