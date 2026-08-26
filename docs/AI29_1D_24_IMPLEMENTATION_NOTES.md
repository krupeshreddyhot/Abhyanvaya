# AI29.1D.24 — Implementation Notes (Prompts 2–4)

## Prompt 2 — Course Master Program Selector

- `CoursesPage.tsx` shows **Program** when `EnablePrograms = true` (from `GET /academic-structure/configuration`).
- Loads Programs via `listPrograms(true)`; selector filters with `programsForCourseAssignmentSelector` (no Archived for new picks; keeps current Inactive for edit).
- When `EnablePrograms = false`, Program field and column are hidden — Course Code/Name unchanged.
- Loading / error / Retry on configuration+program load.

## Prompt 3 — Persistence

1. `POST/PUT /api/course` for Code/Name (optional `programId` on create/update payloads).
2. Authoritative link: `POST /api/programs/assign-course` via `assignCourseToProgram` after save.
3. UI treats success only after API returns; then refreshes course list (and program list for counts).

## Prompt 4 — Authorization & validation

| Check | Where |
|-------|--------|
| Tenant Course / Program | `AssignCourseToProgramAsync` + `ResolveProgramIdAsync` |
| EnablePrograms gate | Catalog + Course controller |
| Archived/inactive new assign | Catalog (allows keep existing link) |
| Permission | `CanAssignCourseToProgram` = `Program.Manage` **OR** `Setup.Courses.Manage` |
| Hierarchy/stats cache | Assign path + CourseController when ProgramId changes |
| Course tenant on update | `FirstOrDefault` filtered by `TenantId` |

No new endpoints, entities, or join tables.
