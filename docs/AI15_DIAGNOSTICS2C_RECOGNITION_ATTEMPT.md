# AI15.DIAGNOSTICS.2C — Recognition Attempt Tracking

**Status: IMPLEMENTED (diagnostics only — no retry behavior, no schema change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect

---

## 1. Objective

Introduce a `RecognitionAttempt` value (always `1` today) into the diagnostics surface, so a future
retry mechanism has an existing, already-wired place to plug in `Attempt = 2, 3, ...` without touching
every log call site again. **No retry implementation, database change, UI change, or business-logic
change is included in this milestone.**

## 2. Where the value comes from

```csharp
// IRecognitionExecutionContext.RecognitionAttempt (AI15.DIAGNOSTICS.2A)
int RecognitionAttempt { get; }
```

`ClassroomRecognitionBackgroundService` passes a single named constant into `Initialize(...)`:

```csharp
// AI15.DIAGNOSTICS.2C: always 1 today — no retry mechanism exists yet.
private const int CurrentRecognitionAttempt = 1;
...
executionContext.Initialize(message.AttendanceSessionId, message.TenantId, CurrentRecognitionAttempt, queueUtc);
```

Every downstream consumer (`ClassroomRecognitionPipeline`, `RecognitionPipelineDiagnostics`, the health
endpoint) reads `RecognitionAttempt` from the same scoped `IRecognitionExecutionContext` instance — it
is never independently computed or hardcoded a second time anywhere else.

## 3. Where `Attempt` now appears

Included in every location the "Execution Trace" sub-block was added for AI15.DIAGNOSTICS.2B (see
`docs/AI15_DIAGNOSTICS2B_PIPELINE_VERSION_TRACE.md` §3), because `Attempt` is one of the fields printed
by the same shared `ExecutionTraceLog.LogBlock(...)` helper:

- **Queue Trace** (background worker, at dequeue)
- **Pipeline Entry** (pipeline, at `ProcessAsync` entry)
- **Recognition Started / Recognition Completed** (`RecognitionPipelineDiagnostics.Begin`/`Complete`)
- **Recognition Memory Summary** (`RecognitionPipelineDiagnostics.Complete` → `LogMemorySummary`)
- **Failure Diagnostics** (`RecognitionPipelineDiagnostics.Fail`)
- **Health endpoint** — `RecognitionDiagnosticsSummary.RecognitionAttempt`, exposed as
  `recognitionDiagnostics.lastRecognition.recognitionAttempt` on `/health` and `/health/ready`.

## 4. "Recovery Summary" — deliberately not wired, and why

The request also asked for `Attempt` inside the **Recovery Summary** log (the `StuckAttendanceSessionRecoveryService`
sweep-completed log from AI14.RUNTIME.3). This was intentionally **not implemented**, for a concrete
reason worth recording rather than silently skipping:

- The recovery sweep operates on `AttendanceSession` rows that were **orphaned mid-processing** — by
  definition, the original `IRecognitionExecutionContext` for that job's failed attempt was already
  `Clear()`-ed (or the process crashed before `Clear()` ran) and its DI scope disposed long before the
  recovery sweep runs, often minutes later, in an entirely different scope/process instance.
- `RecognitionAttempt` is **not persisted anywhere** (no `AttendanceSession` column, no schema change —
  which AI15.DIAGNOSTICS.2C explicitly forbids). There is therefore no correct value the recovery
  sweep could read; the only options would be to fabricate a value (always `1`, which is misleading —
  a session recovered after multiple prior failed attempts would silently look identical to one
  recovered after its first) or to add persistence (out of scope and explicitly disallowed here).
- The recovery sweep also processes **many sessions per run** (`MaxRecoveriesPerRun`, AI14.RUNTIME.2) —
  it is a batch operation, not a single execution correlated by one `ExecutionTraceId`, so a single
  "Attempt" value on the sweep-level summary line would not even correspond to one job.

**Recommendation for a future milestone:** if attempt tracking becomes load-bearing (i.e., retries are
actually implemented), persist `RecognitionAttempt` on `AttendanceSession` at that point, and the
recovery sweep can then report the true last-known attempt per recovered session. Until retries exist,
adding it here would be diagnostic noise with a value that can never be verified against ground truth.

## 5. Constraints verified

- ✅ No retry mechanism implemented — `CurrentRecognitionAttempt` is a `const int = 1`, not a loop, not
  a policy, not configurable.
- ✅ No database/schema change — `AttendanceSession` and all other entities are untouched.
- ✅ No UI change.
- ✅ No business logic change — `RecognitionAttempt` is read but never branched on anywhere in
  recognition/matching/persistence code.
- ✅ Diagnostics-only — every new read of `RecognitionAttempt` feeds a log line or a health JSON field,
  nothing else.
- ✅ `dotnet build` — 0 errors.
