# AI29.1D.24 — Course → Program Assignment (Prompts 5–21)

## Objective

Expose the existing authoritative **`Course.ProgramId`** relationship in Course Master and Program Master UIs, with fail-closed Program-mode cascading, without new entities, join tables, resolvers, or engines.

## Existing architecture reused

- `Course.ProgramId` → `Program.Id`
- `AssignCourseToProgramAsync` / `POST /api/programs/assign-course`
- `CourseMasterWriteService` + `POST/PUT /api/course` (Course Master)
- `GET /api/programs/{id}/courses` (count/list from `ProgramId`)
- `IAcademicHierarchyCache` / `IAcademicStatisticsCache`
- Domain events `CourseAssigned` / `CourseRemoved`
- Policies `CanAssignCourseToProgram`, `CanManageCourses`, Program.* permissions

## Course.ProgramId relationship

Sole Course→Program link. Nullable by AI29.1A policy (ADR / entity docs).

## UI changes

| Surface | Change |
|---------|--------|
| Course Master | Program selector: **No Program** (null allowed); change confirmation; loading/empty/error/retry |
| Program Master View | Assigned Courses table; Assign Course; Unassign; count from `Course.ProgramId` |
| Academic cascade | Unchanged fail-closed (`filterCoursesForProgram`) |

## API / service reused

**New API: NONE.** Existing authoritative APIs reused (`/api/course`, `/api/programs/assign-course`, `/api/programs/{id}/courses`).

## Permission model

- Assign/unassign: `Program.Manage` **OR** `Setup.Courses.Manage` (server policy)
- View Programs: `Program.View` (catalog)
- No new permission keys

## Validation

Active-only new assignment; retain existing Inactive; reject Archived/Inactive new links; tenant-scoped Program lookup.

## Program enabled behavior

Selector visible; fail-closed Course options; assign via Course CRUD or Program View assign-course.

## Program disabled behavior

Selector hidden; no Assign from Course Master path; legacy Course→Group→Semester→Section→Subject; Attendance/Scheduling/Subject Master unchanged.

## Fail-closed behavior

`EnablePrograms` + no Program selected → empty Course options. No “all Courses” fallback. Null `ProgramId` courses never appear under a Program.

## Cache invalidation

Assign path invalidates hierarchy + statistics caches; UI calls `AcademicUiContext.refreshCatalogs()` after Program assign/unassign.

## Audit / telemetry

Existing `CourseAssigned` / `CourseRemoved` (+ metrics handlers). No duplicate event names invented.

## Database

**Database schema change: NONE**

## Architecture guard

UI → API/Application → Domain/Services → Infrastructure. No CourseProgram entity. No UI EF.

## Related docs

- `AI29_1D_24_PROMPT_4B_FINAL.md`
- `AI29_1D_24_FINAL_VALIDATION.md`
