# AI29.1D.24B.2 Prompt 9.4 — Validation Data Inventory & Cleanup

**Date:** 2026-08-11  
**Rule:** Inventory before mutate. No guessing. No silent academic alterations.  
**Evidence:** `prompt9-validation-inventory.json`

## Classification legend

| Code | Meaning |
|------|---------|
| A | Existing legitimate data |
| B | Temporary validation data |
| C | Existing data modified temporarily |
| D | Unknown / cannot safely determine |

## Inventory (Prompt 6 / 7 / 8)

| Id | Classification | Action taken |
|----|----------------|--------------|
| Semester IV (id 9) | B | **Retained** — catalog node; not deleted |
| Section `CA-IV-A` | B | **Retained** — deletion deferred (population risk) |
| Sections `CA-A`, `CA-B`, `FIN-A` | B | **Retained** — Prompt 7 created; still used by acceptance evidence |
| Section `SCCA01` | A | **Retained** |
| Five students moved to Semester IV | C | **No restore** — originals not established |
| StudentSections 20+20 (CA-A/CA-B) | B | **Retained** — supports optional Section re-validation |
| Faculty-A `teststaff1` | C | **Retained** — persona; not deleted |
| FACULTY role 101 + `Section.View` | A | **Retained** — correct RBAC |
| Staff 7 teaching subjects | C | **Retained** — original set unknown; no restore guess |

## Semester IV students (mandatory statement)

Prompt 7 moved five existing students to Semester IV for validation population.

**Original academic assignment could not be established; restoration not performed.**

No before/after student id + original `semesterId` map exists in Prompt 7 inventory, data-prep scripts, or artifact JSON. Guessing restorations is forbidden.

## Cleanup executed

**None.** Category B items were intentionally retained rather than deleted through APIs because:

1. Sections/memberships underpin already-captured live acceptance (Tests 1–2).
2. Deleting validation sections with unknown downstream references is unsafe without a full dependency graph.
3. Category C Sem IV moves cannot be reversed without originals.

## Restored data

**None.**

## Intentionally retained

All inventoried validation artifacts listed above (except the explicit “no restore” Sem IV student moves, which remain in their Prompt 7 state).

## Database schema

**No schema changes** in Prompt 9.
