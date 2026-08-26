# AI29.1D.24 Prompt 4B — Chief Architect Final Hardening Report

**Stream:** Course → Program Assignment UI — Failure Safety, API Contract, Events/Cache, Tenant/Auth, Regression  
**Status:** **CONDITIONAL PASS**  
**Database schema changes:** **NONE**

This report consolidates Prompts 4B / 4B.1–4B.6. Claims below are limited to what source and tests demonstrate. Words such as “atomic” or “fully transactional across DB + events + cache” are **not** used for the overall operation.

---

## 1. Architecture

| Layer | Responsibility |
|-------|----------------|
| UI Course Master | Single `POST/PUT /api/course`; never calls `assign-course` separately |
| `CourseController` | Auth (`CanManageCourses` + assign gate); maps validation/not-found; does not own rules |
| `ICourseMasterWriteService` | Orchestrates Course Code/Name persist + Program assign inside `ExecuteInTransactionAsync` |
| `IAcademicStructureService` → `AssignCourseToProgramAsync` | Authoritative Program relationship command |
| `CourseProgramAssignmentRules` | Pure rules (no-op, Active-only new link, retain existing Inactive) |
| Relationship model | **`Course.ProgramId` only** — no CourseProgram entity/join |

**Not introduced:** second assignment endpoint, Outbox, DTC, event broker, Attendance/Scheduling/Allocation/Section/Subject Master changes, React assignment rules.

---

## 2. Transaction boundary (database)

**Verified:** `CourseMasterWriteService` wraps Course insert/update `SaveChanges` and `AssignCourseToProgramAsync` (which may `SaveChanges` for `ProgramId`) in existing `IUnitOfWork.ExecuteInTransactionAsync` (`BeginTransaction` → action → `Commit` / on exception `Rollback` + rethrow).

**Covered by that database transaction:** Course row + `Course.ProgramId` persistence for the Course Master Create/Update path.

**Standalone** `POST /api/programs/assign-course` does **not** open that wrapper.

**After successful Commit:** master list cache key `tenant:{id}:master:courses` is removed; response is loaded from DB.

---

## 3. Create failure behavior

When `EnablePrograms` and Assign fails (validation, inactive/archived/cross-tenant Program, unexpected exception):

- Database transaction **rolls back** — no committed orphan Course (demonstrated by write-service tests with rollback simulation + `ExecuteInTransactionAsync` in production).
- API returns failure (`400` for `ValidationException`; unhandled → ProblemDetails). UI must not show success.

When Assign is a no-op (create with null/omitted Program): Course is created unassigned; no assign/remove events; no hierarchy/statistics invalidation from Assign.

---

## 4. Update failure behavior

If Code/Name are saved in the same TX action and Assign then fails:

- Database transaction **rolls back** — Code, Name, and ProgramId return to pre-request committed state.
- API failure; UI must not report success.

Omitted `programId` skips Assign entirely (Program unchanged).

---

## 5. Inactive Program retention

Existing `Course.ProgramId = Commerce` where Commerce later becomes Inactive:

- Request retaining Commerce (same id) → **no-op success**
- No assign/remove event; no hierarchy/statistics invalidation

---

## 6. New inactive Program rejection

Changing to a different Inactive Program (or first-time assign to Inactive) → **rejected** (`ValidationException` / API `400`). No relationship change on success path.

---

## 7. Archived Program behavior

- **New** assignment to Archived → **rejected**
- **Existing** link retained when request keeps the same Program id (no-op retention; same pattern as Inactive — no new policy invented)

---

## 8. Idempotency

`requestedProgramId == existingProgramId` (normalized) → no-op:

- No Program relationship `SaveChanges` in Assign
- Zero domain events
- Zero hierarchy + statistics invalidations  
Verified for repeated identical Assign calls (×3 in tests).

---

## 9. Event semantics

Existing events only: `CourseAssigned`, `CourseRemoved`.

