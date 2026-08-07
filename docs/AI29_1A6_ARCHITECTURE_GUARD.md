# AI29.1A.6 — Architecture Guard

## Scope

`AcademicArchitectureGuard` validates **architectural dependencies only**.

It does **not** validate:

- Business rules
- Database contents
- Runtime behavior / performance

## Checks

| Check | Intent |
|-------|--------|
| Subject ↛ Section | Curriculum vs operational section |
| Attendance ↛ Program | Attendance stays Course→…→Subject |
| Program ↛ Attendance | Master stability |
| Catalog ↛ Dashboard | Layering |
| Hierarchy ↛ UI | Clean Architecture |
| Domain ↛ Application/API/UI | Dependency rule |
| Read models are records | Immutability |
| Tree ↛ Search ctor | No cyclic tree/search |

## Report API

`GET /api/v1/academic-structure/architecture/report`

## Quality gate

Unit tests assert `AcademicArchitectureGuard.Validate().Passed == true`.
