# AI20.ENROLLMENT.6 — Background Processing Design

**Type:** Design only. No production code was written or modified to produce this document.

---

## 1. Review of Existing Background Workers

Three `BackgroundService` implementations exist today, all following the same shape:

| Worker | File | Queue mechanism | Retry | Recovery |
|---|---|---|---|---|
| `ClassroomRecognitionBackgroundService` | `Abhyanvaya.Infrastructure/BackgroundWorkers/ClassroomRecognitionBackgroundService.cs` | `await foreach` over `IClassroomPhotoQueue.DequeueAllAsync` — an in-memory `Channel<ClassroomPhotoMessage>` (`InMemoryClassroomPhotoQueue`, singleton, `Channel.CreateUnbounded`) | **None** — explicit code comment confirms no retry mechanism exists; failure logs and moves on | `StuckAttendanceSessionRecoveryService` (separate worker) periodically force-fails sessions stuck too long |
| `StudentFaceEmbeddingBackgroundService` | `Abhyanvaya.Infrastructure/BackgroundWorkers/StudentFaceEmbeddingBackgroundService.cs` | Same `Channel<T>` pattern, `IStudentPhotoEmbeddingQueue` | In-job loop, `EmbeddingStorage.MaxRetryCount = 3`, retried *within* the same dequeued message, not re-queued | None dedicated — a lost in-flight message on crash is simply lost |
| `StuckAttendanceSessionRecoveryService` | `Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs` | N/A — `PeriodicTimer`, runs immediately then every `ScanIntervalSeconds` (default 60s, min 5s) | N/A | Is itself the recovery mechanism: finds `AttendanceSession.Status == Processing` older than `TimeoutMinutes` (default 10), force-`MoveToFailed` |

Common shape all three share (and this design reuses):

```csharp
await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
{
    try
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(message.TenantId);
        try { /* resolve scoped pipeline, do the work */ }
        finally { tenantContext.Clear(); }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
    catch (Exception ex) { _logger.LogError(ex, "..."); /* continue loop */ }
}
```

DI registration is centralized in `Abhyanvaya.Infrastructure/DependencyInjection.cs` (`AddHostedService<T>()` for each, plus `AddSingleton` for each queue) — **not** in `Program.cs` directly.

---

## 2. Can Existing Infrastructure Be Reused?

**Partially — the consumer shape yes; the queue durability model no.** This is the single most important design decision in this document, so it is stated up front and justified before anything else:

### Why the existing `Channel<T>` in-memory queue is insufficient for enrollment

`InMemoryClassroomPhotoQueue`/`InMemoryStudentPhotoEmbeddingQueue` hold queued messages **only in process memory**. If the API process restarts (Render redeploy, crash, scale event) while messages are sitting in the channel *before* being dequeued, those messages are gone — permanently, silently, with no DB trace they ever existed. This is an acceptable risk for classroom recognition today because:
- A classroom recognition job is triggered by a single teacher action and is short-lived (seconds to low minutes) — the window during which a message could be sitting un-dequeued is small.
- `StuckAttendanceSessionRecoveryService` provides a safety net for jobs that *were* dequeued and started but crashed mid-flight (the DB row is already `Processing`) — but it provides **no** protection for jobs that never got dequeued at all, since there's no DB row to notice as "stuck" until `MoveToProcessing` is called.
- If lost, the teacher can simply re-upload the classroom photo — low-friction recovery.

**None of these mitigations hold for AI Enrollment:**
- A SuperAdmin can enqueue **thousands of students** in one batch. The in-memory backlog between "batch created" and "every job dequeued" could realistically span many minutes to hours depending on external photo-source latency and embedding throughput.
- There is currently no DB row for the *download* stage at all in the existing pattern — a classroom job's DB row (`AttendanceSession`) exists from before the queue message is even created, but the equivalent "the job exists" signal for enrollment would need to be true from the moment the batch is created, not from the moment a worker happens to pick it up.
- Recovery from "lost thousands of not-yet-started students because of a redeploy" by asking a SuperAdmin to notice and manually re-trigger the whole batch is a poor operational experience for what is meant to be a reliable, auditable administrative bulk operation.

