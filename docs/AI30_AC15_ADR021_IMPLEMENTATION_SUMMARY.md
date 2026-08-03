# AI30 AC1.5 Prompt 2 — ADR-021 Implementation Summary

## Deliverable

**ADR-021 — Master Data Ownership** added to the Architecture Documentation Library.

## Files modified (ADL)

| File | Change |
|------|--------|
| `Architecture Documentation Library (ADL)/00_Architecture_Decision_Records.md` | Added ADR-021 (full record); ToC item 23; index row; Cross References → §24; revision 1.1 |
| `Architecture Documentation Library (ADL)/00_Governance_Master_Index.md` | ADR index range → ADR-021; revision history 1.1 |

**Existing ADRs (001–020) were not modified.**

## ADR content coverage

| Required section | Present |
|------------------|---------|
| Context | Yes |
| Problem Statement | Yes |
| Decision (Catalog vs Scheduling ownership lists) | Yes |
| Alternatives Considered | Yes |
| Consequences | Yes |
| Benefits | Yes |
| Tradeoffs | Yes |
| Migration Notes | Yes |
| Future Guidance (good/bad examples + checklist) | Yes |
| Mermaid ownership diagram | Yes |

## Decision (summary)

- **Catalog owns:** Department, Course, Semester, Subject, Group, Staff (+ Language, Medium, Gender, Role).
- **Scheduling consumes** those entities via IDs / Catalog APIs.
- **Scheduling owns:** Academic Year/Term, Working Day, Holiday(+Type), Campus/Building/Floor/Room(+Feature/Availability), Faculty Availability/Preference, Subject Allocation, Time Slot Set/Template, Timetable, Schedule Version, Governance; Conflict Detection & Optimization reserved for future.

## Related product docs

- `docs/AI30_MASTER_DATA_OWNERSHIP_MATRIX.md` (SSOT matrix; linked from ADR)
- `docs/AI30_AC15_ARCHITECTURE_GUARD.md` (automated enforcement)

## Out of scope

No UI, API, database, or Scheduling feature changes in this prompt.