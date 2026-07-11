# AI14.RUNTIME.3 — Structured Recovery Logging

**Status: IMPLEMENTED**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

## 1. Objective

Improve production support by giving every automatically-recovered `AttendanceSession` its own
structured log entry, plus a single end-of-run summary — so support staff can answer "which session,
which tenant, when, why" without querying the database directly.

---

## 2. Per-session structured log

Emitted once per recovered session, from `LogSessionRecovered` in
`StuckAttendanceSessionRecoveryService`:

```
Attendance Session Recovery
  Tenant                              : 1
  Session                             : ba347012-9e3d-4a2b-8f61-1c2d3e4f5a6b
  Started                             : 2026-07-11 12:15 UTC
  Recovered After                     : 18 minutes
  Recovery Reason                     : Worker terminated unexpectedly
  Recovery Source                     : Automatic Recovery Service
```

```csharp
private void LogSessionRecovered(int tenantId, Guid sessionId, DateTime startedUtc, TimeSpan orphanedFor)
{
    var recoveredAfterMinutes = Math.Max(0, (int)Math.Round(orphanedFor.TotalMinutes));

    _logger.LogWarning("Attendance Session Recovery");
    _logger.LogWarning("  Tenant                              : {TenantId}", tenantId);
    _logger.LogWarning("  Session                             : {SessionId}", sessionId);
    _logger.LogWarning("  Started                             : {StartedUtc:yyyy-MM-dd HH:mm} UTC", startedUtc);
    _logger.LogWarning("  Recovered After                     : {RecoveredAfterMinutes} minutes", recoveredAfterMinutes);
    _logger.LogWarning("  Recovery Reason                     : {RecoveryReason}", RecoveryReason);
    _logger.LogWarning("  Recovery Source                     : {RecoverySource}", RecoverySource);
}
```

Called from `TryRecoverSessionAsync` immediately after the session is successfully saved as `Failed`
— i.e. it only fires for sessions that were *actually* recovered, not for every candidate considered.

### Field notes

| Field | Source | Notes |
|---|---|---|
| `Tenant` | `session.TenantId` | Numeric tenant id — not a name, email, or any PII. |
| `Session` | `session.Id` | The `AttendanceSession` GUID. Logged in full (not truncated) for real traceability/support lookups; truncation in the milestone's example (`ba3470...`) is illustrative formatting, not an actual redaction requirement. |
| `Started` | `session.StartedUtc` | When AI processing began for this session, `yyyy-MM-dd HH:mm UTC`. |
| `Recovered After` | `now - StartedUtc`, rounded to whole minutes | How long the session sat orphaned before this sweep found it. |
| `Recovery Reason` | Fixed constant `"Worker terminated unexpectedly"` | The recovery service cannot know *why* the process died (OOM, crash, manual restart, etc.) — only that a `Processing` session outlived `TimeoutMinutes` with no completion, which is consistent with the worker holding it having terminated. The full technical explanation remains in `session.ProcessingError`, shown to the faculty user in-app; this log field is a concise, constant label for log scanning/alerting. |
| `Recovery Source` | Fixed constant `"Automatic Recovery Service"` | Identifies this specific background service as the origin of the change, distinct from a manual admin action or the normal pipeline's own failure path (`ClassroomRecognitionPipeline`'s `catch` block, which also calls `MoveToFailed()` but for a *caught* exception, not a timeout). |

No face images, embeddings, student names, student ids, or photo storage keys appear anywhere in this
log — only tenant id, session id, and timestamps.

---

## 3. End-of-run summary log

```
Recovery Sweep Completed
  Candidates Found                    : 238
  Recovered                           : 100
  Remaining                           : 138
  Duration                             : 132 ms
```

```csharp
private void LogSweepCompleted(int candidatesFound, int recovered, int remaining, TimeSpan duration)
{
    _logger.LogInformation("Recovery Sweep Completed");
    _logger.LogInformation("  Candidates Found                    : {CandidatesFound}", candidatesFound);
    _logger.LogInformation("  Recovered                           : {Recovered}", recovered);
    _logger.LogInformation("  Remaining                           : {Remaining}", remaining);
    _logger.LogInformation("  Duration                             : {DurationMs} ms", (int)duration.TotalMilliseconds);
}
```

