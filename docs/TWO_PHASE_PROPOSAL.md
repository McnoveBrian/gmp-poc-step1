# Two-Phase Proposal  
## Precast Wall Shop Ticket Automation (Revit → AutoCAD)

**To:** GMP Architecture LLC (Greg, Marcin)  
**From:** Brian  
**Date:** 3 October 2026  
**Re:** Detailed proposal following our meeting on 1 October 2026 and the working POC  

**Reference samples (acceptance baseline):**  
- AFIP-1035 (Pilgrim’s RTE, Project 45430) — Revit 2025 model, matching AutoCAD DWG, and final PDF  
- Additional approved Tindall wall tickets you provide as acceptance examples  

**Panel class in scope:** rectangular insulated / solid wall panels (e.g. `WALL_PANEL_INSULATED_VERTICAL` and agreed similar types)  

---

## 1. Summary

GMP needs wall-panel shop tickets that are **technically correct** and that **look like the approved Tindall examples**. Your downstream client is exacting: dimension styles, text, hatch, layers, title blocks, and sheet composition are part of the acceptance criteria — not a cosmetic preference.

We already have a working proof-of-concept: select a wall panel in Revit → extract geometry and parameters → generate a ticket drawing natively in AutoCAD from a copy of your seed DWG → plot PDF. The POC proves the pipeline. The remaining work is to close the gap to production drafting quality.

This proposal splits the work into two phases:

| Phase | Focus | Fixed price | Timeframe |
|-------|--------|-------------|-----------|
| **Phase 1** | Core wall ticket **without** reinforcement automation | **$5,000** | **2–3 weeks** |
| **Phase 2** | Rule-based steel / reinforcement on the ticket (optional add-on) | **$3,000** | **1–2 weeks** |
| **Both** | Phase 1 + Phase 2 | **$8,000** | **~3–5 weeks** |

**Recommendation:** Start with Phase 1. Your team uses the automated ticket and adds reinforcement by hand. After that workflow is proven in practice, decide whether Phase 2 adds enough value to justify the second investment.

Budgets and timeframes are negotiable depending on final agreed scope, number of wall conditions, and milestone structure.

---

## 2. Agreed technical approach

### 2.1 Why AutoCAD is the drawing host

The goal is not a different-looking automatic ticket. The goal is to reproduce the **Tindall ticket standard** as closely as practical.

Generating the final drawing **natively in AutoCAD** (from a copy of your approved seed DWG) reuses your actual:

- Title blocks and layouts  
- Layers, linetypes, and plot styles  
- Dimension styles and text styles  
- Blocks and symbols  

rather than inventing a parallel look in Revit and hoping export matches.

### 2.2 What Revit contributes

Revit remains the source of truth for the panel:

- Panel identity (mark / type)  
- Overall geometry, thickness, orientation  
- Openings, recesses, pockets, corbels, reveals (as modeled or parameterized)  
- Embedded parts and locations (as available in the model)  
- Parameters needed for notes, BOM, and title-block fields  

### 2.3 What stays out of the Revit model

**Reinforcement (bars, strands, mesh) will not be modeled as 3D rebar in the production Revit model.** That would inflate model weight and slow daily work. If reinforcement is automated later (Phase 2), it is drawn as **2D fabrication graphics on the AutoCAD ticket** from agreed engineering rules.

### 2.4 Sections and details

Sections on the ticket are **not live Revit section views** pasted into AutoCAD. They are generated programmatically from data extracted from Revit. When you regenerate a ticket after the model changes (e.g. thickness 9″ → 10″), the section content can update. Condition-specific section logic (openings, pockets, embeds, insulation changes) is part of Phase 1 build-out.

### 2.5 Target operating quality

| Metric | Target |
|--------|--------|
| Automation completeness (Phase 1) | ~**85–90%** of drafting for covered wall conditions |
| Manual finish | ~**10–15%** AutoCAD review, cleanup, and (Phase 1) reinforcement |
| Visual standard | As close as practical to approved Tindall examples |
| Wrong quiet output | Prefer **flags / review notes** over guessing |

100% automation is not required for this to be useful.

---

## 3. Current POC vs production gap

The POC already demonstrates the end-to-end path. Relative to your approved tickets and the demo you shared on 1 October, the main gaps to close in Phase 1 are:

1. **Dimension and text presentation** matching house / Tindall styles  
2. **Client SHX fonts** and plot configuration (e.g. Final Print CTB) so plotted text and lineweights match  
3. **Condition-specific sections and details** (openings, pockets, corbels, embeds, insulation transitions)  
4. **Finish / reveal / chamfer graphics** and related callouts as on the reference sheets  
5. **BOM completeness and rules** (part numbers, quantities, concrete / weight / finish summary)  
6. **Broader wall-condition coverage** beyond the single POC panel  

Phase 1 treats the visual and drafting standard as the definition of done for the core ticket.

---

## 4. Phase 1 — Wall ticket without reinforcement

