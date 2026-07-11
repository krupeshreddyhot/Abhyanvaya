# AI15.DIAGNOSTICS.2A — Scoped Recognition Execution Context

**Status: IMPLEMENTED (architectural improvement only — no behavior change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect

---

## 1. Objective

Replace the previously *proposed* (never implemented) `AsyncLocal<ExecutionTraceContext>` design for
correlating logs across one classroom recognition job with a **Scoped, dependency-injected** execution
context — the same architectural pattern already in production for `ITenantContextAccessor`. No
recognition, matching, queue, or business logic changes.

## 2. Why Scoped instead of `AsyncLocal`

| Concern | `AsyncLocal<T>` | Scoped DI service (chosen) |
|---|---|---|
| State ownership | Ambient, flows implicitly with the async call chain | Explicit — owned by the DI scope the caller controls |
| Multiple concurrent workers | Works, but relies on every hop actually awaiting on the same flow | Each worker's scope is independent by construction |
| Parallel per-face processing (roadmap) | Risky — child `Task.Run`/`Parallel.ForEach` bodies capture the *same* ambient value unless explicitly re-scoped | A new child scope per parallel unit gets its own instance for free |
| Distributed queues (Hangfire/Azure Queue) | No async flow crosses process/worker boundaries — `AsyncLocal` is meaningless there | Irrelevant either way; correlation would be re-`Initialize`d per dequeue regardless of transport |
| Testability | Ambient state is awkward to isolate in unit tests | Ordinary constructor-injected dependency — trivially mockable |
| Existing precedent in this codebase | None | `ITenantContextAccessor` already does exactly this for tenant binding |

`AsyncLocal` was never actually implemented (the prior turn only produced a design). This milestone
proceeds directly to the Scoped design — there is no `AsyncLocal` code to remove.

## 3. Contract

`Abhyanvaya.Application/Common/Interfaces/IRecognitionExecutionContext.cs`:

```csharp
public interface IRecognitionExecutionContext
{
    Guid ExecutionTraceId { get; }
    Guid AttendanceSessionId { get; }
    int TenantId { get; }
    DateTime QueueStartUtc { get; }
    DateTime PipelineStartUtc { get; }
    int RecognitionAttempt { get; }

    void Initialize(Guid sessionId, int tenantId, int attempt, DateTime queueUtc);
    void MarkPipelineStarted();
    void Clear();
}
```

Implemented exactly as specified — no additional members were added to the interface (see §5 for how
`PipelineVersion`, a 2B concern, is deliberately kept out of this contract).

`ExecutionTraceId` is a `Guid` (per the interface as specified), minted fresh by `Initialize`. Every log
site that wants the human-readable `TRACE-yyyyMMdd-HHmmss-XXXXXXXX` form calls the shared
`ExecutionTraceLog.FormatTraceId(context)` helper, which derives that display string from
`ExecutionTraceId` + `QueueStartUtc` rather than storing two redundant identifiers on the context.

## 4. Implementation

`Abhyanvaya.Infrastructure/Services/RecognitionExecutionContext.cs` — mirrors
`Abhyanvaya.Infrastructure/Services/TenantContextAccessor.cs` line for line in spirit:

- All state lives in **private instance fields**, guarded by a single `lock (_gate)`.
- **No static fields. No `AsyncLocal<T>`. No `[ThreadStatic]`.**
- Registered as **Scoped**:

  ```csharp
  services.AddScoped<IRecognitionExecutionContext, RecognitionExecutionContext>();
  ```

  in `Abhyanvaya.Infrastructure/DependencyInjection.cs`, immediately below the existing
  `IRecognitionPipelineDiagnostics` registration.

- `MarkPipelineStarted()` defensively no-ops into "now" if called before `Initialize` (diagnostics must
  never throw into the pipeline) rather than leaving `PipelineStartUtc` at `DateTime.MinValue`, which
  would otherwise produce a nonsensical multi-year "elapsed" value in logs.
- `Clear()` resets every field to its default, so a future scope-pooling change (none exists today)
  could never leak one job's identifiers into the next.

## 5. Usage — exactly the diagram in the request

```
ClassroomRecognitionBackgroundService.ExecuteAsync
  await foreach message in queue
    │
    ├─ scope = _scopeFactory.CreateAsyncScope()
    ├─ tenantContext = scope.Resolve<ITenantContextAccessor>();      tenantContext.SetTenant(...)
    ├─ executionContext = scope.Resolve<IRecognitionExecutionContext>();
    ├─ executionContext.Initialize(sessionId, tenantId, attempt=1, queueUtc)
    ├─ LogQueueTrace(...)                                            ← AI15.DIAGNOSTICS.2B/2C fields
    │
    ├─ pipeline = scope.Resolve<IClassroomRecognitionPipeline>();
    ├─ ENTERING PIPELINE
    ├─ await pipeline.ProcessAsync(message, ct)
    │     └─ ClassroomRecognitionPipeline.ProcessAsync
    │           ├─ executionContext.MarkPipelineStarted()   (same scope, same instance)
    │           ├─ LogPipelineEntry(...)
    │           ├─ _diagnostics.Begin(...)  → RecognitionPipelineDiagnostics reads the same
    │           │                              scoped executionContext for ExecutionTraceId/Attempt
    │           └─ ... existing AI15.DIAGNOSTICS.1 stage instrumentation, unchanged ...
    ├─ PIPELINE COMPLETED / PIPELINE FAILED
    │
    └─ finally
          tenantContext.Clear();
          executionContext.Clear();
```

`ClassroomRecognitionPipeline` and `RecognitionPipelineDiagnostics` both receive
`IRecognitionExecutionContext` through ordinary constructor injection — because all three
(`ClassroomRecognitionPipeline`, `IRecognitionPipelineDiagnostics`, `IRecognitionExecutionContext`) are
registered **Scoped** and resolved from the **same** `scope.ServiceProvider`, they transparently share
the one instance the background worker initialized. No parameter threading, no ambient state, no
service-locator anti-pattern beyond what `ClassroomRecognitionBackgroundService` already does today for
`ITenantContextAccessor` and `IClassroomRecognitionPipeline`.

## 6. Files changed

| File | Change |
|---|---|
| `Abhyanvaya.Application/Common/Interfaces/IRecognitionExecutionContext.cs` | **New.** Interface, as specified. |
| `Abhyanvaya.Infrastructure/Services/RecognitionExecutionContext.cs` | **New.** Scoped implementation. |
| `Abhyanvaya.Infrastructure/Diagnostics/ExecutionTraceLog.cs` | **New.** Shared trace-id formatting + "Execution Trace" log sub-block, reused by the background worker, the pipeline, and `RecognitionPipelineDiagnostics` — a single source of truth for the display format. |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Registers `IRecognitionExecutionContext` as Scoped. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/ClassroomRecognitionBackgroundService.cs` | Resolves + `Initialize`s + `Clear`s the context per job; adds Queue Trace / Entering-Pipeline / Pipeline-Completed / Pipeline-Failed log lines. |
| `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs` | Injects the context; calls `MarkPipelineStarted()` + logs "Pipeline Entry" at the top of `ProcessAsync`, before the session is even loaded. |
| `Abhyanvaya.Infrastructure/Diagnostics/RecognitionPipelineDiagnostics.cs` | Injects the context (read-only consumer) to append the "Execution Trace" sub-block to the existing Recognition Started/Completed, Memory Summary, and Failure Diagnostics logs. |

## 7. Constraints verified

- ✅ No static state anywhere in the new code.
- ✅ No `AsyncLocal`, no `[ThreadStatic]`.
- ✅ `RecognitionExecutionContext` is registered **Scoped**, not Singleton.
- ✅ All existing AI15.DIAGNOSTICS.1 log formats are byte-for-byte unchanged — every new line is
  *appended after* an existing box, never inserted into or reformatting one.
- ✅ Recognition/matching/queue-dequeue/business logic untouched — every new call is a read-only
  snapshot + `_logger.LogInformation`/`LogWarning`/`LogError` call wrapped in its own try/catch that
  never rethrows.
- ✅ No public API (controllers/DTOs) changes.
- ✅ `dotnet build` — 0 errors.

## 8. Overhead

- `Initialize`/`MarkPipelineStarted`/`Clear`: one `lock` + a handful of field writes each — sub-microsecond.
- One extra `Guid.NewGuid()` per job (16 bytes, stack-negligible).
- The new log lines (Queue Trace, Pipeline Entry, Execution Trace sub-blocks) are bookend/summary-level
  only — they do **not** fire per-face or per-stage, so their cost does not scale with image size or
  face count. See `docs/AI15_DIAGNOSTICS2_ARCHITECTURE_REVIEW.md` for the consolidated overhead
  estimate across 2A/2B/2C.
