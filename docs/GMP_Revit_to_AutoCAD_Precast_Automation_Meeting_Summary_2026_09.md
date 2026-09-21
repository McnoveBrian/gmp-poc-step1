# Meeting Summary — Revit-to-AutoCAD Precast Shop Ticket Automation

**Company:** GMP Architecture LLC  
**Project:** Revit Precast Shop Ticket / Drafting Automation  
**Date:** September 18, 2026  
**Participants:** Greg, Marchin Sikorsky, Brian (AEC Automation Engineer)  
**Primary Technology:** Autodesk Revit 2025, AutoCAD, C#/.NET, Revit API, Python/Dynamo where appropriate

---

## 1. Meeting Purpose

The meeting focused on discussing a potential automation solution for GMP Architecture's precast concrete drafting workflow.

GMP currently develops precast concrete models and erection-related information in Autodesk Revit. The next production stage—creating detailed fabrication/shop tickets—is more manual and ultimately moves into AutoCAD, where existing AutoCAD drafters perform final drafting cleanup and prepare PDFs.

The main objective is therefore to create a **Revit automation system that can extract the information already available in the Revit model, generate the required shop-ticket documentation, and export a highly complete drawing package into AutoCAD**.

The expected workflow is not necessarily 100% automated. GMP considers a result requiring approximately **10–15% final manual drafting/cleanup** acceptable if the majority of repetitive production work can be automated reliably.

---

## 2. Current GMP Workflow

GMP primarily works in the **precast concrete industry**.

The team currently creates precast building models in Revit. Different precast components require fabrication/shop-ticket documentation. During the meeting, walls were used as the primary example, while other element categories such as beams, columns, slabs, and additional precast component types were also mentioned.

The final shop-ticket package contains significantly more information than a simple exported model view. It may include multiple views and sheets containing fabrication-specific information.

The examples shown during the meeting included a completed PDF shop ticket and its corresponding AutoCAD drawing. These examples represent the quality and formatting level that GMP's client expects.

---

## 3. Desired Future Workflow

The desired high-level workflow is:

**Revit Model → Automation Tool → Generated Shop Ticket Documentation → DWG Export → AutoCAD Final Cleanup → PDF**

Revit should remain the main source of geometric and BIM information.

Rather than recreating information manually in AutoCAD, the proposed system should extract as much information as possible directly from the Revit model and use predefined drafting rules to generate the shop-ticket package.

The AutoCAD drafting team would then receive a substantially completed DWG and focus on final checking, minor adjustments, presentation cleanup, and PDF production.

This would allow GMP's existing AutoCAD-focused drafters to continue working with the software they already know while substantially reducing repetitive drafting work upstream.

---

## 4. Information Expected to Come from Revit

Based on the sample drawings and discussion, the automation may need to extract or derive information including:

- Precast element geometry and dimensions
- Openings and penetrations
- Reinforcement/rebar information where modeled
- Embeds and connection components
- Element marks and identification data
- Precast component parameters
- Material and production information
- Element quantities
- Part quantities
- Weights
- Architectural features
- Required fabrication metadata
- Information needed for the Bill of Materials
- Information required to generate views, sections, and details

A key part of the initial investigation will be determining **which of these items already exist as structured Revit data and which require additional calculation or drafting rules**.

---

## 5. Required Drawing Automation

The target system should potentially automate creation of the documentation normally prepared manually for each precast element.

For a wall panel, for example, this may include the primary elevation plus:

- Top views
- Side views
- Sections
- Details
- Dimensions
- Annotations
- Fabrication information
- Bill of Materials
- Production notes
- Element identification information

Different precast component types will require different drafting rules.

The preferred architecture is therefore not one large hard-coded drawing script. Instead, the system should use a **rule-based and modular approach**, where each element category has its own drawing-generation rules while sharing common services for dimensions, annotation, data extraction, exporting, and drawing standards.

A possible conceptual workflow is:

**Element Selection → BIM Data Extraction → Element Classification → View Generation → Dimension/Annotation Rules → BOM/Data Generation → Sheet Composition → DWG Export**

---

## 6. AutoCAD Output Requirements

The client emphasized that producing technically correct geometry alone will not be sufficient.

The external/client-facing shop tickets already follow established drafting standards, and the client's acceptance of the automated output will depend heavily on maintaining those standards.

The automation therefore needs to reproduce the existing AutoCAD presentation as closely as reasonably possible, including:

- Dimension styles
- Text styles and fonts
- Layers
- Colors
- Line types
- Line weights
- Blocks
- Symbols
- Annotation appearance
- View organization
- Drawing arrangement
- Sheet composition
- General graphical conventions

The target is for the exported DWG to look familiar to the current drafting team and, more importantly, to the final client.

