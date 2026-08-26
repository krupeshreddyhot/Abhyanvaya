# AI29.1D.24B.4A.1 Prompt 2 — Controlled Capacity Validation Data (Retest)

**Date:** 2026-08-16  
**STATUS:** **AVAILABLE** (Architect-requested retest)

## Decision

User requested creation of **CA - B Section** with capacity **50** for the exact Band 60 / Capacity 50 gate.

Live Allocation Context (AY=1 / Course=1 / Group=2 / Semester=3):

| SectionId | Code | Name | MaximumCapacity | Classification |
|-----------|------|------|----------------:|----------------|
| 3 | SCCA01 | CA  - A Section | 60 | Live |
| 4 | CA-A | Computer Applications A | 60 | Live CA III |
| 5 | CA-B | Computer Applications B | 60 | Live CA III |
| 8 | SCCA02 | **CA - B Section** | **50** | Controlled capacity validation section |

**Create action:** `ALREADY_EXISTS` — section id=8 was already present with `maximumStrength=50`; no additional create/mutation required for this retest.

## Manifest

| Field | Value |
|-------|-------|
| tenant / college | uni `001` / college `1053` |
| academic year | 1 |
| course | 1 |
| group | 2 |
| semester | 3 |
| section | SCCA02 / CA - B Section (id=8) |
| original capacity | 50 (pre-existing) |
| temporary capacity | n/a — already 50 |
| reason | Exact Band 60 / Capacity 50 validation |
| restoration procedure | **N/A** — capacity not temporarily mutated; leave section as created |

## Student / hierarchy / timetable / attendance

**Unchanged.** No StudentSection writes.
