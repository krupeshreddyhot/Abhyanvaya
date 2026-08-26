# AI29.1D.24 Prompt 4B.1 — Failure Boundary Discovery

**Mode:** Discovery only (no implementation changes in this prompt).  
**Inspected:** Course Master Create/Update + Program assignment path in current source (Prompt 4A architecture, with Prompt 4B write-service/TX already present in tree).  
**Out of scope:** Attendance, Scheduling, Sections, Subject Master, Allocation.

---

## 1. Call graph (verified)

```
UI CoursesPage
  → POST/PUT /api/course  (single write; no separate assign-course)
       → CourseController
            → [Authorize CanManageCourses]
            → optional Forbid if assign requested && !CanAssignCourseToProgram
            → ICourseMasterWriteService.CreateAsync / UpdateAsync
                 → pre-TX validation (code/name/dup)
                 → IUnitOfWork.ExecuteInTransactionAsync   ← VERIFIED TX
                      → Add/Update Course + SaveChangesAsync
                      → (if EnablePrograms + applicable) IAcademicStructureService.AssignCourseToProgramAsync
                           → AcademicCatalogService.AssignCourseToProgramAsync
                                → validator / rules
                                → SaveChangesAsync (ProgramId) when not no-op
                                → DomainEventPublisher.DispatchAndClearAsync
                                → IAcademicHierarchyCache + IAcademicStatisticsCache invalidate
                 → after TX success: ICacheService.RemoveAsync(master:courses)
                 → LoadAsync → Ok(row)

Alternate (not used by Course Master UI):
POST /api/programs/assign-course
  → ProgramsController → AssignCourseToProgramAsync
  → NO ExecuteInTransactionAsync wrapper in controller/service (verified)
```

---

## 2. Prompt 4A vs current source (transaction claim)

| Era | Atomicity mechanism (documented / coded) |
|-----|------------------------------------------|
| Prompt 4A docs | Application-level **compensate** (delete Course on create failure) / **revert** Code/Name on update failure — **not** a DB transaction |
| Current source | `CourseMasterWriteService` calls `_db.ExecuteInTransactionAsync(...)` — **verified** in `ApplicationDbContext.UnitOfWork.cs` as `Database.BeginTransactionAsync` + Commit / Rollback |

**Claim rule for this discovery:** A database transaction exists for Course Master Create/Update orchestration **only** because `ExecuteInTransactionAsync` is present on the write path. Standalone `assign-course` does **not** open that wrapper.

---

## 3. Verified transaction boundary (Course Master)

**Infrastructure:** `IApplicationDbContext : IUnitOfWork`  
**Implementation:** `ApplicationDbContext` explicit interface method `ExecuteInTransactionAsync`:

1. `BeginTransactionAsync`
2. Run `action`
3. On success → `CommitAsync`
4. On any exception → `RollbackAsync` + rethrow

**Inside the TX action (Create):**

1. `AddAsync(Course)` with `ProgramId = null`
2. `SaveChangesAsync` (Course insert)
3. If `EnablePrograms` → `AssignCourseToProgramAsync` (may `SaveChangesAsync` again for `ProgramId`)

**Inside the TX action (Update):**

1. Reload Course
2. Mutate Code/Name → `SaveChangesAsync`
3. If `EnablePrograms && ProgramIdSpecified` → `AssignCourseToProgramAsync`

**Outside / after TX (not rolled back by DB):**

- Master courses cache: `_cache.RemoveAsync($"tenant:{tenantId}:master:courses")`
- Read-back `LoadAsync`
- Domain events already dispatched **inside** Assign (before Commit)
- Hierarchy/statistics invalidation already run **inside** Assign (before Commit)

---

## 4. Failure points catalog

### 4.1 Database persistence can fail

| Location | Operation | Effect if throws |
|----------|-----------|------------------|
| `CourseMasterWriteService` pre-TX queries | `AnyAsync` / `FirstOrDefaultAsync` / config read | Exception escapes; **no TX started** (or only reads) |
| Create: `AddAsync` + first `SaveChangesAsync` | Insert Course | TX rolls back; no committed Course |
| Update: `SaveChangesAsync` (Code/Name) | Update Course | TX rolls back |
| Assign: `SaveChangesAsync` (ProgramId) | Update `Course.ProgramId` | TX rolls back entire Course Master action (when called from write service) |
| TX `CommitAsync` | Commit | Rollback path in catch; exception rethrown |
| Standalone assign-course SaveChanges | ProgramId only | No outer TX; EF default save semantics only |
| Post-TX `LoadAsync` | Read | DB already committed; failure → exception after success persist (see §6) |

