# AI29.1A — Architecture Review

## Verdict

**Compliant** foundation release. Optional Program layer is additive; attendance/scheduling/subject contracts preserved.

## Constraints

| Constraint | Result |
|------------|--------|
| AttendanceSessionResolver unchanged | Not modified |
| Attendance APIs unchanged | Not modified in AI29.1A |
| Subject Master unchanged | Not modified |
| Timetable/Scheduling engines unchanged | Not modified |
| AI31 Dashboard unchanged | Prep APIs only |
| Soft delete / tenant / audit | Preserved via BaseEntity |
| FluentValidation | Program + assign validators |

## ADR — Academic Organizational Unit (future)

**Decision:** Implement **Program** now; document a future abstraction **Academic Organizational Unit (AOU)** that may represent Faculty, School, Division, Academic Unit, or Program.

**Rationale:** Institutions vary (universities vs colleges vs medical schools). Coding only to “Program” forever would force redesign. Keeping AOU as the conceptual parent (with Program as the first concrete type) preserves longevity without expanding AI29.1A scope.

**Consequences:** AI29.1A ships Program tables/APIs only. Future phases may rename or generalize without breaking Course.ProgramId semantics (nullable parent AOU id).

## Clean Architecture

- Domain entities in `Entities/Academic`
- Application service + validators
- API controllers thin
- UI permission-aware Catalog page
