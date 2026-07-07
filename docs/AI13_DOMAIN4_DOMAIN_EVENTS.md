# AI13.DOMAIN.4 — Attendance Domain Events

## Objective

Introduce proper Domain Events for the `Attendance` aggregate so future features (notifications,
audit trail, parent SMS, faculty notifications, analytics, AI statistics, event sourcing) have a
stable, decoupled seam to plug into — **without changing any existing attendance behaviour, API
contract, UI, or database schema.**

This is a pure architectural enhancement. No endpoint changes shape or status codes, no response
DTOs changed, no migrations, no UI changes.

---

## 1. Existing Infrastructure Reused

The solution already had a lightweight domain-event foundation from the `AttendanceSession`
aggregate (`AttendanceApprovedEvent` / `AttendanceCompletedEvent` / `AttendanceCancelledEvent`).
AI13.DOMAIN.4 reuses it as-is and extends it with one additive capability (typed handler routing).

| Piece | Location | Status |
|---|---|---|
| `IDomainEvent` | `Abhyanvaya.Domain/Common/IDomainEvent.cs` | Reused, unchanged |
| `DomainEventBase` | `Abhyanvaya.Domain/Common/DomainEventBase.cs` | Reused, unchanged |
| `IHasDomainEvents` | `Abhyanvaya.Domain/Common/IHasDomainEvents.cs` | Reused, unchanged |
| `BaseEntity` (implements `IHasDomainEvents`) | `Abhyanvaya.Domain/Common/BaseEntity.cs` | Reused, unchanged |
| `IDomainEventDispatcher` | `Abhyanvaya.Application/Common/Interfaces/IDomainEventDispatcher.cs` | Reused, **interface unchanged** |
| `DomainEventPublisher.DispatchAndClearAsync` | `Abhyanvaya.Application/Internal/DomainEventPublisher.cs` | Reused, unchanged |
| `DomainEventDispatcher` (infra impl) | `Abhyanvaya.Infrastructure/DomainEvents/DomainEventDispatcher.cs` | **Extended** (see §3) |

### New pieces added

| Piece | Location | Purpose |
|---|---|---|
| `IDomainEventHandler<TEvent>` | `Abhyanvaya.Application/Common/Interfaces/IDomainEventHandler.cs` | Typed, logging-only observer contract |
| 5 event records | `Abhyanvaya.Domain/Events/AttendanceEvents.cs` | The Attendance aggregate's domain events |
| 5 handler classes | `Abhyanvaya.Infrastructure/DomainEvents/Handlers/AttendanceDomainEventHandlers.cs` | Structured logging only |

---

## 2. The Five Domain Events

All events live in `Abhyanvaya.Domain.Events`, derive from `DomainEventBase` (which supplies
`OccurredUtc`), and are immutable `sealed record` types — consistent with the existing
`AttendanceSessionEvents.cs` convention.

```csharp
public sealed record AttendanceMarkedEvent(
    int TenantId, int SubjectId, AttendanceDay AttendanceDay,
    int StudentCount, AttendanceMethod CaptureMethod, int? MarkedByUserId) : DomainEventBase;

public sealed record AttendanceGeneratedFromAIEvent(
    Guid AttendanceSessionId, int TenantId, int SubjectId, AttendanceDay AttendanceDay,
    int StudentCount, int PresentCount, int AbsentCount, AttendanceMethod CaptureMethod) : DomainEventBase;

public sealed record AttendanceFinalizedEvent(
    Guid AttendanceSessionId, int TenantId, int SubjectId, AttendanceDay AttendanceDay,
    int StudentCount, int PresentCount, int AbsentCount, AttendanceMethod CaptureMethod) : DomainEventBase;

public sealed record AttendanceLockedEvent(
    int TenantId, int SubjectId, AttendanceDay AttendanceDay,
    int StudentCount, int? LockedByUserId) : DomainEventBase;

public sealed record AttendanceUnlockedEvent(
    int TenantId, int SubjectId, AttendanceDay AttendanceDay,
    int StudentCount, int? UnlockedByUserId) : DomainEventBase;
```

Events carry the `AttendanceDay` value object (AI13.DOMAIN.2/.3) directly rather than a raw
`DateTime` — consumers get the canonical local date, reporting time zone, and UTC range for free,
with no re-derivation.

### Why both `AttendanceGeneratedFromAIEvent` and `AttendanceFinalizedEvent`?

They represent two different facts that happen to occur at the same moment today, on purpose kept
distinct for future extensibility:

- **`AttendanceGeneratedFromAIEvent`** — "the AI recognition pipeline materialized N present/absent
  rows for this session." AI-pipeline-specific (carries `AttendanceSessionId`); useful for AI
  adoption/accuracy analytics.
