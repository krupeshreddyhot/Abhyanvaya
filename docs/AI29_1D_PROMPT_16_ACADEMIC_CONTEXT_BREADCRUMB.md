# AI29.1D Prompt 16 — Academic Context Breadcrumb

## Goal

One consistent academic context trail across Attendance, Sections, Faculty Workspace, Allocation, and Timetable:

```
Commerce > B.Com > Computer Applications > Semester 3 > Section A > Business Statistics
```

When Programs are disabled (tree omits Program):

```
B.Com > Computer Applications > Semester 3 > Section A > Business Statistics
```

## Rule

**Do not** reconstruct hierarchy display paths in each page. Use the Academic Breadcrumb service/API.

## Backend

| Piece | Role |
|-------|------|
| `IAcademicBreadcrumbService.BuildOperationalContextBreadcrumbAsync` | Entry point |
| `AcademicOperationalBreadcrumbComposer` | Composes trail from canonical `IAcademicTreeService` tree |
| `GET /api/v1/academic-structure/breadcrumb/context` | Query: programId, courseId, groupId, semesterId, sectionId, sectionIds, subjectId |

Authorization & ID consistency hardening: see **Prompt 16A** (`AI29_1D_PROMPT_16A_ACADEMIC_CONTEXT_BREADCRUMB_HARDENING.md`).  
Operational context uses `CanViewAcademicOperationalContext` (not `Program.View`-only).

Existing node breadcrumbs (`/breadcrumb`, `/breadcrumb/section/{id}`, …) remain unchanged.

Programs-disabled behavior is owned by `AcademicTreeService` (Course roots) — the composer does not special-case labels.

## Frontend

| Piece | Role |
|-------|------|
| `academicBreadcrumbService.ts` | API client |
| `academicContextBreadcrumb.ts` | Selection → query mapping only (no labels) |
| `AcademicContextBreadcrumb` | Shared UI; fetches display names from API |

### Surfaces

| Surface | Integration |
|---------|-------------|
| Attendance | `AttendanceMarking` passes local Course/Group/Semester/Section/Subject as `context` |
| Sections | `SectionsPage` uses AcademicUi selection |
| Faculty Workspace | Override from current class + AcademicUi |
| Allocation | `AllocationContextPage` + `EnterpriseAllocationWorkspace` |
| Timetable | `TimetableHubPage` uses AcademicUi selection |

`ContextAwareLayout` supports optional `showAcademicContext` for other context-aware pages.
