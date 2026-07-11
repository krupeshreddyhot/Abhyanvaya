# AI14.RUNTIME.2 — Recovery Service Throttling

**Status: IMPLEMENTED**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

## 1. Objective

Prevent `StuckAttendanceSessionRecoveryService` from recovering an unbounded number of orphaned
`AttendanceSession` rows in a single execution, so a large backlog (e.g. after an extended outage)
cannot turn one recovery cycle into a long-running, all-at-once database sweep.

No behavioral change to *what* gets recovered or *why* — only *how many rows per run*.

---

## 2. Configuration

New/updated `AttendanceSessionRecovery` section (`Abhyanvaya.API/appsettings.json`):

```json
"AttendanceSessionRecovery": {
  "Enabled": true,
  "TimeoutMinutes": 10,
  "ScanIntervalSeconds": 60,
  "MaxRecoveriesPerRun": 100
}
```

Bound by `AttendanceSessionRecoveryOptions`:

```csharp
public sealed class AttendanceSessionRecoveryOptions
{
    public const string SectionName = "AttendanceSessionRecovery";

    public bool Enabled { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 10;
    public int ScanIntervalSeconds { get; set; } = 60;
    public int MaxRecoveriesPerRun { get; set; } = 100;
}
```

| Key | Meaning | Default |
|---|---|---|
| `Enabled` | Master on/off switch for the whole sweep. | `true` |
| `TimeoutMinutes` | How long a session may sit in `Processing` before it's considered orphaned. | `10` |
| `ScanIntervalSeconds` | How often the sweep runs. | `60` |
| `MaxRecoveriesPerRun` | Upper bound on how many sessions one run will recover. | `100` |

This is a rename/extension of the options introduced alongside the recovery service itself
(previously `StuckProcessingTimeoutMinutes`/`PollIntervalSeconds`); since that service had not yet
been deployed, the names were aligned to this task's required shape rather than kept as aliases —
there is no production configuration depending on the old names.

---

## 3. Implementation

`Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs`:

```csharp
if (!_options.Enabled)
{
    _logger.LogInformation(
        "Attendance session recovery sweep is disabled (AttendanceSessionRecovery:Enabled=false). Orphaned sessions will not be auto-recovered.");
    return;
}
```

The `Enabled` flag is checked once when the hosted service starts; when `false`, the service logs and
exits immediately without registering a timer or ever querying the database — a true no-op, not just
a sweep that finds nothing.

Throttled query, oldest-first:

```csharp
var stuckQuery = context.AttendanceSessions
    .IgnoreQueryFilters()
    .Where(s => s.Status == AttendanceSessionStatus.Processing
                && s.StartedUtc != null
                && s.StartedUtc < cutoffUtc);

var candidatesFound = await stuckQuery.CountAsync(cancellationToken);

if (candidatesFound == 0)
{
    LogSweepCompleted(candidatesFound, recovered: 0, remaining: 0, stopwatch.Elapsed);
    return;
}

// AI14.RUNTIME.2: recover at most MaxRecoveriesPerRun sessions per run, oldest-first, so a
// single sweep never processes an unbounded number of rows; anything beyond the cap is left
// for the next scan.
var batch = await stuckQuery
    .OrderBy(s => s.StartedUtc)
    .Take(Math.Max(0, _options.MaxRecoveriesPerRun))
    .ToListAsync(cancellationToken);

var recoveredCount = 0;
foreach (var session in batch)
{
    if (await TryRecoverSessionAsync(session, unitOfWork, cancellationToken))
    {
        recoveredCount++;
    }
}

var remaining = Math.Max(0, candidatesFound - batch.Count);
LogSweepCompleted(candidatesFound, recoveredCount, remaining, stopwatch.Elapsed);
```

- `candidatesFound` is a `COUNT(*)` over **all** matching orphaned rows (no limit) — this is what's
  reported as "Candidates Found", independent of the cap.