Concurrency / constraint / connectivity (`DbUpdateException`, etc.) are not specially caught in CourseController — they escape to `GlobalExceptionHandler`.

### 4.2 Program assignment can fail

| Cause | Thrown type | When |
|-------|-------------|------|
| FluentValidation on `AssignCourseProgramRequest` | `ValidationException` | Bad CourseId / ProgramId shape |
| Course not in tenant | `KeyNotFoundException` | Assign load |
| Rules: invalid / inactive / archived / cross-tenant (null target) | `ValidationException` | `decision.Error` |
| Unexpected fault in Assign / infra | any | not caught inside Assign |

**Controller mapping (Course + Programs assign-course):**

- `ValidationException` → `400 BadRequest`
- `KeyNotFoundException` → `404`
- Other → not caught in controller → `GlobalExceptionHandler` (typically 500 ProblemDetails)

**Auth failure (Course Master):** `Forbid()` **before** write service — no Course mutation started.

### 4.3 Event dispatch can fail

| Layer | Behavior (verified) |
|-------|---------------------|
| `DomainEventPublisher.DispatchAndClearAsync` | Clears aggregate events, then `DispatchAsync` |
| `DomainEventDispatcher.DispatchAsync` | Logs each event; invokes handlers |
| Handler `catch (Exception)` | **Swallows** handler exceptions; logs error; **does not** fail Assign |
| Dispatcher itself (e.g. DI/`GetServices`/`Invoke` unexpected) | Could throw **before** handler try/catch or outside it — would fail Assign and, on Course Master path, roll back TX if still before Commit |

**Important:** Dispatch runs **after** Assign’s `SaveChangesAsync` but **still inside** the Course Master TX action (before Commit). If dispatch threw an uncaught exception, TX would roll back DB — but handler failures are intentionally swallowed, so they do **not** trigger rollback.

There is **no** Outbox and **no** post-commit event bus.

### 4.4 Cache invalidation can fail

| Cache | Call site | On failure |
|-------|-----------|------------|
| Hierarchy | `InvalidateHierarchyAsync` → multiple `ICacheService.RemoveAsync` | Exception propagates from Assign (unless cache impl swallows) |
| Statistics | `InvalidateAsync` | Same |
| Master courses | `RemoveAsync` **after** TX commit in write service | Exception after DB commit → HTTP failure despite committed data |

**`SmartCacheService.RemoveAsync`:** Redis remove errors are **caught and ignored**; memory remove still runs. So Redis-side invalidate failure often does **not** fail the request; stale Redis entries are possible.

Invalidation is **not** transactional with DB rollback.

### 4.5 Unexpected exceptions that can escape

| Source | Caught by CourseController? | Typical response |
|--------|----------------------------|------------------|
| `ValidationException` | Yes → 400 | BadRequest message |
| `KeyNotFoundException` | Yes → 404 | NotFound |
| `InvalidOperationException` / `DbUpdateException` / etc. from TX/Assign | **No** | GlobalExceptionHandler |
| Auth Forbid | Returned as IActionResult | 403 |
| Post-commit master cache / LoadAsync | **No** | GlobalExceptionHandler after DB committed |
| Event **handler** exceptions | Swallowed in dispatcher | Operation continues as success |

---

## 5. Ordered timelines

### 5.1 Create (EnablePrograms = true)

```
[API] Authorize CanManageCourses
[API] If programId > 0: Authorize CanAssignCourseToProgram (else Forbid — no write)
[App] Validate code/name; duplicate check (pre-TX)
[TX BEGIN]
  Insert Course (ProgramId null) + SaveChanges     ← persist can fail
  AssignCourseToProgramAsync
    validate / load / rules                         ← assignment can fail → ROLLBACK
    [no-op?] return
    SaveChanges ProgramId                           ← persist can fail → ROLLBACK
    Dispatch CourseAssigned/Removed                 ← handler fail swallowed; other fail → ROLLBACK
    Invalidate hierarchy + statistics               ← can fail → ROLLBACK; Redis often swallowed
[TX COMMIT]                                         ← can fail → ROLLBACK
[App] Remove master:courses cache                   ← can fail AFTER commit
[App] LoadAsync                                     ← can fail AFTER commit
[API] Ok(row) | mapped 400/404 | unhandled → ProblemDetails
```

### 5.2 Update (EnablePrograms = true, programId specified)