Because the client is particular about drawing appearance, comparison against the existing approved DWG/PDF will be a major part of the initial technical analysis.

---

## 7. Proposed Technical Direction

Brian proposed using **C#/.NET and the Autodesk Revit API as the primary development technology**.

Python and Dynamo may also be useful for selected tasks, experimentation, utilities, or supporting workflows, but the main production automation is expected to be based around the Revit API because of the level of control required.

The initial technical concept is to treat Revit as the source of truth and programmatically:

1. Extract element geometry and parameters.
2. Identify the type of precast component.
3. Apply component-specific rules.
4. Generate appropriate Revit views and sections.
5. Generate dimensions and annotations.
6. Populate drawing information and BOM data.
7. Organize documentation.
8. Export the resulting content to DWG according to GMP's AutoCAD standards.

A key engineering principle will be avoiding unnecessary duplication of information. If data already exists reliably in the Revit model, the automation should retrieve it rather than requiring the drafter to enter it again.

---

## 8. Recommended Development Strategy

Both sides aligned around a **small, controlled first step rather than attempting to automate every precast element immediately**.

The recommended starting point is one representative wall panel for which GMP can provide the complete workflow:

**Revit source model + existing AutoCAD shop ticket + final PDF**

This example would serve as the reference case.

Brian would compare the same element across all three formats to understand where every drawing item originates and determine how closely it can be reproduced automatically.

The first implementation should prove the complete workflow for that representative wall before expanding the system to other wall conditions and eventually other precast element categories.

This approach reduces technical risk and allows GMP to evaluate actual output quality before committing to a broader automation effort.

---

## 9. Initial Analysis / Step One

For the first stage, Brian will independently review the provided materials.

The objective of this stage is not yet to build the entire system. It is to understand the production workflow sufficiently to provide a technically informed scope.

The initial review should establish:

- What can be automated reliably
- What information exists directly in Revit
- What information must be calculated
- What drafting rules need to be encoded
- What may still require manual work
- How the DWG output should be structured
- What special cases exist
- What level of effort is required

After completing this analysis, Brian will provide GMP with a proposed technical approach together with a realistic development timeframe and price.

No final development estimate was committed to during the meeting because the actual Revit model and corresponding reference drawings need to be reviewed first.

---

## 10. Revit Version

GMP confirmed that the current model is in **Autodesk Revit 2025**.

For the initial development and analysis, Brian recommended staying with Revit 2025 rather than upgrading or converting the model.

This avoids unnecessary compatibility issues and allows the automation to be tested against exactly the same environment used by the production model.

---

## 11. Collaboration with GMP's Revit Team

GMP explained that its owners are primarily engineers and do not intend to provide all detailed Revit implementation information themselves.

GMP also has a Revit/drafting team located in the **Philippines**, which is advantageous because Brian is also based in the Philippines and can communicate with that team during overlapping working hours.

The agreed preferred process is:

**Brian first reviews the sample independently. After the initial analysis, GMP's Revit team can become involved for detailed workflow clarification.**

The Revit team can provide practical information regarding:

- Modeling conventions
- Family structures
- Parameters
- Drafting practices
- Existing templates
- Project-specific standards
- Unusual project conditions

Screen-sharing sessions can also be used when necessary to walk through specific Revit workflows directly.

This should reduce communication delays and allow technical questions to be resolved by the people most familiar with the models.

---

## 12. Communication Approach

The project can work effectively across the time-zone difference between GMP's owners and Brian.

The meeting indicated approximately a 13-hour difference between some participants, while GMP's Philippines-based Revit team should have significantly easier real-time collaboration with Brian.

Routine questions can therefore be handled asynchronously through email or messaging.

When Brian encounters a business, engineering, production, or drafting rule that cannot safely be inferred from the model, he should **ask GMP rather than make assumptions**.

Questions can be grouped and sent to Greg or the appropriate GMP team member, with the Philippines Revit team participating where appropriate.

---

## 13. Key Project Principle

An important point agreed during the meeting is that **100% automation is not required for the project to be successful**.

The goal is instead to automate the high-volume, repetitive, rule-based part of the process so that an AutoCAD drafter receives a nearly finished drawing.

An acceptable target discussed was approximately:

**85–90% automated production + 10–15% human drafting/checking/cleanup**

This is particularly important because some precast drawings may contain exceptions, unusual geometry, project-specific notes, or presentation adjustments that are faster and safer for an experienced drafter to finalize manually than to encode into increasingly complex software rules.

---

## 14. Important Technical Considerations Identified

Several items will require special attention during the technical review.

### 14.1 Data Availability

The automation depends heavily on how consistently the required fabrication information is stored inside the Revit model.

