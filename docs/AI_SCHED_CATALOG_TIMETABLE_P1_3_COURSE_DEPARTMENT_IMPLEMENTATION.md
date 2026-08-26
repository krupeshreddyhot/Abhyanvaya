# AI-SCHED-CATALOG/TIMETABLE — P1-3 Prompt 2  
# Course Department Ownership Implementation (Option A)

**Workstream:** AI-SCHED-CATALOG/TIMETABLE  
**Prompt:** P1-3 Prompt 2 — Implement Course Department Ownership  
**Date:** 2026-08-22  
**Type:** Implementation  
**Final status: PASS**

---

## Architecture (approved Option A)

- `Course.DepartmentId` is the **catalog ownership SSOT** (required).
- When `Course.ProgramId` is set ⇒ `Course.DepartmentId == Program.DepartmentId`.
- When `EnablePrograms = false` ⇒ `Course.ProgramId` null; Department still required.
- When `EnablePrograms = true` ⇒ ProgramId may be null (“No Program”); Department still required.
- P1-2 `Program.DepartmentId` unchanged.
- SubjectAllocation / TimetableEntry / TeachingGroup **not** modified.

---

## Schema / migration

Migration: `20260822100000_AI_SCHED_CATALOG_P1_3_CourseDepartment`

1. Add nullable `Course.DepartmentId`
2. Backfill from `Program.DepartmentId` where `ProgramId` set
3. For null-Program Courses: exactly one non-deleted Department **per TenantId**
4. Abort if any Course remains unmapped
5. NOT NULL + Restrict FK + indexes

**Local data:** Course `B.Com` (`ProgramId=1`) → `DepartmentId=1` via Program.

---

## API

| Contract | Change |
| --- | --- |
| `CreateCourseRequest` / `UpdateCourseRequest` | Required `DepartmentId` |
| `CourseMasterRowDto` / `CourseDto` / GET `/api/course` | Expose `DepartmentId` |
| `CourseMasterWriteService` | Server validation via `CourseDepartmentAssociationRules` |
| `AssignCourseToProgramAsync` | When linking Program, sets `Course.DepartmentId = Program.DepartmentId` |

---

## UI

`CoursesPage`: Department selector always; Program selector only when `EnablePrograms`; Program list filtered to Department; selecting Program syncs Department to Program’s Department (server still authoritative).

---

## Tests

- `AiSchedCatalogTimetableP13Prompt2CourseDepartmentTests`
- Updated Prompt 1 contract guards, P1-2 Course assertions, AI29.1D.24 failure-safety harness
- UI Vitest `AiSchedCatalogTimetableP13Prompt2CourseDepartmentUi.test.ts`

---

## Deferred

- **SA consistency** (Prompt 3+): do not enforce `SA.DepartmentId == Course.DepartmentId` yet
- Academic hierarchy tree under Department
- SA uniqueness key changes

---

## Non-actions confirmed

No SubjectAllocation, TimetableEntry, TeachingGroup, Attendance, or StudentSection changes.