- **`AttendanceFinalizedEvent`** — "this session's attendance is now official/immutable." Capture-
  method-agnostic in intent; any future finalization workflow that goes through
  `AttendanceSessionFinalizer` (not just AI/recognition-based ones) will raise it too, so downstream
  consumers (notifications, audit) can subscribe to one generic "attendance is final" signal instead
  of caring which capture method produced it.

### Why is `AttendanceUnlockedEvent` defined but not published anywhere?

Grep of the entire solution confirms there is **no "unlock attendance" capability today** — no
endpoint, no service method flips `Attendance.IsLocked` back to `false`. The task requires "no API
changes," so adding an unlock endpoint solely to raise this event was out of scope. The event type
is defined now (stable contract, ready for handlers/consumers) and documented with an XML `<remarks>`
note; wire it up from the future unlock endpoint/service when that capability ships.

---

## 3. Dispatch / Handler Infrastructure

The pre-existing `DomainEventDispatcher` only logged the event type name generically ("Domain event
dispatched: {Type}") — there were no handlers to invoke, so structured per-field logging (Tenant,
Subject, AttendanceDay, Student Count, Capture Method) was not possible without extending it.

`IDomainEventHandler<TEvent>` was added as a minimal typed-observer contract:

```csharp
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
```

`DomainEventDispatcher.DispatchAsync` keeps its **exact original behaviour** (the generic log line
for every event) and additionally resolves `IEnumerable<IDomainEventHandler<TConcreteEventType>>`
from DI (via `IServiceProvider.GetServices` with a closed generic built from the event's runtime
type) and invokes each one. A handler exception is caught and logged, never rethrown — a logging
observer must never fail the business operation that already committed to the database.

```
DomainEventDispatcher.DispatchAsync(events)
 ├─ foreach event:
 │   ├─ generic log line (unchanged, always runs)
 │   └─ resolve IDomainEventHandler<TEvent> from DI, invoke each (try/catch, log-only on failure)
```

Events with zero registered handlers behave **exactly as before this change** (generic log line
only) — this is why the three pre-existing `AttendanceSession` events (`AttendanceApprovedEvent`,
`AttendanceCompletedEvent`, `AttendanceCancelledEvent`) are unaffected; they still have no handlers
registered and their dispatch is byte-for-byte the same as before AI13.DOMAIN.4.

### Handlers (logging-only, no business logic)

Five handler classes in `Abhyanvaya.Infrastructure/DomainEvents/Handlers/AttendanceDomainEventHandlers.cs`,
one per event. Each does nothing but a single structured `ILogger.LogInformation` call with the
fields the task specified (Tenant, Subject, AttendanceDay, Student Count, Capture Method where
applicable, Timestamp via `OccurredUtc`), e.g.:

```csharp
public sealed class AttendanceMarkedEventHandler : IDomainEventHandler<AttendanceMarkedEvent>
{
    public Task HandleAsync(AttendanceMarkedEvent e, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AttendanceMarked: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} " +
            "StudentCount={StudentCount} CaptureMethod={CaptureMethod} MarkedByUserId={MarkedByUserId} OccurredUtc={OccurredUtc}",
            e.TenantId, e.SubjectId, e.AttendanceDay, e.StudentCount, e.CaptureMethod, e.MarkedByUserId, e.OccurredUtc);
        return Task.CompletedTask;
    }
}
```

`AttendanceLockedEvent`/`AttendanceUnlockedEvent` omit `CaptureMethod` — locking is a workflow
action, not a capture channel, so there is no meaningful value to log for that field. All five log
`Tenant`, `Subject`, `AttendanceDay`, `StudentCount`, and `OccurredUtc` (Timestamp).

Registered in `Abhyanvaya.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<IDomainEventHandler<AttendanceMarkedEvent>, AttendanceMarkedEventHandler>();
services.AddScoped<IDomainEventHandler<AttendanceGeneratedFromAIEvent>, AttendanceGeneratedFromAIEventHandler>();
services.AddScoped<IDomainEventHandler<AttendanceFinalizedEvent>, AttendanceFinalizedEventHandler>();
services.AddScoped<IDomainEventHandler<AttendanceLockedEvent>, AttendanceLockedEventHandler>();
services.AddScoped<IDomainEventHandler<AttendanceUnlockedEvent>, AttendanceUnlockedEventHandler>();
```

---

## 4. Architecture Diagram

