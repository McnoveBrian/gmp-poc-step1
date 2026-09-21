# Phase 1 Proposal  
## Precast Shop Ticket Automation (Revit to AutoCAD)

**To:** GMP Architecture LLC  
**From:** Brian  
**Date:** 21 September 2026  
**Re:** Proposal following our meeting on 18 September 2026  

**Reference sample:** AFIP-1035 (Pilgrim’s RTE, Project 45430) — Revit 2025 model, matching AutoCAD DWG, and final PDF  
**Panel in the model:** `WALL_PANEL_INSULATED_VERTICAL`, type D-70  

---

## 1. Summary

The goal of Phase 1 is to prove that we can take one real wall panel from your Revit model and produce a shop-ticket package that is close enough to your approved AFIP-1035 drawings for an AutoCAD drafter to finish with limited cleanup.

**Proposed workflow**

1. Select the wall panel in Revit.  
2. Read what we can from the model (geometry, openings, parameters, neighboring panels, joints, and any connection hardware that is already modeled).  
3. Create the required views and cross-sections.  
4. Add Detail Lines and related 2D drafting for connection assemblies and other fabrication graphics that are not fully built in the 3D family.  
5. Place dimensions, notes, and bill of materials information, and compose the sheets.  
6. Export to DWG and bring the drawing in line with your AutoCAD standards (dimension styles, text, layers, blocks, and so on).  
7. Your AutoCAD team completes the last **10–15%** (check, adjust, and PDF).

**Target quality:** about **85–90%** complete from automation, with **10–15%** left for AutoCAD. Full automation is not the goal, and is not needed for this to be useful.

**Tools:** C# and the Revit API on Revit 2025 as the main path. Python or Dynamo only where they help with smaller supporting tasks.

---

## 2. Where things stand today

Right now the building model lives in Revit, but shop tickets are still largely drafted by hand and finished in AutoCAD. Your team has already explored some automation, but day-to-day drafting leaves little time to push it further.

Phase 1 keeps Revit as the place where the panel is selected and most of the drawing is built, then hands a nearly finished DWG to the AutoCAD drafters you already have in place. That way we reduce repetitive work without forcing a change in how tickets are finalized and issued.

---

## 3. Important point about the model vs the PDF

The shop ticket should start from the wall panel marked in the model (D-70 insulated vertical panel for the AFIP-1035 example). That is the right element to drive the mark.

However, that family does **not** contain everything shown on the approved PDF. In particular, the PDF includes:

- Joint and connection details between the subject panel and adjacent walls  
- Connection components and callouts that may not all exist as 3D geometry  
- Cross-sections and detail graphics at specific cut locations  
- Finish, reveal, chamfer, and similar fabrication notes  
- Dimensioning and annotation laid out to your client’s standards  

So Phase 1 is not simply “cut a section through the family and export.” We need to generate the detail drawings so they cover the wall itself, its connections to neighboring walls, and the related connection hardware — then use **Detail Lines** (and similar 2D drafting in Revit) to fill in what the 3D model does not show.

In short:

- The model gives us identity, size, openings, and whatever embeds or parameters are already there.  
- Rules and Detail Lines give us the fabrication graphics the PDF expects.  
- AutoCAD export and a standards pass give us the look your client is used to seeing.

---

## 4. What the three AFIP-1035 sheets tell us we need

Using AFIP-1035 as the reference:

**Sheet 1 — Top-in-form (TIF)**  
Main fabrication layout, side view, mark end, Section S, Detail P, insert/rebar callouts, dimensions, production notes, and title block.

**Sheet 2 — Bottom finish (BIF), concrete removed**  
Finish layout, reveals and chamfers, opening edge conditions, finish tags, and the edge details (G through U on the sample).

**Sheet 3 — Sections and bill of materials**  
The section set driven by openings, recesses, pockets, fastening holes, corbels, solid zones, insulation changes, and joint conditions as applicable — plus the parts list, concrete strengths, weight, finish notes, and title block.

Some of this can come straight from the model. Some of it has to be placed by rule (especially where to cut sections). Some of it has to be drawn with Detail Lines after the views exist. We will sort that list carefully with your Revit team before building too far.

---

## 5. What will vary from panel to panel

For Phase 1 we will stick to **rectangular** panels and **rectangular** openings. Even within that, tickets change based on:

- Panel size, type (D-70), mark, sandwich build-up, orientation (TIF/BIF), concrete mixes, strength, weight, and finishes  
- Neighboring panels and joint type  
- Number, size, and location of openings (and whether they are through openings or recesses)  
- Edge conditions (reveals, chamfers, corbels, solid zones)  
- Embeds and connection hardware, including which face they belong to  
- Which sections and details are required for that mark  

Later phases can add other shapes and other element types (beams, columns, slabs). Those are outside Phase 1.

---

## 6. How I propose to build it

I recommend a rule-based add-in approach in C#, not a one-off script for a single drawing. The first release focuses on walls, but the structure should allow beams, columns, and slabs later without starting over.

Roughly, the tool will:

1. Let the user select the subject panel.  
2. Collect data from that panel and its neighbors/joints.  
3. Decide where sections and details should be created (based on rules we agree with you).  
4. Create the TIF, BIF, side, and end views, plus the needed sections and details.  
5. Draw connection and assembly information with Detail Lines where the 3D model is incomplete.  
6. Add dimensions, annotations, notes, and BOM content, and arrange the sheets.  
7. Export to DWG and align styles with your approved AutoCAD example.  
8. Flag odd or incomplete cases for manual review instead of producing a wrong ticket quietly.

I will send an early sample from Sheet 1 so you can judge dimension style and overall look before we finish Sheets 2 and 3.

---

## 7. Phase 1 scope and success criteria

