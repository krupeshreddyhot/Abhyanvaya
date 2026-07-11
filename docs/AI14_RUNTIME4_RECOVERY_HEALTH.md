# AI14.RUNTIME.4 — Recovery Health Metrics

**Status: IMPLEMENTED**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

## 1. Objective

Expose `StuckAttendanceSessionRecoveryService`'s runtime recovery statistics through the **existing**
`/health` and `/health/ready` endpoints and the startup diagnostics summary — no new endpoints, no
persistence, runtime counters only.

---

## 2. Runtime counters

New `IAttendanceSessionRecoveryMetrics` (Application layer interface) /
`AttendanceSessionRecoveryMetrics` (Infrastructure singleton implementation), following the exact
same pattern as the existing `IEmbeddingGenerationMetrics` / `EmbeddingGenerationMetrics` (AI-11/12
embedding observability) — a thread-safe, in-process, singleton counter with no backing store:

```csharp
public interface IAttendanceSessionRecoveryMetrics
{
    void RecordRun(int recoveredCount, int pendingRemaining, long durationMs);
    AttendanceSessionRecoveryMetricsSnapshot GetSnapshot();
}

public sealed record AttendanceSessionRecoveryMetricsSnapshot(
    long RecoveryRuns,
    long RecoveredSessions,
    DateTime? LastRecoveryUtc,
    long? LastRecoveryDurationMs,
    int PendingRecoveries);
```

| Counter | Updated | Semantics |
|---|---|---|
| `RecoveryRuns` | Every sweep execution (even when zero candidates are found). | Total number of times the sweep has run since process start. |
| `RecoveredSessions` | Every run, incremented by that run's recovered count. | Cumulative sessions recovered since process start. |
| `LastRecoveryUtc` | Only on a run that recovered ≥1 session. | When a session was **last actually recovered** — not merely when the timer last ticked. `null` until the first recovery. |
| `LastRecoveryDuration` | Only on a run that recovered ≥1 session. | Wall-clock duration of that last recovery-performing run. `null` until the first recovery. |
| `PendingRecoveries` | Every run, set to that run's `remaining` count. | Current known backlog of orphaned sessions beyond the `MaxRecoveriesPerRun` cap (AI14.RUNTIME.2), as of the most recent scan. |

All fields use `Interlocked`/`Volatile` operations only — no locks, no database, no file I/O. Counters
reset to zero/null on process restart, which is intentional: they describe *this instance's* runtime
behavior, matching the same "no persistence" model already used by the in-memory recognition queues
and `IEmbeddingGenerationMetrics`.

### Why `LastRecoveryUtc` only updates on an actual recovery

The alternative reading — "last time the sweep ran at all" — is already covered by
`RecoveryRuns` growing every `ScanIntervalSeconds`, and would make `LastRecoveryUtc` nearly useless for
support (it would almost always just be "a few seconds ago"). Tracking "last time we actually had to
intervene" is the operationally meaningful signal, consistent with the field name and with how an
on-call engineer would use it ("has this happened recently, and how long did the last one take?").

`StuckAttendanceSessionRecoveryService.RecordRun` call sites:

```csharp
if (candidatesFound == 0)
{
    LogSweepCompleted(candidatesFound, recovered: 0, remaining: 0, stopwatch.Elapsed);
    _metrics.RecordRun(recoveredCount: 0, pendingRemaining: 0, (long)stopwatch.Elapsed.TotalMilliseconds);
    return;
}
...
var remaining = Math.Max(0, candidatesFound - batch.Count);
LogSweepCompleted(candidatesFound, recoveredCount, remaining, stopwatch.Elapsed);
_metrics.RecordRun(recoveredCount, remaining, (long)stopwatch.Elapsed.TotalMilliseconds);
```

Registered as a singleton in `DependencyInjection.cs`:

```csharp
services.AddSingleton<IAttendanceSessionRecoveryMetrics, AttendanceSessionRecoveryMetrics>();
```