### Decision

**The `StudentEnrollmentJob.Status` column (per `docs/AI20_ENROLLMENT_DATABASE.md`) is the durable queue.** A row with `Status IN (Pending, RetryRequired)` **is** "work to be done" — there is no separate in-memory message representing that fact that could be lost independently of the DB. This is combined with a lightweight in-memory `Channel<Guid>` used purely as a **low-latency wake-up signal** (so a newly created batch doesn't have to wait for the next poll interval to start processing) — but the signal is an optimization, never a correctness requirement: if the process restarts and the channel is empty, the periodic poll (§3) finds the same `Pending` rows regardless.

**What is reused unchanged:** the per-job `BackgroundService` consumer shape (`CreateAsyncScope` → set tenant → run scoped pipeline → clear tenant in `finally` → catch-log-continue on error) and the `StuckAttendanceSessionRecoveryService`-style `PeriodicTimer` recovery sweep shape. Only the *queue* half of the pattern changes.

---

## 3. Design

### 3.1 Enrollment Queue

```csharp
public interface IEnrollmentJobQueue
{
    // Called by EnrollmentBatchService right after a batch's job rows are inserted —
    // purely a latency optimization, not required for correctness.
    void SignalWork();

    // Consumed by EnrollmentBackgroundService. Combines the wake signal with a periodic
    // fallback poll so a missed/coalesced signal (or a fresh process start) still finds
    // Pending/RetryRequired work within one poll interval.
    IAsyncEnumerable<Guid> DequeueClaimedJobIdsAsync(CancellationToken cancellationToken);
}
```

Implementation sketch (`InMemoryEnrollmentWakeSignal` + a poll loop inside the background service, rather than a `Channel<StudentEnrollmentJob>` carrying the actual payload — the channel only ever carries a "something changed, go check the DB" ping):

```csharp
public sealed class InMemoryEnrollmentWakeSignal : IEnrollmentJobQueue
{
    private readonly Channel<byte> _wake = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void SignalWork() => _wake.Writer.TryWrite(0);

    // EnrollmentBackgroundService's loop: WaitToReadAsync(cancellationToken) with a timeout
    // (via Task.WhenAny against a PeriodicTimer tick) — whichever fires first triggers a DB poll.
}
```

**Job claiming (optimistic concurrency, DB-level, so multiple parallel workers/instances never double-process the same job):**

```sql
UPDATE "StudentEnrollmentJob"
SET "Status" = @Downloading, "LastAttemptUtc" = now(), "RowVersion" = @newRowVersion
WHERE "Id" = @jobId AND "Status" IN (@Pending, @RetryRequired) AND "RowVersion" = @expectedRowVersion
```

via EF Core's built-in concurrency-token semantics (the same `RowVersion`/`bytea` mechanism `AttendanceRecognition` already uses, per `ConcurrencyExceptionHelper.SaveChangesAsync` — reused unmodified). A worker selects candidate job IDs (`SELECT TOP N ... WHERE Status IN (Pending, RetryRequired) AND BatchId NOT IN (cancelling batches) ORDER BY CreatedUtc`), then attempts the claim update per candidate; a `DbUpdateConcurrencyException` on the claim simply means another worker/instance got there first — skip and move to the next candidate, no error logged (this is expected, routine contention, not a failure).

### 3.2 Progress

**No new progress table.** Progress is the existing counter-based approach `StudentEnrollmentBatch` already carries (`docs/AI20_ENROLLMENT_DATABASE.md` §3.1: `PendingCount`, `DownloadingCount`, ..., `CompletedCount`, `FailedCount`, ...), updated transactionally in the same `SaveChangesAsync` call that transitions a `StudentEnrollmentJob.Status` (decrement the old status's counter, increment the new one). This mirrors the general pattern of denormalized progress counters over recomputing aggregates on every poll, and is directly analogous to how `AttendanceSessionStatusMapper` derives `RecognitionProgressPercent` from a handful of session-level counters rather than scanning `AttendanceRecognition` rows on every status poll.

`GET /api/enrollment/batches/{id}/status` (new endpoint) simply reads the batch row's counters — an O(1) read regardless of how many thousands of jobs the batch contains.

**Per-job current-stage detail** (for the UI's "Current Student / Current Stage" per AI20.3) is a direct read of `StudentEnrollmentJob.Status` + stage timestamps for whichever jobs are currently `Downloading`/`Validating`/`Embedding` — a small, indexed (`IX_StudentEnrollmentJob_Batch_Status`) query, not a full-batch scan.

**Speed metrics** (download speed, embedding speed, per AI20.3): computed at query time from recent completed jobs' stage-timestamp deltas (e.g., average of `DownloadedUtc - DownloadStartedUtc` over the last N completed jobs in the batch) — no separate telemetry table needed, since the raw timestamps already exist per job.

### 3.3 Recovery / Crash Recovery

New `StuckEnrollmentJobRecoveryService`, structurally identical to `StuckAttendanceSessionRecoveryService`:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var pollInterval = TimeSpan.FromSeconds(Math.Max(5, _options.ScanIntervalSeconds));
    using var timer = new PeriodicTimer(pollInterval);
    do
    {
        try { await RecoverStuckJobsAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        catch (Exception ex) { _logger.LogError(ex, "Enrollment job recovery sweep failed unexpectedly."); }
    }
    while (await timer.WaitForNextTickAsync(stoppingToken));
}
```

`RecoverStuckJobsAsync` query shape (using the `IX_StudentEnrollmentJob_Status_LastAttempt` index from `docs/AI20_ENROLLMENT_DATABASE.md`):

```
WHERE Status IN (Downloading, Validating, Embedding)
  AND LastAttemptUtc < now() - TimeoutMinutes
```

For each match: increment `RetryCount`; if `RetryCount < MaxAutoRetries` (configurable, recommend 3, mirroring `EmbeddingStorage.MaxRetryCount`), set `Status = RetryRequired` (so the normal queue poll picks it back up); else set `Status = Failed`, `FailureCategory = Timeout`. This is the mechanism that makes a process crash mid-job **self-healing** without any SuperAdmin action — exactly the safety net `StuckAttendanceSessionRecoveryService` provides for `AttendanceSession`, applied to `StudentEnrollmentJob`.

Because the queue itself is DB-row-backed (§2), this recovery sweep is actually **more powerful** than its `AttendanceSession` counterpart: it also implicitly covers jobs that were `Pending` and never got dequeued before a restart, since those rows were never lost in the first place — a plain restart of `EnrollmentBackgroundService` naturally re-polls and finds them with no special-casing required. The stuck-job sweep exists specifically for jobs that *were* claimed (moved past `Pending`) and then the worker died before reaching a terminal status.

### 3.4 Resume

Resume is a natural consequence of §2's design, not a separate feature to build: because "work remaining" is defined entirely by DB row state (`Status IN (Pending, RetryRequired)`), simply restarting `EnrollmentBackgroundService` (whether due to a crash, a deploy, or the whole process restarting) **is** resuming — it re-polls, finds exactly the same outstanding rows a healthy process would have found, and continues. No explicit "resume batch" server-side logic is needed. The UI's "Resume" button (AI20.3) is really just **un-setting** `CancellationRequestedUtc` (if the batch had been explicitly cancelled) or a no-op reassurance if the batch was merely paused by a restart, since processing resumes automatically the moment the service is running again.

### 3.5 Parallel Processing

**Two independently-tunable concurrency limits**, because the two dominant stages have very different resource profiles:

| Stage | Bottleneck | Recommended concurrency |
|---|---|---|
| Download (external HTTP + R2 upload) | I/O-bound (network latency to the photo source and to R2) | Higher — e.g. `MaxDegreeOfParallelism: 8` (configurable), since concurrent HTTP requests don't contend for CPU |
| Validation + Embedding (ONNX inference) | CPU-bound, shares the same singleton `InsightFaceOnnxModelHost` sessions the classroom recognition pipeline also uses | Lower, and must respect the **same** `IntraOp`/`InterOp` thread configuration already tuned for the ONNX sessions (AI16.RUNTIME.1) — recommend concurrency capped at **1–2** concurrent embedding operations on a Render Starter/Standard instance, to avoid oversubscribing the CPU cores ONNX's own internal threading already claims, and to avoid enrollment embedding work starving live classroom recognition requests that might be running concurrently on the same instance |

Implementation: two `SemaphoreSlim` gates inside `EnrollmentBackgroundService` (or two separate bounded worker-loop tasks reading from the same claimed-job stream, gated at the point they enter the download vs. embedding phase) — a job's download phase and embedding phase acquire/release their respective semaphore independently, so a slow embedding backlog doesn't stall downloads from proceeding (and vice versa), while still bounding total concurrent CPU-heavy work.

### 3.6 Cancellation

- SuperAdmin cancel action sets `StudentEnrollmentBatch.CancellationRequestedUtc` (a single UPDATE, immediate).
- Before claiming a new job (§3.1's claim query), the worker additionally filters out jobs whose `BatchId` has a non-null `CancellationRequestedUtc` — so no *new* work starts for a cancelled batch.
- Jobs already in flight (claimed, mid-download/mid-embedding) are allowed to finish naturally rather than being forcibly interrupted — mirrors the existing `ClassroomRecognitionPipeline` precedent of cooperative `CancellationToken` checks at natural boundaries rather than hard-aborting ONNX inference mid-call (ONNX Runtime inference is not safely interruptible mid-call).
- Once the last in-flight job for a cancelled batch reaches a terminal state, any remaining `Pending` jobs for that batch are swept to `Cancelled` (a small batch-scoped UPDATE, either done eagerly by the cancel action itself for jobs not yet claimed, or lazily by the recovery sweep — eager is simpler and recommended, since un-claimed `Pending` rows can be updated immediately and safely with no concurrency risk).

### 3.7 Logging / Telemetry

Reuses the existing structured-logging conventions already established for recognition diagnostics (`IRecognitionExecutionContext`-style correlation IDs), applied to enrollment:

- Every job transition logs `BatchId`, `StudentId`, `JobId`, old/new `Status`, and (on failure) `FailureCategory` — mirrors `RecognitionMediaService`'s "Upload Started/Completed/Failed" structured-log convention.
- Batch-level summary logs on completion (`"Enrollment Batch Completed. BatchId={BatchId} Total={Total} Completed={Completed} Failed={Failed} DurationMs={DurationMs}"`) mirror `StuckAttendanceSessionRecoveryService`'s "Recovery Sweep Completed" block style.
- `BackgroundWorkerInspector` (the existing component that already inspects `ClassroomRecognitionBackgroundService`/`StudentFaceEmbeddingBackgroundService` for health/logging) should be extended to also report on `EnrollmentBackgroundService` — same inspection shape, one more worker to look at, no change to the inspector's existing behavior for the two workers it already covers.
- Telemetry counters exposed via the batch status endpoint (§3.2) double as the operational telemetry surface — no separate metrics/APM integration is proposed as part of this design (out of scope; if the platform later adopts one, these counters are the natural source to feed it).

---

## 4. DI Registration (mirrors existing pattern)

```csharp
// Abhyanvaya.Infrastructure/DependencyInjection.cs — new registrations alongside the existing ones
services.AddSingleton<IEnrollmentJobQueue, InMemoryEnrollmentWakeSignal>();

services.AddOptions<EnrollmentRecoveryOptions>()
    .Bind(configuration.GetSection(EnrollmentRecoveryOptions.SectionName));

services.AddScoped<IEnrollmentPipeline, EnrollmentPipeline>();

services.AddHostedService<EnrollmentBackgroundService>();
services.AddHostedService<StuckEnrollmentJobRecoveryService>();
```

Placed alongside, not replacing, the three existing `AddHostedService` registrations (`ClassroomRecognitionBackgroundService`, `StudentFaceEmbeddingBackgroundService`, `StuckAttendanceSessionRecoveryService`) — all run concurrently in the same API process, exactly as the existing three already do.

---

## Constraints Confirmed

No code was written or modified to produce this document — no new `BackgroundService`, queue, or DI registration exists yet. All designs above are proposals only, and no existing background worker, queue, or recovery service was changed.