```
┌─────────────────────────┐        ┌──────────────────────────┐
│   AttendanceController   │        │  AttendanceSessionFinalizer │
│  (manual mark / lock)    │        │  (AI recognition approval)  │
└───────────┬──────────────┘        └──────────────┬───────────┘
            │ after SaveChangesAsync succeeds        │ after SaveChangesAsync succeeds
            │ (direct dispatch — no aggregate         │ (session.AddDomainEvent, existing
            │  instance owns the batch operation)     │  DispatchAndClearAsync call site)
            ▼                                         ▼
   ┌───────────────────────────────────────────────────────────┐
   │            IDomainEventDispatcher.DispatchAsync             │
   │  (Abhyanvaya.Infrastructure.DomainEvents.DomainEventDispatcher) │
   └───────────────────────────┬───────────────────────────────┘
                                │ 1. generic log line (always)
                                │ 2. resolve IDomainEventHandler<T> from DI
                                ▼
        ┌───────────────────────────────────────────────────┐
        │ AttendanceMarkedEventHandler                        │
        │ AttendanceLockedEventHandler                        │
        │ AttendanceGeneratedFromAIEventHandler                │
        │ AttendanceFinalizedEventHandler                     │
        │ AttendanceUnlockedEventHandler (defined, unwired)   │
        │      — structured logging only, no side effects —  │
        └───────────────────────────────────────────────────┘
                                │
                                ▼ (future — not built now)
        ┌───────────────────────────────────────────────────┐
        │ NotificationHandler │ AuditLogHandler │ SmsHandler  │
        │ AnalyticsHandler    │ EventSourcingProjector        │
        └───────────────────────────────────────────────────┘
```

---

## 5. Event Flow — Where Each Event Is Raised

| Event | Raised from | Trigger point | Publish mechanism |
|---|---|---|---|
| `AttendanceMarkedEvent` | `AttendanceController.MarkAttendance` | After `SaveChangesAsync` succeeds, only if ≥1 row was actually inserted | Direct `IDomainEventDispatcher.DispatchAsync([...])` |
| `AttendanceLockedEvent` | `AttendanceController.LockAttendance` | After `SaveChangesAsync` succeeds (records locked) | Direct `IDomainEventDispatcher.DispatchAsync([...])` |
| `AttendanceGeneratedFromAIEvent` | `AttendanceSessionFinalizer.FinalizeAttendanceSessionAsync` | After `ConcurrencyExceptionHelper.SaveChangesAsync` succeeds, right before the pre-existing `DomainEventPublisher.DispatchAndClearAsync(session, ...)` call | `session.AddDomainEvent(...)` → existing dispatch call |
| `AttendanceFinalizedEvent` | `AttendanceSessionFinalizer.FinalizeAttendanceSessionAsync` | Same point as above (alongside `AttendanceGeneratedFromAIEvent` and the pre-existing `AttendanceApprovedEvent`) | `session.AddDomainEvent(...)` → existing dispatch call |
| `AttendanceUnlockedEvent` | *(none — no unlock capability exists yet)* | N/A | N/A |

### Why events are raised where they are (design rationale)

- **Controller-level events (`Marked`, `Locked`)**: `MarkAttendance`/`LockAttendance` create or
  mutate *multiple* `Attendance` rows in one HTTP call; there is no single aggregate instance that
  naturally "owns" a batch operation like this the way `AttendanceSession` owns its own lifecycle.
  Rather than force an artificial `AddDomainEvent` call onto one arbitrary `Attendance` row, the
  controller calls `IDomainEventDispatcher.DispatchAsync` directly — the interface accepts any
  `IReadOnlyCollection<IDomainEvent>`, it does not require going through `IHasDomainEvents`. This is
  dispatched **after** `SaveChangesAsync` returns successfully, so an event is only ever published
  for data that is actually committed.
- **Finalizer-level events (`GeneratedFromAI`, `Finalized`)**: `AttendanceBuilder.BuildAsync` (which
  actually creates the `Attendance` rows) runs *inside* the same database transaction as
  `AttendanceSessionFinalizer`'s later `session.Approve()` + `SaveChangesAsync` call, and it re-reads
  `AttendanceSession` with `AsNoTracking()` — a different in-memory instance than the tracked
  `session` the finalizer holds. Raising events inside `BuildAsync` itself would risk publishing an
  event for data that could still be rolled back if a later step in the same transaction fails, and
  the event couldn't be attached to the finalizer's own tracked `session` instance anyway (different
  object). Raising both events on the finalizer's tracked `session` immediately before its **existing,
  already-safe** `DispatchAndClearAsync` call (which only ever runs after `SaveChangesAsync` has
  already succeeded) reuses a proven-safe pattern with zero new risk, and requires no change to
  `AttendanceBuilder`'s already-complex method.

---

## 6. Future Extensibility

Adding a new consumer for any of these events (e.g. parent SMS on `AttendanceMarkedEvent`, or an
audit-log projector on all five) requires:

1. Implement `IDomainEventHandler<TEvent>` for the event(s) of interest.
2. Register it in DI: `services.AddScoped<IDomainEventHandler<TEvent>, MyHandler>();`