| Transition | Dispatched (spy-verified) |
|------------|---------------------------|
| Commerce → Commerce | 0 |
| Commerce → Science | exactly 1 `CourseAssigned` |
| Commerce → null | exactly 1 `CourseRemoved` |

**Order in Assign (non-no-op):** `SaveChanges(ProgramId)` → dispatch → hierarchy/statistics invalidate.

**Handler failure after persistence:** `DomainEventDispatcher` **swallows** handler exceptions (log only). Assign still completes; ProgramId remains updated; hierarchy/statistics invalidation still proceeds. Therefore **event-handler success is not coupled to database commit success**, and handler failure does **not** roll back the database.

Dispatch runs **inside** the Course Master TX *action* (before `Commit`). An **uncaught** exception from dispatch infrastructure (not handler swallow) would prevent Commit. Handler failures do not qualify.

---

## 10. Cache semantics

Existing caches only: `IAcademicHierarchyCache`, `IAcademicStatisticsCache` (+ master courses list via `ICacheService` after Commit).

| Transition | Hierarchy | Statistics |
|------------|-----------|------------|
| unchanged | 0 | 0 |
| changed / removed | 1 each (spy-verified) | 1 each |

`SmartCacheService.RemoveAsync` may ignore Redis errors → possible stale Redis after success. Cache clear is **not** reversed by DB rollback (entries may already have been removed).

---

## 11. Tenant isolation

Tenant A Course → Tenant B Program (real foreign Program entity):

- Rejected as `"Invalid Program."` → API **400**
- No ProgramId mutation, no events, no hierarchy/statistics invalidation
- Course Master Update also rolls back staged Code/Name

---

## 12. Authorization

Policy: `CanAssignCourseToProgram` (existing) — `Program.Manage` **OR** `Setup.Courses.Manage` (+ SuperAdmin; requires `TenantId > 0` for non–SuperAdmin).

| Outcome | Status |
|---------|--------|
| Missing assign permission when Program mutation requested | **403** Forbid **before** write |
| Cross-tenant / rule validation | **400** |
| Course Master base | `CanManageCourses` (`Setup.Courses.Manage`) |

**No new permission names.** Policy is claim-based; it does not evaluate Staff/User `IsActive`.

---

## 13. Omitted vs null `programId`

Presence-aware DTOs (`ProgramIdSpecified`):

| JSON (Update) | Meaning |
|---------------|---------|
| `"programId": 15` | Assign 15 |
| `"programId": null` | Explicit unlink |
| property omitted | **Do not modify** existing Program (legacy-safe) |

Create: omitted/null → unassigned when Programs enabled; value → assign.

---

## 14. Programs enabled / disabled

| `EnablePrograms` | Behavior |
|------------------|----------|
| `false` | Program selector hidden; Assign **not** called from Course Master write path; legacy Code/Name CRUD |
| `true` | Program selector; Course→Program cascade fail-closed (no “all Courses” fallback) |

---

## 15. UI behavior

- Success only after API success; list refresh from server
- Failure shows error; no success toast; no React rollback/assignment rules
- `buildCourseMasterSavePlan` → `callAssignCourseSeparately: false`; when Programs enabled, always sends explicit `programId` (value or null)

---

## 16. Attendance compatibility

Prompt 4B did not modify AttendanceSessionResolver, Subject Master, Section business logic, Allocation, or Scheduling.

Regression (Prompt 4B.6) confirmed suites covering:

- No-timetable Course → Group → Semester → Subject → Period  
- Course → Group → Semester → Section → Subject → Period  
- Timetable-driven attendance  
- Combined Section A+B  

---

## 17. Database changes

**NONE** — no migration, no schema change.

---

## 18. API changes

| Endpoint | Change |
|----------|--------|
| `POST/PUT /api/course` | Orchestration via write service + DB TX; omitted vs null contract; assign auth gate |
| `POST /api/programs/assign-course` | Unchanged HTTP contract; still authoritative Assign for non–Course-Master clients |
| Response | `{ id, code, name, programId }` (`CourseMasterRowDto`) |

