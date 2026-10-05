# Reply to Greg — price range and Revit version compatibility

**To:** Greg  
**Cc:** Marcin  
**Subject:** Re: Shop ticket automation — price and Revit version compatibility  
**Attachment:** `TWO_PHASE_PROPOSAL.md` (updated 6 October; new section 4 on versions)

---

Hi Greg,

Thank you, and no rush at all. I understand you are busy. Answers to both points below.

**1. Price: $3,000–$5,000 vs $5,000**

You are right that we first talked about a $3,000–$5,000 range. The $5,000 is the top of that range because the full Phase 1 now covers the whole condition list (openings, pockets, corbels, embeds, edge details), and it includes the version support described below, so that is not a later extra.

If you would rather start lower, I can do a **reduced Phase 1 for $3,500 (about 2 weeks)**: the same pipeline, the same Tindall style lock and installer, for the primary panel type (the AFIP-1035 family: insulated rectangular panel with openings, embeds, standard sections, BOM and title block). The wider condition sweep would follow later. Nothing from the reduced phase is thrown away; it all carries forward.

**2. Version compatibility (moving to a newer Revit)**

I agree this needs to be settled at the very beginning, so I have already built and tested it before we start:

- **One codebase, a build per Revit year.** The same source code now produces the add-in for **Revit 2024, 2025 and 2026**. On my machine it loads and runs in Revit 2024 and Revit 2025; 2026 builds cleanly and will be run-tested on the first machine that has it.
- **Side by side.** One installer puts the right build into every Revit year installed on a workstation. Someone can work on a 2024 project in Revit 2024 and a 2025 project in Revit 2025 on the same day, both with the add-in.
- **Only a thin layer depends on Revit.** The Revit part only reads the panel and writes a data file. The drawing itself is made in AutoCAD from that file, so your standards, sections and BOM logic do not change when Revit changes. The AutoCAD part works with **AutoCAD 2025 and 2026** as one build.
- **Moving to a new year** (for example Revit 2027 when a project needs it): add that year's build, fix any Autodesk API changes, test on one of your models saved in that year, and send an updated installer. Typically **1–3 working days**, and earlier years stay supported. Revit/AutoCAD 2027 move to a newer Microsoft .NET, which we know about in advance and plan for.
- **Your project files are never upgraded by the add-in.** It always follows the Revit year of the project.

Supporting Revit 2024–2026 and AutoCAD 2025–2026 is included in the Phase 1 price. Each later Revit year would be a small fixed fee ($300–$600 depending on Autodesk's changes), agreed when you decide to move. An annual maintenance arrangement is also possible if you prefer.

One practical note: Revit shows a security prompt for add-ins that are not digitally signed, and shows it again after each update. A code-signing certificate (roughly $200–$400 per year) removes that. Worth doing before the whole team uses it, but not needed to start.

**To plan this properly, could you tell me:**

1. Which Revit and AutoCAD years your projects use **today**, and which you expect to move to **next**?
2. Roughly how many workstations would run the add-in?
3. Does GMP already have a code-signing certificate?

The updated proposal (attached) has a new section 4 with the details. Once you choose the full or reduced Phase 1, I will send the short written scope for sign-off and we can start.

Best regards,  
Brian