**Fixed price:** $5,000  
**Duration:** 2–3 weeks after kickoff and receipt of agreed acceptance samples  

### 4.1 Objective

Deliver a production-usable wall shop-ticket workflow: from one selected wall panel in Revit to AutoCAD DWG + PDF sheets that a drafter can review, add reinforcement by hand, and issue with limited cleanup — looking as close as practical to your approved Tindall examples.

### 4.2 In scope

| Area | Deliverable |
|------|-------------|
| **Geometry** | Panel outline, thickness, mark, orientation (TIF / BIF / SIF presentation as applicable) |
| **Openings** | Through openings and agreed recess types from the model |
| **Embeds** | Embedded parts and locations when present in Revit data |
| **Edge / form features** | Pockets, corbels, reveals, chamfers, solid zones as agreed from samples |
| **Views** | TIF, BIF, and related side / end presentation consistent with the reference package |
| **Dimensions & callouts** | Required tags and dimensions using your AutoCAD dim / text styles |
| **Sections & details** | Condition-driven section set regenerated from Revit-extracted data |
| **Sheet layouts** | Multi-sheet package aligned to the reference structure (typically three sheets) |
| **BOM** | Parts list and fabrication summary fields supported by available model data / rules |
| **Title block** | Project / mark / sheet / personnel / date fields as agreed |
| **Standards** | Layers, blocks, dim styles, text styles, hatching presentation from seed DWG |
| **Output** | Editable DWG + plotted PDF under the platform export path |
| **Drafter hand-off** | Clear workflow for review, cleanup, and **manual reinforcement** |

### 4.3 Out of scope (Phase 1)

- Automatic reinforcement, strands, mesh, or trim-bar placement  
- Modeling rebar as 3D elements in Revit  
- Non-rectangular panel shapes (unless explicitly added by change order)  
- Beams, columns, slabs / double tees  
- Batch processing of an entire building  
- Full coverage of every rare one-off engineering exception (those are flagged for manual work)

### 4.4 Suggested milestones

| Milestone | Timing | Outcome |
|-----------|--------|---------|
| **M0 — Kickoff** | Day 0 | Written scope signed; acceptance tickets confirmed; seed DWG / fonts / CTB paths set |
| **M1 — Style lock** | End of week 1 | Sheet 1 (or equivalent) style sample against approved example; dim / text / hatch review |
| **M2 — Full ticket** | Mid Phase 1 | Three-sheet package for the primary acceptance panel (e.g. AFIP-1035) |
| **M3 — Condition sweep** | Late Phase 1 | Agreed set of additional conditions (openings / embeds / pockets / corbels) |
| **M4 — Handover** | End of Phase 1 | Installable add-in build, short usage notes, known-limitation list, cleanup checklist |

### 4.5 Success criteria (Phase 1)

Phase 1 is accepted when, for the agreed reference panel(s):

1. One-button (or ribbon) generation produces the shop-ticket DWG + PDFs from Revit selection.  
2. Geometry, openings, embeds, dimensions, sections, BOM, and title-block content match the agreed rules.  
3. Visual presentation is **as close as practical** to the approved Tindall acceptance examples (style treated as a requirement).  
4. Drafter reinforcement-by-hand workflow is documented and usable.  
5. Unknown or unsupported conditions are flagged rather than silently invented.  
6. Remaining AutoCAD work for covered cases is in the ~10–15% cleanup band (excluding reinforcement drafting, which remains manual by design).

### 4.6 Assumptions

- Timely answers from GMP (within a few business days) on drafting conventions and exceptions.  
- Revit **2025** and AutoCAD **2025** (accoreconsole) available for development and demo.  
- AFIP-1035 (and any extra tickets you supply) remain the visual / content acceptance baseline.  
- Seed DWG, font map, and plot style (CTB) are available on the build machine.  
- Scope stays locked to rectangular wall panels and the condition list agreed at kickoff.

---

## 5. Phase 2 — Steel / reinforcement automation (optional)

**Fixed price:** $3,000 (add-on after Phase 1)  
**Duration:** 1–2 weeks after Phase 1 acceptance and receipt of engineering rules  

### 5.1 Objective

Add a **rule-based reinforcement module** that draws fabrication reinforcement on the AutoCAD shop ticket from panel geometry, openings, parameters, and rules your engineering team provides — **without** placing 3D rebar in the Revit model.

### 5.2 In scope

Depending on the rules you provide, Phase 2 can include:

- Bar sizes and lengths  
- Spacing and cover  
- Lap lengths  
- Reinforcement around openings  
- Trim bars  
- Mesh or strand requirements where rules are repeatable  
- Other agreed repeatable reinforcement conditions  

Output remains **2D ticket graphics** (and schedules/callouts as agreed), generated when the ticket is created or regenerated.

### 5.3 Out of scope (Phase 2)

- Full 3D rebar modeling inside Revit  
- Guaranteeing every special engineering case without a manual override path  
- Structural design / calculation of reinforcement (software applies **your** rules; it does not replace the engineer)  
- Expanding to beams / columns / slabs (separate future work)

