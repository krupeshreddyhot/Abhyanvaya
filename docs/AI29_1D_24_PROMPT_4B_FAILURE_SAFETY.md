# AI29.1D.24 Prompt 4B — Transactional Failure Safety, API Contract & Persistence Verification

## 1. Scope

Hardening only for Course Master Create/Edit + Program assignment orchestration established in Prompts 1–4A.

**Not in scope:** CourseProgram entity/join, second assignment endpoint, Attendance/Scheduling/Allocation/Subject Master/Section redesign, Outbox, distributed transactions, React-side assignment rules.

## 2. Existing Prompt 4A architecture

| Concern | Authority |
|---------|-----------|
| Relationship | `Course.ProgramId` → `Program.Id` only |
| Assignment command | `AssignCourseToProgramAsync` / `POST /api/programs/assign-course` |
| Course Master HTTP | Single `POST/PUT /api/course` (UI never calls assign-course separately) |
| Rules | `CourseProgramAssignmentRules` (idempotent no-op; Active-only new assign; retain existing Inactive) |

Prompt 4B adds: DB transaction boundary, omitted vs null API contract, real event/cache invocation tests, cross-tenant Program entity test, authorization gate before mutation when assignment is requested.

## 3. Failure scenarios

| Case | Behavior |
|------|----------|
| A Validation (inactive/archived/invalid/cross-tenant Program) | TX rolls back; Create leaves no Course; Update restores Code/Name/ProgramId; API BadRequest; UI must not show success |
| B Unexpected assignment exception | Exception not swallowed; TX rolls back; no success response |
| C Authorization (`CanAssignCourseToProgram` fails) | `Forbid` **before** write service mutation |
| D Tenant validation | Foreign Program not visible under tenant filter ⇒ `Invalid Program.`; no link, no event, no hierarchy/stats cache invalidation |
| E Cache invalidation failure | Hierarchy/stats invalidate after Assign SaveChanges; not rolled back with DB (see §21) |
| F Event handler failure | `DomainEventDispatcher` logs and swallows handler exceptions (existing contract) |
| G Program unchanged | No-op: no Program SaveChanges, 0 events, 0 hierarchy/stats invalidations |
| H Explicit null | Unlink; one `CourseRemoved` when previously linked |
| I Omitted `programId` on Update | Do **not** call Assign; leave `Course.ProgramId` unchanged |

## 4. Transaction boundary

`ICourseMasterWriteService` (`CourseMasterWriteService`) wraps Course persist + `AssignCourseToProgramAsync` in existing `IUnitOfWork.ExecuteInTransactionAsync` (EF `BeginTransaction` / Commit / Rollback).

```
BEGIN
  persist Course Code/Name (Create insert or Update)
  AssignCourseToProgramAsync (when applicable)
COMMIT
```

On failure: `ROLLBACK` — no manual compensate/delete/revert of Code/Name.

Master list cache key `tenant:{id}:master:courses` is removed **after** successful commit.

## 5. Create behavior

When `EnablePrograms`:

1. Insert Course with `ProgramId = null`
2. Call Assign with requested Program (omitted/null ⇒ unassigned no-op)
3. On Assign failure → entire TX rolls back (no orphan Course)

When `EnablePrograms = false`: Assign is **not** called; legacy Code/Name create only.

## 6. Update behavior

1. Update Code/Name inside TX
2. If `ProgramIdSpecified` and Programs enabled → Assign
3. If `programId` omitted → skip Assign (retain existing Program)
4. On Assign failure → TX rollback restores Code/Name/ProgramId

## 7. Inactive Program behavior

| Scenario | Result |
|----------|--------|
| Keep existing link to Inactive Program | Success, no-op (0 events, 0 cache) |
| New assign to Inactive / Archived | Fail |
| Existing archived relationship | Retention via no-op when requested id equals existing (same as Inactive retention); no new policy invented |

## 8. Idempotency

`requestedProgramId == existingProgramId` (normalized) ⇒ `CourseProgramAssignmentOutcome.IsNoOp`:

- no Program relationship SaveChanges
- 0 domain events
- 0 hierarchy + statistics cache invalidations
- successful HTTP response

Verified for 1×, 2×, and 3× identical Assign calls.

## 9. Event semantics

| Transition | Events |
|------------|--------|
| Commerce → Commerce | 0 |
| Commerce → Science | exactly 1 `CourseAssigned` (Science) |
| Commerce → null | exactly 1 `CourseRemoved` |
| null → Science | exactly 1 `CourseAssigned` |

Event names unchanged. Tests assert actual `IDomainEventDispatcher.DispatchAsync` payloads, not only decision flags.

**Post-commit handler failure:** dispatcher catches handler exceptions, logs, does **not** fail the business operation. DB commit is **not** undone by a late handler fault.

## 10. Cache semantics

Uses existing `IAcademicHierarchyCache` + `IAcademicStatisticsCache` only.

