# Client wants — meeting 2026-10-01 (~35 min)

Source: [`meeting 1001.mp3`](meeting%201001.mp3) · Full script: [`meeting 1001.transcript.md`](meeting%201001.transcript.md)

Participants (from audio): Greg (GMP), Marcin (GMP), Brian (dev). Client ticket standard referenced as **Tindall**.

---

## Verdict (what they are buying)

**Style fidelity is the acceptance gate.** Correct data is necessary but not enough — tickets must look like the approved Tindall example (dimensions, text, hatch, title blocks, layers). Their downstream client is “very picky.” Greg already proved the style is achievable in AutoCAD himself.

Preferred first delivery: **wall tickets without reinforcement automation.** Rebar later (or forever manual).

---

## Phase 1 — walls, no rebar (what they want first)

| Include | Notes |
|---|---|
| Panel geometry from Revit | Outline / thickness / mark |
| Openings | Recognized and drawn |
| Embeds / plates / pockets / corbels | Condition coverage matters for estimate |
| Dimensions | Same dim styles as approved ticket |
| Views | Top / side / bottom-form style presentation |
| Sections + form details | Regenerated from Revit data (not live Revit views; not static copy-paste) |
| BOM / tables | Where the example shows them |
| Title blocks, layers, text styles | From seed DWG / house standard |
| Match approved drafting look | **Acceptance requirement**, not preference |

| Out of Phase 1 | Notes |
|---|---|
| Reinforcement automation | Drafters place rebar by hand on the ticket |
| Modeling rebar/strands/mesh in Revit | Explicitly unwanted — blows up model size |

Greg: even a module like his screen demo **without reinforcement would already be “pretty satisfactory / good enough.”**

---

## Phase 2 — rebar (optional later)

- Prefer **rules → 2D graphics in AutoCAD**, not 3D rebar in the Revit production model.
- Needs GMP engineering rules (bar size, cover, spacing, opening trim, laps, etc.).
- Client concern: many one-off conditions; per-ticket parameter entry may be slower than copy-paste by hand — so defer until Phase 1 proves value.

---

## Commercial / process (what they asked for)

- Budget matters more than speed; a few weeks either way is fine.
- Numbers discussed on the call (negotiable): Phase 1 ~2–3 weeks / ~$2.5–3k; rebar phase ~1–2 weeks / ~$1.5–3k.
- Before kickoff they want:
  1. Example drawings as **final acceptance samples**
  2. Written scope email: in / out / deliverables / timeline / cost
  3. Mutual approval → then build
- They will internalize and reply **sometime next week** (likely Phase 1 without rebar).

---

## Gaps called out vs current POC

1. Dimension / text style still not matching their example  
2. Missing client SHX fonts / Final Print CTB (and similar plot standards)  
3. Condition-specific sections (openings, pockets, embeds) not fully built  
4. Finish details / BOM rules still thin  
5. Visual standard must be treated as the definition of done