---

## 3. Health endpoint exposure

Both endpoints call the same helper, `BuildRecoverySnapshot`, so `/health` and `/health/ready` can
never disagree about recovery status (same pattern already used for
`ToConfigurationIssueSummaries`/`ModelAvailabilityChecker`):

```csharp
static object BuildRecoverySnapshot(
    AttendanceSessionRecoveryOptions recoveryOptions,
    AttendanceSessionRecoveryMetricsSnapshot metrics)
{
    var status = !recoveryOptions.Enabled
        ? "Disabled"
        : metrics.PendingRecoveries > 0
            ? "Degraded"
            : "Healthy";

    return new
    {
        status,
        runs = metrics.RecoveryRuns,
        recoveredSessions = metrics.RecoveredSessions,
        lastRecoveryUtc = metrics.LastRecoveryUtc,
        lastDurationMs = metrics.LastRecoveryDurationMs,
        pendingRecoveries = metrics.PendingRecoveries,
    };
}
```

### `GET /health/ready`

Added as a new key inside the existing `checks` extension (alongside `database`, `storage`,
`configurationIssues`, etc.) — no new top-level extension, no new endpoint:

```json
{
  "checks": {
    "database": "Reachable",
    "storage": "Reachable",
    "modelsPresent": true,
    "recognitionWorkerStarted": true,
    "embeddingWorkerStarted": true,
    "queueAvailable": true,
    "configurationIssues": { "critical": 0, "warning": 0, "information": 0, "items": [] },
    "recovery": {
      "status": "Healthy",
      "runs": 18,
      "recoveredSessions": 4,
      "lastRecoveryUtc": "2026-07-11T08:35:42Z",
      "lastDurationMs": 78,
      "pendingRecoveries": 0
    }
  }
}
```

### `GET /health`

Added as a new field on the existing `health` snapshot object (alongside `recognitionWorker`,
`embeddingWorker`, `backgroundQueue`, `cache`, etc.):

```json
{
  "health": {
    "...": "...",
    "recovery": {
      "status": "Healthy",
      "runs": 18,
      "recoveredSessions": 4,
      "lastRecoveryUtc": "2026-07-11T08:35:42Z",
      "lastDurationMs": 78,
      "pendingRecoveries": 0
    },
    "overallStatus": "Healthy"
  }
}
```

### `status` derivation

- `"Disabled"` — `AttendanceSessionRecovery:Enabled` is `false`; the sweep never runs and orphaned
  sessions will not be auto-recovered.
- `"Degraded"` — the sweep is enabled and running, but its most recent scan found more orphaned
  sessions than `MaxRecoveriesPerRun` could clear in one pass (`pendingRecoveries > 0`); the backlog
  is being worked down but hasn't cleared yet.
- `"Healthy"` — the sweep is enabled and, as of its most recent scan, has no known backlog.

`recovery.status` is **metadata only**: consistent with how `configurationIssues` is already treated,
it does not factor into `/health/ready`'s `isReady` boolean or `/health`'s `overallStatus`. A
degraded/backlogged recovery sweep does not, by itself, mean the platform is not ready to serve
traffic or process new recognition jobs.

---

## 4. Startup summary

`Program.cs`'s `LogStartupConfigurationSummary` now logs a `Recovery Service` block, reading directly
from the same `IOptions<AttendanceSessionRecoveryOptions>` the background service itself binds (no
duplicate configuration reads, no hardcoded values):

```
Recovery Service
  Enabled                             : Yes
  Scan Interval                       : 60 seconds
  Recovery Timeout                    : 10 minutes
  Maximum Recoveries                  : 100
```