Same TX shape: Code/Name SaveChanges then Assign. Omitted `programId` → Assign **not** called (Program unchanged).

### 5.3 Programs disabled

TX still wraps Course Code/Name persist; Assign **not** invoked from write service. No Program events/cache from this path.

### 5.4 Standalone assign-course

```
[API] CanAssignCourseToProgram
Assign… SaveChanges → events → cache invalidate
No ExecuteInTransactionAsync around the operation (verified)
```

---

## 6. Success / failure reporting gaps (discovery)

| Scenario | DB state | Side effects | API / UI risk |
|----------|----------|--------------|---------------|
| Assign validation fails inside TX | Rolled back | No events/cache if fail before those steps | Correct failure (400) |
| Event handler throws | Committed (if Commit reached) or rolled back only if uncaught before Commit | Handler fail does not fail op | Success with partial observability |
| Hierarchy/stats invalidate throws after SaveChanges, before Commit | Rolled back | Partial cache ops possible | Failure response; DB clean |
| Hierarchy/stats invalidate succeeds, Commit fails | Rolled back | Caches already cleared | Failure response; cache colder than DB |
| Master cache Remove / Load fails after Commit | **Committed** | Master cache may be stale or Load fails | **Failure response after successful persist** — UI must not treat as “nothing saved” without reload |
| Redis Remove fails (SmartCache) | Committed | Memory cleared; Redis may be stale | Success; possible stale Redis |

---

## 7. Existing infrastructure inventory (reuse only)

| Component | Role | Verified |
|-----------|------|----------|
| `IUnitOfWork.ExecuteInTransactionAsync` | DB TX for Course Master write service | Yes |
| `IAcademicStructureService.AssignCourseToProgramAsync` | Authoritative Program assignment | Yes |
| `CourseProgramAssignmentRules` | Pure decision / no-op / inactive retention | Yes |
| `IDomainEventDispatcher` / `DomainEventDispatcher` | In-process dispatch; handler errors swallowed | Yes |
| `CourseAssigned` / `CourseRemoved` | Existing event types | Yes |
| `IAcademicHierarchyCache` / `IAcademicStatisticsCache` | Hierarchy + stats invalidation | Yes |
| `ICacheService` (master courses key) | Course Master list cache | Yes |
| `GlobalExceptionHandler` | Unhandled → ProblemDetails | Yes |
| Outbox / message broker / DTC | **Not present** on this path | N/A |

---

## 8. Explicit non-claims

- Do **not** claim full end-to-end atomicity of DB + events + all caches.
- Do **not** claim event handler failures fail the HTTP operation (they do not).
- Do **not** claim standalone `assign-course` uses `ExecuteInTransactionAsync` (it does not in source).
- Do **not** claim Prompt 4A’s compensate/revert is still the Course Master mechanism — current write service uses a **verified** EF transaction instead.

---

## 9. Files inspected

- `Abhyanvaya.API/Controllers/CourseController.cs`
- `Abhyanvaya.API/Controllers/ProgramsController.cs` (assign-course)
- `Abhyanvaya.Application/Academic/CourseMasterWriteService.cs`
- `Abhyanvaya.Application/Academic/AcademicCatalogService.cs` (`AssignCourseToProgramAsync`, `InvalidateCachesAsync`)
- `Abhyanvaya.Application/Academic/CourseProgramAssignmentRules.cs`
- `Abhyanvaya.Application/Internal/DomainEventPublisher.cs`
- `Abhyanvaya.Infrastructure/DomainEvents/DomainEventDispatcher.cs`
- `Abhyanvaya.Infrastructure/Persistence/ApplicationDbContext.UnitOfWork.cs`
- `Abhyanvaya.Application/Academic/AcademicHierarchyCache.cs`
- `Abhyanvaya.Infrastructure/Services/SmartCacheService.cs`
- `Abhyanvaya.API/ExceptionHandling/GlobalExceptionHandler.cs`
- `docs/AI29_1D_24_PROMPT_4A_PERSISTENCE_HARDENING.md` (historical compensate model)

---

## 10. Discovery conclusion

Course Master Create/Update **does** have a real EF transaction boundary around Course persistence + Program assignment SaveChanges. Side effects (domain event dispatch, hierarchy/statistics invalidation) run **inside** that action before Commit; master list cache clear runs **after** Commit. Event **handlers** cannot fail the operation. Cache/Redis failures are only partially fail-closed. These boundaries are the constraints any further hardening must respect — without inventing Outbox, DTC, or Attendance/Scheduling changes.