---

## 19. Tests (Prompt 4B family)

| Suite | Focus |
|-------|--------|
| `AI29_1D_24_Prompt4B_FailureSafetyIntegrationTests` | TX failure, create/update, inactive, idempotency |
| `AI29_1D_24_Prompt4B3_EventCacheConsistencyTests` | Spy event/cache counts; handler swallow |
| `AI29_1D_24_Prompt4B4_ProgramIdApiContractTests` | Omitted / null / assign JSON contract |
| `AI29_1D_24_Prompt4B5_TenantAuthorizationTests` | Cross-tenant + policy matrix |
| Prompt 4A rules tests | Retained |
| UI persistence / cascade tests | No duplicate assign; fail-closed cascade |

Prompt 4B filter: **56 passed**, 0 failed (Prompt 4B.6).

---

## 20. Regression results (Prompt 4B.6 snapshot)

| Area | Passed | Failed | Skipped |
|------|--------|--------|---------|
| Broad AI29* | 425 | 0 | 0 |
| AI29.1D.24 all | 71 | 0 | 0 |
| Prompt 4B | 56 | 0 | 0 |
| Architecture guard (broad) | 46 | 0 | 0 |
| Attendance path filter | 96 | 0 | 0 |
| AI30 Scheduling/Optimization | 165 | 0 | 0 |
| AI31 Faculty/Dashboard/Workspace | 112 | 0 | 0 |
| UI Course Master related | 30 | 0 | 0 |
| API build / UI build | success | | |

Full table: `docs/AI29_1D_24_PROMPT_4B6_REGRESSION.md`.

---

## 21. Architecture guard

**PASS** — UI must not own EF, Program assignment rules, tenant validation, cache invalidation, domain events, or persistence rollback. Guarded by Prompt 21 / 21A / 15A Prompt 9 suites and 4B source assertions (`callAssignCourseSeparately: false`, Forbid-before-write).

---

## 22. Known limitations

1. **Events and hierarchy/statistics invalidation run inside Assign before outer `Commit`.** DB row changes participate in the EF transaction; cache eviction is not a transactional resource. If Commit failed after invalidation, caches may be colder than DB.
2. **Domain event handlers cannot fail the business operation** (exceptions swallowed). Observability/metrics side effects are best-effort.
3. **Redis remove failures** in `SmartCacheService` are often ignored → possible stale Redis.
4. **Post-Commit** master-cache remove or `LoadAsync` failure can yield HTTP error after DB already committed.
5. **Standalone assign-course** has no Course Master `ExecuteInTransactionAsync` wrapper.
6. **Account IsActive** is not part of `CanAssignCourseToProgram` (claim-based only).

---

## 23. Supporting documents

| Doc | Topic |
|-----|--------|
| `AI29_1D_24_PROMPT_4B_FAILURE_DISCOVERY.md` | Failure boundaries |
| `AI29_1D_24_PROMPT_4B_FAILURE_SAFETY.md` | 4B hardening notes |
| `AI29_1D_24_PROMPT_4B3_EVENT_CACHE_CONSISTENCY.md` | Events/cache |
| `AI29_1D_24_PROMPT_4B4_PROGRAMID_API_CONTRACT.md` | API contract |
| `AI29_1D_24_PROMPT_4B5_TENANT_AUTHORIZATION.md` | Tenant/auth |
| `AI29_1D_24_PROMPT_4B6_REGRESSION.md` | Exact regression counts |
| `AI29_1D_24_PROMPT_4A_PERSISTENCE_HARDENING.md` | Prior 4A baseline |

---

## 24. Final verdict

**CONDITIONAL PASS** — Course Master Create/Update + Program assignment meet the hardened failure-safety, contract, event/cache spy, tenant, and regression bar with a **verified EF database transaction** around Course/`ProgramId` persistence. End-to-end “DB + events + all caches as one unit of work” is **not** claimed; handler and cache limitations are explicit above.