```csharp
static void LogRecoveryServiceConfiguration(ILogger logger, AttendanceSessionRecoveryOptions recoveryOptions)
{
    logger.LogInformation("Recovery Service");
    logger.LogInformation("  Enabled                             : {Enabled}", recoveryOptions.Enabled ? "Yes" : "No");
    logger.LogInformation("  Scan Interval                       : {ScanIntervalSeconds} seconds", recoveryOptions.ScanIntervalSeconds);
    logger.LogInformation("  Recovery Timeout                    : {TimeoutMinutes} minutes", recoveryOptions.TimeoutMinutes);
    logger.LogInformation("  Maximum Recoveries                  : {MaxRecoveriesPerRun}", recoveryOptions.MaxRecoveriesPerRun);
}
```

Placed after the `Recognition Queue` line and before `Started At UTC`, alongside the other
SaaS-deployment-metadata lines in the startup summary.

---

## 5. Constraints verification

- **Extend existing health endpoints only.** `recovery` was added as a new key/field inside the
  existing `checks` dictionary (`/health/ready`) and the existing `health` snapshot object
  (`/health`) — no new route was mapped.
- **No new endpoints.** Confirmed — zero new `app.Map*` calls.
- **No persistence required.** `AttendanceSessionRecoveryMetrics` stores everything in private fields
  updated via `Interlocked`; nothing is written to the database or disk.
- **Runtime metrics only.** All five values (`RecoveryRuns`, `RecoveredSessions`, `LastRecoveryUtc`,
  `LastRecoveryDuration`, `PendingRecoveries`) are process-lifetime counters, reset on restart.

---

## 6. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Fresh start:** call `/health` and `/health/ready` immediately after startup (before the sweep's
   first tick completes) — `recovery.runs` should be `0`, `lastRecoveryUtc`/`lastDurationMs` should be
   `null`, `pendingRecoveries` should be `0`, `status` should be `"Healthy"`.
3. **After a no-op run:** wait past one `ScanIntervalSeconds` with no orphaned sessions; confirm
   `recovery.runs` increments but `recoveredSessions`/`lastRecoveryUtc` stay at `0`/`null`.
4. **After a real recovery:** force a session into orphaned `Processing` past `TimeoutMinutes`; after
   the next scan, confirm `recoveredSessions` increments, `lastRecoveryUtc`/`lastDurationMs` populate,
   and both endpoints report identical values.
5. **Backlog:** seed more orphaned sessions than `MaxRecoveriesPerRun`; confirm `pendingRecoveries > 0`
   and `status` becomes `"Degraded"` until a later scan clears the backlog.
6. **Disabled:** set `AttendanceSessionRecovery:Enabled` to `false`; confirm `status` is `"Disabled"`
   and counters stay frozen at their startup values (the sweep never runs).
7. **Startup log:** confirm the `Recovery Service` block appears in the startup log with values
   matching `appsettings.json`.

---

## 7. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.Application/Common/Interfaces/IAttendanceSessionRecoveryMetrics.cs` | New — interface + `AttendanceSessionRecoveryMetricsSnapshot` record. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/AttendanceSessionRecoveryMetrics.cs` | New — thread-safe singleton implementation. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs` | Injects `IAttendanceSessionRecoveryMetrics`; calls `RecordRun(...)` at the end of every sweep execution. |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Registers `IAttendanceSessionRecoveryMetrics` as a singleton. |
| `Abhyanvaya.API/Program.cs` | Added `BuildRecoverySnapshot` (shared by both endpoints), `LogRecoveryServiceConfiguration`; `/health/ready` adds `checks.recovery`; `/health` adds `health.recovery`; startup summary adds the `Recovery Service` block. |

---

## 8. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 9. Acceptance criteria

- ✅ `RecoveryRuns`, `RecoveredSessions`, `LastRecoveryUtc`, `LastRecoveryDuration`, `PendingRecoveries`
  maintained as runtime-only counters.
- ✅ Exposed through `GET /health` and `GET /health/ready` — no new endpoints.
- ✅ Startup summary includes the `Recovery Service` block (`Enabled`, `Scan Interval`, `Recovery
  Timeout`, `Maximum Recoveries`), sourced from existing configuration.
- ✅ No persistence, no schema changes, no behavioral changes to recovery itself.