| Transition | Hierarchy | Statistics |
|------------|-----------|------------|
| unchanged | 0 | 0 |
| changed | 1 | 1 |
| removed | 1 | 1 |

Tests verify Moq invocation counts.

## 11. Tenant isolation

Scenario uses a real `Program` row with `TenantId = B` while current user is Tenant A. Lookup filters `TenantId == current` ⇒ target null ⇒ `Invalid Program.` No Course mutation, events, or hierarchy/stats invalidation.

## 12. Omitted vs null ProgramId contract

Presence-aware DTOs (`CreateCourseRequest` / `UpdateCourseRequest`): setter sets `ProgramIdSpecified = true` when JSON includes `programId` (including null).

| JSON | Meaning |
|------|---------|
| `"programId": 15` | Assign Program 15 |
| `"programId": null` | Explicit unlink |
| property omitted (Update) | **Do not modify** existing Program |
| property omitted (Create) + EnablePrograms | Unassigned |

UI Course Master always sends `programId` when EnablePrograms (explicit value or null). Clients that omit on Update keep the previous Program.

## 13. UI success / failure behavior

`CoursesPage` clears success before save; sets success only after API resolves; on catch sets error only (never success). Reload uses server list. No React rollback/assignment rules (`callAssignCourseSeparately: false`).

## 14. Attendance compatibility

No changes to AttendanceSessionResolver, Subject Master, Section business logic, TimetableSections, SectionGroup, Allocation, or Scheduling. Faculty Course→Group→Semester→(Section)→Subject→Period paths unchanged by this prompt.

## 15. Database impact

**NONE** — no migration, no schema change.

## 16. Tests

- `AI29_1D_24_Prompt4B_FailureSafetyIntegrationTests.cs` — cases 1–25 (create/update failures, TX rollback simulation, events, cache, tenant, omitted/null, Programs disabled, architecture guards)
- Prompt 4A rule tests remain
- UI `courseMasterPersistence` — no separate assign-course

## 17. Regression results

| Suite | Passed | Failed | Skipped | Duration |
|-------|--------|--------|---------|----------|
| `AI29_1D_24_Prompt4B` | 26 | 0 | 0 | ~640 ms |
| `AI29_1D_24` + architecture guard filter | 70 | 0 | 0 | ~2 s |
| Broad `AI29*` / `AI29_1A*`…`AI29_1D*` filter | 395 | 0 | 0 | ~4 s |
| Attendance / AI22 / Scheduling / AI30 / Faculty / AI31 / Dashboard filter | 398 | 0 | 0 | ~50 s |
| UI `courseMasterPersistence` + `courseProgramAssignment` + `academicCascade` | 30 | 0 | 0 | (vitest) |
| API build | success | | | |
| UI build | success | | | ~12 s |

## 18. Files changed

- `Abhyanvaya.Application/Academic/CourseMasterWriteService.cs` (new)
- `Abhyanvaya.Application/Academic/ICourseMasterWriteService.cs` (new)
- `Abhyanvaya.Application/Academic/CourseProgramAssignmentOutcome.cs` (new)
- `Abhyanvaya.Application/Academic/AcademicCatalogService.cs` (outcome + counts)
- `Abhyanvaya.Application/Academic/IAcademicCatalogService.cs` / `IAcademicStructureService.cs` / `AcademicStructureService.cs`
- `Abhyanvaya.Application/DTOs/Course/*` (presence-aware ProgramId)
- `Abhyanvaya.Application/DependencyInjection.cs`
- `Abhyanvaya.API/Controllers/CourseController.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_24_Prompt4B_FailureSafetyIntegrationTests.cs`
- `docs/AI29_1D_24_PROMPT_4B_FAILURE_SAFETY.md`

## 19. APIs changed

- `POST/PUT /api/course` — Update omitted `programId` no longer treated as unlink; Create/Update failure uses TX rollback; assign auth checked when assignment requested
- `POST /api/programs/assign-course` — still authoritative; return type of service is `CourseProgramAssignmentOutcome` (HTTP still NoContent)

## 20. Architecture guard result

UI must not access EF/DbContext, implement Program assignment rules, tenant validation, cache invalidation, domain events, or persistence rollback. Guarded by source assertions + Prompt 4A persistence plan (`callAssignCourseSeparately: false`).

## 21. Known limitations

1. **Events + hierarchy/statistics cache invalidation run inside `AssignCourseToProgramAsync` before the outer TX commit.** If Commit failed after Assign returned (rare), side effects could outlive a rolled-back DB state. Master courses cache is invalidated only after successful commit.
2. **`DomainEventDispatcher` swallows handler exceptions** — handler fault after dispatch does not fail HTTP or roll back DB.
3. Authorization for Course Master uses `CanManageCourses` + additional `CanAssignCourseToProgram` when assignment is requested; both are satisfied by `Setup.Courses.Manage` in current policy wiring.
4. Rollback semantics in unit tests are simulated via snapshot restore around `ExecuteInTransactionAsync`; production uses real EF transactions.