Emitted exactly once per sweep execution (`RecoverStuckSessionsAsync`), timed with a `Stopwatch`
started at the beginning of that method.

### Why one combined summary instead of two

AI14.RUNTIME.2 asks for a summary containing `Candidates Found` / `Recovered` / `Remaining`.
AI14.RUNTIME.3 asks for a summary containing `Recovered` / `Duration`. Emitting both as separate log
blocks for the same sweep execution would mean two overlapping "here's what this run did" messages —
which conflicts with this task's own **"No duplicate logs"** constraint. Instead, `LogSweepCompleted`
is a single block carrying the union of both requirements (`Candidates Found`, `Recovered`,
`Remaining`, `Duration`), titled `Recovery Sweep Completed` per AI14.RUNTIME.3's naming. Every field
either task asked for is present exactly once.

---

## 4. Constraints verification

- **Structured logging.** Every field is passed as a named template placeholder
  (`{TenantId}`, `{SessionId}`, `{StartedUtc}`, `{RecoveredAfterMinutes}`, `{RecoveryReason}`,
  `{RecoverySource}`, `{CandidatesFound}`, `{Recovered}`, `{Remaining}`, `{DurationMs}`) via
  `ILogger`'s message-template API — not string-concatenated — so log sinks that parse structured
  properties (e.g. Seq, Application Insights, ELK) capture each value individually.
- **No duplicate logs.** The previous single-line `LogWarning("Classroom recognition... recovered...")`
  emitted directly in the recovery loop was replaced by the structured block below (see §5) rather
  than left alongside it. The sweep summary is emitted exactly once per run (see §3 above for why it
  isn't duplicated across the two milestone specs either).
- **No sensitive information / no student data.** Confirmed by field-by-field review in §2 — only
  tenant id (int), session id (GUID), and timestamps are logged. No student id, name, photo, face
  image, embedding, or storage key is read or logged by this service at any point.

---

## 5. Relationship to the prior single-line log

Before this task, a session recovery produced one line:

```
warn: Recovered orphaned attendance session stuck in Processing. SessionId=... TenantId=... StartedUtc=...
```

This has been fully replaced by the `Attendance Session Recovery` structured block (§2), which carries
strictly more information (adds `Recovered After`, `Recovery Reason`, `Recovery Source`) in a
consistent, multi-line format matching the rest of the AI12/AI14 diagnostics style — avoiding having
both an old-style and a new-style log for the same event.

---

## 6. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Per-session log:** force a session into orphaned `Processing` (e.g. via the manual SQL used to
   clear the original OOM incident, run in reverse) and let the sweep recover it; confirm the
   `Attendance Session Recovery` block appears with correct tenant, session id, start time, and a
   `Recovered After` value consistent with the configured `TimeoutMinutes`.
3. **Summary log:** confirm exactly one `Recovery Sweep Completed` block appears per run, with
   `Recovered` matching the number of `Attendance Session Recovery` blocks emitted in that same run.
4. **No PII:** grep the log output for student ids/names/photo keys — none should appear from this
   service.

---

## 7. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs` | Replaced the single-line recovery log with the structured `Attendance Session Recovery` block (`LogSessionRecovered`); added the `Recovery Sweep Completed` end-of-run summary (`LogSweepCompleted`, shared with AI14.RUNTIME.2). |

---

## 8. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 9. Acceptance criteria

- ✅ Every recovered session logs `Attendance Session Recovery` with Tenant, Session, Started,
  Recovered After, Recovery Reason, Recovery Source.
- ✅ One summary log per sweep run (`Recovery Sweep Completed`) with Recovered + Duration (plus the
  AI14.RUNTIME.2 throttling fields, consolidated rather than duplicated).
- ✅ No duplicate logs for the same event.
- ✅ No sensitive information or student data in any log field.