### 5.4 Why Phase 2 is recommended as a second step

As discussed on 1 October:

- Reinforcement rules vary widely by condition.  
- Entering many one-off parameters per ticket can take longer than a drafter copying a proven pattern.  
- Phase 1 alone is already “good enough” for day-to-day value if the core ticket matches your standard.  

After your team uses Phase 1 in practice, we can judge whether automation of reinforcement pays for itself.

### 5.5 Success criteria (Phase 2)

1. Agreed rule set is documented and implemented for the covered conditions.  
2. Regenerating a ticket places the corresponding reinforcement graphics / callouts on the DWG.  
3. Revit model remains free of mandatory 3D rebar for this workflow.  
4. Unsupported or special cases are flagged for manual completion.  
5. Acceptance checked against the same visual standard as Phase 1, plus the reinforcement presentation on the reference tickets.

### 5.6 What I need from you for Phase 2

- Engineering logic per wall type / opening / edge condition (tables, markups, or worked examples).  
- Confirmation of which reinforcement is mandatory on the ticket vs optional.  
- Preference for how overrides work when a mark is non-standard (dialog, JSON parameters, or leave blank for drafter).

---

## 6. Commercial summary

| Item | Phase 1 | Phase 2 |
|------|---------|---------|
| **Scope** | Core wall ticket, no rebar automation | Rule-based rebar on ticket |
| **Fixed price** | **$5,000** | **$3,000** |
| **Timeframe** | **2–3 weeks** | **1–2 weeks** |
| **Combined** | **$8,000** · **~3–5 weeks** | |

Rates discussed previously ($25–$35/hour) informed these fixed figures. Fixed pricing keeps the commercial picture simple for your internal decision. If the agreed condition library expands significantly beyond the kickoff list, we discuss a change order before expanding work.

**Flexibility:** Budget, timeframe, and milestone payment structure are negotiable. I am interested in longer-term collaboration with GMP on walls and later other precast elements — not only a single isolated phase.

### Suggested payment structure (optional)

| Event | Amount |
|-------|--------|
| Phase 1 kickoff (scope signed) | 40% of Phase 1 ($2,000) |
| Phase 1 acceptance (M4) | 60% of Phase 1 ($3,000) |
| Phase 2 kickoff (if authorized) | 40% of Phase 2 ($1,200) |
| Phase 2 acceptance | 60% of Phase 2 ($1,800) |

Alternative structures (weekly, milestone-only, or single payment on delivery) are fine if you prefer.

---

## 7. Working process before development starts

Before coding against the production scope, I will prepare a **detailed written scope** covering:

1. Exactly what is included / excluded in the chosen phase(s)  
2. What remains manual (reinforcement in Phase 1; special cases always)  
3. The expected drawing / drafting standard  
4. The reference tickets used for acceptance  
5. Milestones, timeline, and fee  

Both sides approve that document; it becomes the shared target for what you are paying for and what I am delivering.

---

## 8. Helpful materials (optional, not required to proceed)

Additional practical examples accelerate correctness and reduce assumptions:

- Extra approved wall shop tickets (PDF / DWG)  
- Matching Revit + DWG + PDF sets for the same marks  
- Examples with different openings, pockets, corbels, reveals, embeds, or unusual conditions  
- Existing drafting or production standards  
- A short screen recording of how a drafter manually prepares or finishes a ticket  

The more representative the set, the better the recurring rules and exceptions can be captured.

---

## 9. Risks and how we manage them

| Risk | Mitigation |
|------|------------|
| Style mismatch vs picky client standard | Early M1 style lock; seed DWG + fonts + CTB; acceptance against real examples |
| Model missing fabrication graphics | Rules + AutoCAD drafting from parameters; flag gaps |
| Too many rare wall conditions | Lock Phase 1 condition list at kickoff; change order for expansion |
| Rebar rules incomplete or contradictory | Keep rebar in Phase 2; require written rules before build |
| Revit / AutoCAD file locks during build | Documented rebuild path; DeployAddin toggles; no edits to `samples/` originals |
| Scope creep into beams / columns | Explicitly Phase 1/2 out of scope; roadmap later |

---

## 10. After these phases

If Phase 1 (and optionally Phase 2) works in production, the same platform can grow to:

- More wall types and joint libraries  
- Beams, columns, slabs / double tees  
- Batch ticket runs and stronger QA flags  
- Shared standards / BOM / reporting across element types  

Phase 1 is built so expansion is an addition, not a rewrite.

---

## 11. Decision request

Please review the two options and let me know:

1. **Phase 1 only** ($5,000 / 2–3 weeks) — recommended first step  
2. **Phase 1 + Phase 2** ($8,000 / ~3–5 weeks) — if you already want reinforcement automation committed  
3. Any adjustments to scope, budget, schedule, or payment structure  

Once you decide, I will issue the detailed kickoff scope for signature and list the exact acceptance drawings. I am ready to start as soon as we are aligned in writing.

Best regards,  
Brian