- `batch` applies `OrderBy(StartedUtc).Take(MaxRecoveriesPerRun)` — the oldest-orphaned sessions are
  recovered first (fairness: a session that's been stuck longest is prioritized), and the query never
  materializes more than `MaxRecoveriesPerRun` rows into memory.
- `remaining = candidatesFound - batch.Count` — anything not included in this run's batch is left
  untouched in the database and will be picked up (and re-counted) on the next scan.
- Each session is still recovered (and saved) individually inside `TryRecoverSessionAsync`, exactly as
  before — throttling only changes the size of the candidate set that reaches that per-session logic,
  not the per-session recovery mechanics themselves.

### End-of-run summary log

```
Recovery Sweep Completed
  Candidates Found                    : 238
  Recovered                           : 100
  Remaining                           : 138
  Duration                             : 42 ms
```

Emitted via `LogSweepCompleted` at the end of every run — including runs that find zero candidates
(`Candidates Found: 0 / Recovered: 0 / Remaining: 0`), so the sweep's liveness is always visible in
logs, not only when it does work.

> **Note on log consolidation:** AI14.RUNTIME.3 (structured recovery logging) independently asks for
> an end-of-run summary with `Recovered` + `Duration`. Rather than emit two near-identical summary
> blocks for the same sweep execution (which would violate AI14.RUNTIME.3's own "no duplicate logs"
> constraint), both requirements are satisfied by the single `Recovery Sweep Completed` block above,
> which is a superset containing `Candidates Found`, `Recovered`, `Remaining`, **and** `Duration`.

---

## 4. Constraints verification

- **No behavioral changes.** A session is still recovered using the exact same criteria
  (`Status == Processing && StartedUtc < now - TimeoutMinutes`) and the exact same transition
  (`MoveToFailed()` + `ProcessingError` + `CompletedUtc`) as before this task. Only the *number of rows
  considered per run* changed.
- **No database schema changes.** No migration, no new column, no new table — `AttendanceSession` is
  unchanged.
- **Configuration driven.** `Enabled`, `TimeoutMinutes`, `ScanIntervalSeconds`, and
  `MaxRecoveriesPerRun` are all read from `IOptions<AttendanceSessionRecoveryOptions>`, bound from the
  `AttendanceSessionRecovery` configuration section — none are hardcoded.
- **Backward compatible.** The service still runs immediately on startup and then on the configured
  interval; disabling it (`Enabled: false`) is opt-in (default remains `true`, matching prior
  behavior). Existing `AttendanceSession` rows and the `ClassroomRecognitionPipeline` are untouched.

---

## 5. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Throttling:** seed (or simulate) more than `MaxRecoveriesPerRun` orphaned sessions; confirm one
   run recovers exactly `MaxRecoveriesPerRun` of them (oldest `StartedUtc` first) and the summary log
   reports the correct `Remaining` count; confirm the next scan recovers the rest.
2. **Disabled:** set `AttendanceSessionRecovery:Enabled` to `false`; confirm the service logs the
   disabled message once at startup and never logs a `Recovery Sweep Completed` block or queries
   `AttendanceSession`.
3. **Zero candidates:** with no stuck sessions, confirm each run still logs
   `Recovery Sweep Completed` with all three counts at `0`.

---

## 6. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/BackgroundWorkers/AttendanceSessionRecoveryOptions.cs` | Renamed/extended: `Enabled`, `TimeoutMinutes`, `ScanIntervalSeconds`, `MaxRecoveriesPerRun`. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs` | Added `Enabled` gate; throttled candidate query (`COUNT` + `OrderBy(StartedUtc).Take(MaxRecoveriesPerRun)`); added `LogSweepCompleted` end-of-run summary. |
| `Abhyanvaya.API/appsettings.json` | Updated `AttendanceSessionRecovery` section to the new key names plus `Enabled`/`MaxRecoveriesPerRun`. |

---

## 7. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 8. Acceptance criteria

- ✅ `AttendanceSessionRecovery` configuration section matches the requested shape exactly
  (`Enabled`, `TimeoutMinutes`, `ScanIntervalSeconds`, `MaxRecoveriesPerRun`).
- ✅ At most `MaxRecoveriesPerRun` sessions are recovered per cycle; the rest are left for the next
  scan (never processed all at once).
- ✅ End-of-run log reports `Candidates Found` / `Recovered` / `Remaining`.
- ✅ No behavioral, schema, or backward-compatibility changes beyond the throttle itself.