### 14.2 Modeling Consistency

Automation works best when families, parameters, naming conventions, and model structures follow predictable standards.

### 14.3 Drawing-Standard Reproduction

Matching existing dimension styles, text, annotation, blocks, layers, and layouts may require both Revit configuration and controlled DWG post-export behavior.

### 14.4 Element-Specific Logic

Walls, beams, columns, slabs, and other precast elements should not be assumed to follow identical documentation rules.

### 14.5 Exception Handling

When an element does not meet expected conditions, the automation should ideally flag it for manual review rather than generating an incorrect shop ticket.

---

## 15. Potential Long-Term Architecture

Although the immediate goal is automating the example wall workflow, the discussion suggests the project has the potential to become a broader internal **precast documentation automation platform**.

A scalable architecture could eventually support separate modules for:

- Wall panels
- Beams
- Columns
- Slabs
- Other precast component families
- BOM generation
- Drawing-rule configuration
- Annotation standards
- DWG export configuration
- Validation
- Batch production
- Exception reporting
- Production QA/QC

This was not committed as the immediate scope, but designing the first wall automation in a modular manner would make future expansion much easier than building a one-off script.

---

## 16. Agreed Next Steps

| Owner | Action | Purpose |
|---|---|---|
| **Greg / GMP** | Send Brian a link to the project folder | Provide source material for technical evaluation |
| **GMP** | Include the **Revit 2025 model** | Primary BIM/data source |
| **GMP** | Include the corresponding **AutoCAD DWG** | Reference for drafting standards and final editable output |
| **GMP** | Include the corresponding **final PDF shop ticket** | Reference for approved/client-facing output |
| **Brian** | Compare the Revit model, DWG, and PDF for the same representative precast element | Trace data and drawing-generation requirements |
| **Brian** | Identify automatable versus manual portions | Define realistic automation coverage |
| **Brian** | Review dimension styles, fonts/text, layers, blocks, views, sections, BOM, and graphical standards | Determine how closely existing output can be reproduced |
| **Brian** | Prepare technical approach | Explain proposed Revit API/DWG workflow |
| **Brian** | Prepare realistic development timeframe | Based on actual model complexity rather than assumptions |
| **Brian** | Prepare pricing estimate | Support GMP's decision on whether to proceed |
| **Brian** | Compile clarification questions | Avoid incorrect assumptions about production/engineering rules |
| **GMP Revit Team + Brian** | Collaborate after initial review as needed | Resolve detailed Revit/modeling questions efficiently |

---

## 17. Success Criteria for the Initial Phase

The initial phase should be considered successful if Brian can demonstrate that a representative precast wall can be transformed from the existing Revit model into a highly complete shop-ticket package that:

- Closely follows the current client-approved visual standard
- Includes the required views and fabrication information
- Uses reliable dimensions and annotations
- Includes or supports Bill of Materials information
- Exports cleanly into AutoCAD
- Requires only limited final drafting cleanup
- Can serve as a reusable foundation for additional precast element types

The key success measure is practical production value rather than theoretical automation coverage.

---

## 18. Overall Meeting Outcome

The meeting established strong alignment around the project objective.

GMP is looking for a developer who understands both **Revit/BIM workflows and software development**, rather than simply a programmer unfamiliar with AEC production or a Revit drafter without API-development experience.

The proposed first step is intentionally low risk: evaluate one real, complete wall-panel example and determine whether the existing Revit information can be transformed into an AutoCAD shop ticket that closely matches GMP's current accepted output.

The immediate success criterion is therefore **not a theoretical demonstration**, but proving that a representative Revit precast element can be converted into a highly complete, standards-compliant shop-ticket package that leaves only limited manual AutoCAD finishing work.

Once Brian receives and analyzes the **Revit 2025 model, matching DWG, and final PDF**, he will be able to provide GMP with:

- Technical feasibility assessment
- Proposed implementation approach
- Expected automation level
- Development timeframe
- Cost estimate
- Clarification questions
- Recommended next phase

---

## 19. Concise Decision Summary

**Agreed direction:** Proceed with a technical evaluation using one representative wall-panel example.

**Inputs required from GMP:**
- Revit 2025 model
- Matching AutoCAD DWG
- Final PDF shop ticket

**Brian's next deliverables:**
- Feasibility assessment
- Automation scope
- Technical approach
- Time estimate
- Price estimate
- Questions requiring GMP/Revit-team clarification

**Target workflow:**  
**Revit → Automated Shop Ticket Generation → DWG → 10–15% AutoCAD Cleanup → PDF**

**Long-term opportunity:** Expand the wall automation into a modular precast documentation platform covering additional precast element types and production workflows.