Phase 1 is successful if, for the AFIP-1035 / D-70 example, we can:

- Produce the three-sheet shop-ticket structure with the main views, sections, notes, and BOM information  
- Include adjacent wall connections and connection components as agreed in the detailing rules  
- Place cross-sections according to rules we document with you  
- Apply automated Detail Line work after those views are created  
- Export a usable DWG that follows your standards closely enough for production review  
- Leave only about **10–15%** for AutoCAD cleanup  
- Demonstrate at least three rectangular opening arrangements, plus at least one joint/connection case  
- Flag unknown joint or data conditions rather than guessing  

---

## 8. Schedule and fee

**Duration:** about **4–5 weeks** after we start.

| Week | Focus |
|------|--------|
| 1 | Review the family and joint data with your team; agree section-placement rules; deliver an early Sheet 1 / style sample |
| 2–3 | Sheets 2 and 3, Detail Line connection drafting, BOM, section rules |
| 4 | Additional opening and joint cases; tighten the DWG standards pass |
| 5 | Buffer, review against AFIP-1035, list remaining cleanup items, outline Phase 2 |

If the family mapping and connection rules are clear, we may finish closer to 3½ weeks. If the joint-detail library is large or data is incomplete, it may run closer to 6 weeks.

**Rate:** $25/hour  

| Case | Hours | Fee |
|------|-------|-----|
| Straightforward | 140–160 | $3,500–$4,000 |
| **Expected** | **170–200** | **$4,250–$5,000** |
| Extra (if needed) | +30–40 | +$750–$1,000 |

I suggest we plan on a fixed Phase 1 fee of about **$4,500–$5,000**. If the connection Detail Line rules turn out much larger than the AFIP-1035 set, or if the model cannot support the BOM part numbers we need, we would discuss a change order before expanding the work.

Assumptions: timely answers from GMP and the Philippines Revit team (within a few business days), Revit 2025 for this sample, and your confirmation of section-placement and connection drafting rules.

---

## 9. Risks I want to be upfront about

- The 3D panel will not equal the finished detail by itself — Detail Lines are required.  
- We need clear agreement on **where** sections are cut before that part of the tool is reliable.  
- Joint and connection rules need to be written down; unknown conditions should go to a drafter.  
- Matching your AutoCAD look takes an early style check, not a late surprise.  
- Phase 1 should stay locked to the AFIP-1035 wall and its joint condition so we do not try to solve every wall type at once.

---

## 10. How we work together

I will keep day-to-day questions short and practical, by email or message. For modeling conventions, families, and how you detail joints today, I will work with your Philippines Revit team where that is easier for timezone and technical depth. For production or engineering rules that are not obvious from the model, I will ask rather than assume.

Screen sharing is fine when a walkthrough helps.

---

## 11. Questions I need answered to build this correctly

**About the panel and data**

1. For AFIP-1035 / D-70, where are the BOM part numbers stored (RSX, PAS, INS, and similar)?  
2. How do you store TIF / BIF / SIF face information?  
3. What is the dimension origin convention (mark end, stub direction, start/stop panel)?  
4. What do the numeric finish tags and “AS” mean on the drawings?  
5. Are notes such as “reduce insulation” or “not for stripping / field only” parameters in the model, or drafting standards only?  
6. Is the bottom notch on AFIP-1035 a normal family parameter, or a special case?

**About sections, joints, and Detail Lines**

7. Exactly where should cross-sections be created for a wall mark (every opening, every joint, corbels, solid zones, only when a named detail applies, or another rule)?  
8. On joint details, how much of the adjacent wall should show (clip distance / overlap)?  
9. When connection hardware is not modeled in 3D, what should appear on the ticket, and in what graphic form (Detail Lines, detail components, or CAD blocks after export)?  
10. Do you already have a library of typical joint or connection details we should reuse?  
11. On AFIP-1035 Sheet 3, which sections are mostly live model cuts, and which are mostly drafted overlays?

**About process**

12. Are there existing view templates or export setups from your earlier automation trials?  
13. How should title-block names (Drawn / Checked / Project Lead / Engineer) be filled?  
14. Prefer one DWG with multiple layouts, or one file per sheet?  
15. After this wall proof, what should come next — more wall conditions, or columns and beams?

---

## 12. What you will receive

1. A clear list of which panel and joint data maps to which drawing content  
2. Written section-placement and Detail Line rules, agreed with you  
3. A Revit add-in workflow: select the mark, generate the sheets (views, sections, Detail Line work)  
4. Demonstrations for at least three opening arrangements and one joint/connection case  
5. The exported DWG after the standards pass  
6. A short list of what remains for the 10–15% AutoCAD cleanup  
7. Notes on how odd cases are flagged  
8. A brief recommendation for Phase 2  

---

## 13. What I need from you to proceed

1. Confirmation that Phase 1 covers one representative wall (AFIP-1035 / D-70), including adjacent connections and Detail Line drafting, at roughly 85–90% automation / 10–15% cleanup.  
2. Confirmation of the fee range (**about $4,500–$5,000** at $25/hour over 4–5 weeks), or any adjustment you want.  
3. Access to your Philippines Revit team for the questions above — especially section placement and connection detailing.  
4. Confirmation that AFIP-1035 (Revit 2025 model, DWG, and PDF) remains the acceptance sample.

---

## 14. After Phase 1

If the wall ticket works well, the same approach can grow to more wall conditions and joint types, then to beams, columns, and slabs, with shared tools for BOM, standards, checking, and eventually batch runs. Phase 1 will be built so that expansion is an addition, not a rebuild.

---

Please let me know if you would like any part of the scope or fee adjusted before we start. I am ready to begin with the family review and section-placement discussion as soon as you give the go-ahead.

Best regards,  
Brian
