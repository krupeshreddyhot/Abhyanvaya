# AI29.1D.24 Prompt 4B.3 — Event and Cache Consistency

## Scope

Verify **actual** domain-event dispatch and hierarchy/statistics cache invalidation for Course → Program assignment transitions. No new event types or cache services.

## Method

Integration-style tests against `AcademicCatalogService.AssignCourseToProgramAsync` with:

- Moq spies on `IDomainEventDispatcher`, `IAcademicHierarchyCache`, `IAcademicStatisticsCache`
- Captured dispatched event instances (`CourseAssigned` / `CourseRemoved`)
- Real `DomainEventDispatcher` + throwing `IDomainEventHandler<CourseAssigned>` for post-persist handler failure

Tests live in `AI29_1D_24_Prompt4B3_EventCacheConsistencyTests.cs`.

## Results

| Transition | Assignment events | Removal events | Hierarchy invalidate | Statistics invalidate |
|------------|-------------------|----------------|----------------------|-----------------------|
| Commerce → Commerce | **0** | **0** | **0** | **0** |
| Commerce → Science | **1** `CourseAssigned` (Science) | **0** | **1** | **1** |
| Commerce → null | **0** | **1** `CourseRemoved` (Commerce) | **1** | **1** |

Commerce → Commerce also performs **no** Assign `SaveChanges` (no-op early return).

## Order inside Assign (non-no-op)

Verified in `AcademicCatalogService.AssignCourseToProgramAsync`:

1. `SaveChangesAsync` (`Course.ProgramId`)
2. `DomainEventPublisher.DispatchAndClearAsync` → `IDomainEventDispatcher.DispatchAsync`
3. `InvalidateHierarchyAsync` + statistics `InvalidateAsync`

## Event handler failure after database persistence

**Observed / contracted behavior** (`DomainEventDispatcher`):

- Handlers run **after** Assign’s `SaveChangesAsync`.
- Each handler is wrapped in `try/catch`; failures are **logged and swallowed**.
- Assign **completes successfully**; `Course.ProgramId` remains the new value.
- Hierarchy/statistics invalidation **still runs** after dispatch returns (handler swallow does not abort Assign).

**Therefore:**

- Do **not** claim that a failed event handler rolls back Program assignment.
- Do **not** claim full transactional atomicity of DB + handler side effects.
- Course Master outer `ExecuteInTransactionAsync` can still roll back DB if an **uncaught** exception escapes Assign before Commit; handler exceptions do **not** qualify because they are swallowed.

## Non-goals

- No new events / caches
- No Attendance, Scheduling, Section, Subject Master, or Allocation changes
- No Outbox / broker

## Files

- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_24_Prompt4B3_EventCacheConsistencyTests.cs`
- `docs/AI29_1D_24_PROMPT_4B3_EVENT_CACHE_CONSISTENCY.md`
- Related production: `AcademicCatalogService.AssignCourseToProgramAsync`, `DomainEventDispatcher`
