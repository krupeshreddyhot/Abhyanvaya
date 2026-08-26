# AI29.1D.24 Prompt 4A — Course → Program Persistence Consistency Hardening

## Problem found

1. **Duplicate Program writes** — `CoursesPage` sent `programId` on `POST/PUT /api/course` **and** called `POST /api/programs/assign-course`, risking divergent validation and double cache/event side effects.
2. **Inactive Program inconsistency** — `CourseController.ResolveProgramIdAsync` rejected Inactive Programs on Update even when the Course already linked to that Program, while `AssignCourseToProgramAsync` correctly allowed retaining the existing link.

## Architectural decision

- `Course.ProgramId` remains the sole Course→Program relationship.
- `AssignCourseToProgramAsync` / `POST /api/programs/assign-course` remains the **authoritative** Program assignment command.
- **No** new entity, join table, hierarchy, resolver, or second assignment endpoint.
- Database schema change: **NONE**.

## Chosen persistence approach

| Layer | Responsibility |
|-------|----------------|
| UI Course Master | Single HTTP call: `POST/PUT /api/course` with Code/Name and (when EnablePrograms) `programId`. **Never** calls assign-course separately. |
| `CourseController` | Persists Code/Name; when EnablePrograms, orchestrates `IAcademicStructureService.AssignCourseToProgramAsync` in-process. |
| `AcademicCatalogService.AssignCourseToProgramAsync` | Validates, applies `Course.ProgramId`, events, cache. |

**Atomicity / failure behavior**

- **Create:** if assign fails after insert, the new Course row is **deleted** (compensate) and BadRequest is returned — UI never sees success.
- **Update:** if assign fails after Code/Name save, Code/Name are **reverted** and BadRequest is returned — UI never shows success with an ambiguous Program relationship.
- Limitation (documented): compensation is application-level (not a distributed transaction). Acceptable under existing HTTP contracts.

## Idempotency behavior

`CourseProgramAssignmentRules` + catalog:

- If `previousProgramId == requestedProgramId` (normalized): **no-op** — no SaveChanges side effects beyond early return, **no** `CourseAssigned`/`CourseRemoved`, **no** hierarchy/statistics invalidation.

## Inactive Program behavior

| Scenario | Result |
|----------|--------|
| New assign to Active | Allowed |
| New assign to Inactive / Archived | Rejected |
| Keep existing link when Program became Inactive | Allowed (no-op) |
| Change Commerce → Science (Science Active) | Allowed; one `CourseAssigned` |
| Change Commerce → Inactive Science | Rejected |

## Event behavior

| Transition | Event |
|------------|--------|
| `P → P` | None |
| `null → P` | `CourseAssigned` |
| `P → Q` | `CourseAssigned` (Q only) |
| `P → null` | `CourseRemoved` |

## Cache behavior

Invalidate `AcademicHierarchyCache` + `AcademicStatisticsCache` **only** when `ProgramId` actually changes.

## Course Master UI

- EnablePrograms = true: Code, Name, Program (shows `Commerce (Inactive)` when retaining inactive).
- EnablePrograms = false: Program hidden; legacy Code/Name only.

## Fail-closed cascade

Unchanged: when Programs enabled, empty Program selection ⇒ no Courses in academic cascade; no “all Courses” fallback (`filterCoursesForProgram`).

## Tests

- `AI29_1D_24_Prompt4A_CourseProgramPersistenceTests.cs` — cases 1–15 (rules, events, cache, disabled legacy).
- `courseMasterPersistence.test.ts` — no separate assign-course from UI.
- `courseProgramAssignment.test.ts` — selector inactive retention.

## Regression results

| Suite | Result |
|-------|--------|
| `AI29_1D_24_Prompt4A` + related Program/cascade filters | **68 passed** |
| Broader `AI29_1A* / AI29_1B* / AI29_1C* / AI29_1D*` | **361 passed** |
| UI `courseMasterPersistence` + `courseProgramAssignment` + `academicCascade` | **29 passed** |

Attendance resolver, scheduling engine, Section model, Subject Master, and Allocation Engine were **not** modified.

Faculty without timetable path (Course → Group → Semester → Subject → Period) is unchanged by this prompt.

## Files changed

| File | Change |
|------|--------|
| `Abhyanvaya.Application/Academic/CourseProgramAssignmentRules.cs` | **New** — pure transition rules |
| `Abhyanvaya.Application/Academic/AcademicCatalogService.cs` | Idempotent assign + rules |
| `Abhyanvaya.API/Controllers/CourseController.cs` | Orchestrate assign; no direct ProgramId write; compensate on failure |
| `abhyanvaya-ui/src/pages/setup/CoursesPage.tsx` | Single write path |
| `abhyanvaya-ui/src/utils/courseMasterPersistence.ts` | Save plan helper |
| `abhyanvaya-ui/src/utils/courseMasterPersistence.test.ts` | UI contract tests |
| `Abhyanvaya.Application.UnitTests/.../AI29_1D_24_Prompt4A_*.cs` | Persistence tests |
| `docs/AI29_1D_24_PROMPT_4A_PERSISTENCE_HARDENING.md` | This document |

## APIs changed

| API | Change |
|-----|--------|
| `POST /api/course` | Orchestrates assign-course when EnablePrograms; compensate create on assign failure |
| `PUT /api/course` | Orchestrates assign-course when EnablePrograms; revert Code/Name on assign failure |
| `POST /api/programs/assign-course` | Behavior hardened (idempotent); route unchanged |

## Database changes

**NONE**