No change to `AttendanceController`, `AttendanceSessionFinalizer`, `DomainEventDispatcher`, or any
existing handler is required — multiple handlers can be registered for the same event type (DI
resolves `IEnumerable<IDomainEventHandler<TEvent>>`), so today's logging handler and a future
notification handler can coexist independently. This is also the seam for event sourcing later: a
projector handler could append each event to an append-only store without touching the
attendance-writing code paths at all.

---

## 7. Regression Analysis

| Area | Impact |
|---|---|
| API contracts (routes, request/response shapes, status codes) | **None.** No controller signatures, routes, or response bodies changed. |
| Database schema | **None.** No migration, no new tables/columns. |
| UI | **None.** No frontend files touched. |
| Existing `AttendanceSession` events (`Approved`/`Completed`/`Cancelled`) | **None.** Dispatch behaviour for events with no registered handler is unchanged (generic log line only, same as before). |
| `AttendanceBuilder.BuildAsync` | **None.** Signature and internal logic untouched; the AI event is raised by its caller (`AttendanceSessionFinalizer`), not by the builder. |
| Performance | Negligible. Domain event dispatch was already awaited synchronously in the finalizer's transaction; the added work per request is 1–2 `record` allocations, a DI service-collection resolution (typically 0–1 registered handlers), and one structured log call — no I/O, no extra queries, no extra database round-trips. |
| Manual attendance save / lock behavior | **None.** Events are raised strictly *after* `SaveChangesAsync` returns; if dispatch or a handler throws, it is caught and logged inside `DomainEventDispatcher` and never surfaces to the HTTP response (the attendance data is already safely persisted by that point). |

## 8. Risk Assessment

| Risk | Mitigation |
|---|---|
| A future handler with real side effects (e.g. calling an external SMS API) could throw and be swallowed silently by the dispatcher's catch-all. | Acceptable for now — today's handlers are logging-only per the task's explicit constraint ("NO business logic"). The dispatcher logs handler failures at `Error` level with full exception detail, so failures are visible in logs even though they don't propagate. |
| `IServiceProvider.GetServices(Type)` reflection-based resolution adds a small per-event overhead. | Negligible — attendance write endpoints are already dominated by database round-trips; a handful of DI lookups per request is not measurable in this workload. |
| `AttendanceUnlockedEvent` exists but is never raised, risking confusion if a developer expects it to fire. | Explicitly documented via XML `<remarks>` on the record and in this document. No handler subscribes to production behaviour that doesn't exist. |
| Adding `IDomainEventDispatcher` to `AttendanceController` increases its constructor surface. | Consistent with the existing DI-heavy style of this controller (5 constructor dependencies total); no circular dependency risk since `IDomainEventDispatcher` has no dependency back on any controller-layer type. |

## 9. Read-Only Verification

- Full solution build: **`dotnet build Abhyanvaya.sln` → Build succeeded, 0 Error(s)** (42 pre-existing
  nullable-reference warnings, unrelated to this change, unchanged in count).
- Confirmed via `grep` that no existing call site of `IDomainEventDispatcher.DispatchAsync` or
  `DomainEventPublisher.DispatchAndClearAsync` was altered — only new call sites were added.
- Confirmed the three pre-existing `AttendanceSession` domain events have no registered
  `IDomainEventHandler<T>` implementation, so their dispatch path is provably unchanged bit-for-bit.

---

## 10. Files Created / Modified

**Created**

- `Abhyanvaya.Domain/Events/AttendanceEvents.cs` — the 5 event records
- `Abhyanvaya.Application/Common/Interfaces/IDomainEventHandler.cs` — typed handler contract
- `Abhyanvaya.Infrastructure/DomainEvents/Handlers/AttendanceDomainEventHandlers.cs` — 5 logging-only handlers
- `docs/AI13_DOMAIN4_DOMAIN_EVENTS.md` — this document

**Modified**

- `Abhyanvaya.Infrastructure/DomainEvents/DomainEventDispatcher.cs` — added typed-handler resolution/invocation (generic logging behaviour preserved)
- `Abhyanvaya.Infrastructure/DependencyInjection.cs` — registered the 5 handlers
- `Abhyanvaya.API/Controllers/AttendanceController.cs` — injected `IDomainEventDispatcher`; raise `AttendanceMarkedEvent` in `MarkAttendance`, `AttendanceLockedEvent` in `LockAttendance`
- `Abhyanvaya.Application/AttendanceSessionFinalizer.cs` — injected `IAttendanceCalendar`; raise `AttendanceGeneratedFromAIEvent` and `AttendanceFinalizedEvent` alongside the existing `AttendanceApprovedEvent` dispatch
