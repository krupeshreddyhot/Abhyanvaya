# AI11.BG.5 — Worker Runtime Execution Trace (Investigation Only)

> **Scope:** Read-only investigation. No source code was modified, no logging
> was added, no architecture was changed. This document traces the runtime
> execution path of the classroom recognition worker to determine exactly where
> a session stuck at **15% (Pending)** stops progressing.

---

## Symptom Recap

- Session remains at **15%**. Per `AttendanceSessionStatusMapper.ComputeProgressPercent`,
  **15% is emitted only when `AttendanceSession.Status == Pending`** (see
  `Abhyanvaya.Application/Internal/AttendanceSessionStatusMapper.cs:114-117`).
- Background workers report **Running / Healthy** (confirmed live via `GET /health`).
- Recognition queue depth reaches **0** (job was dequeued).
- The `models/insightface` directory and both ONNX files (`det_10g.onnx`,
  `w600k_r50.onnx`) are **missing** on disk (confirmed live via `GET /health`).

---

## STEP 1 — Complete Execution Path / Call Chain

### Enqueue side (HTTP request thread)

```
AttendanceSessionController  (upload endpoint)
        ↓
AttendancePhotoService.UploadClassroomPhotoAsync()        [Application/AttendancePhotoService.cs:42]
   — or —
AttendanceSessionCreator.CreateAndUploadClassroomPhotoAsync()  [Application/AttendanceSessionCreator.cs:83]
        ↓
IUnitOfWork.ExecuteInTransactionAsync(...)                 [Persistence/ApplicationDbContext.UnitOfWork.cs:36]
        ↓  (inside transaction)
   UploadToSessionAsync()  → validate → _mediaStorage.SaveOriginalObjectAsync()
                          → session.AttachClassroomImage()
        ↓
   session.MoveToPending()            [Draft → Pending]   (only if status == Draft)
        ↓
   ConcurrencyExceptionHelper.SaveChangesAsync()  ← COMMIT #1: persists Status = Pending
        ↓  (transaction commits)
QueueProcessingAsync()                                     [AttendancePhotoService.cs:182]
        ↓
IClassroomPhotoQueue.EnqueueAsync()                       [Recognition/InMemoryClassroomPhotoQueue.cs:16]
        ↓
   Interlocked.Increment(ref _queuedCount)  →  _channel.Writer.WriteAsync(message)
```

At this point the DB row is **Pending (15%)** and one message sits in the channel.

### Dequeue side (background worker thread)

```
ClassroomRecognitionBackgroundService.ExecuteAsync()      [BackgroundWorkers/ClassroomRecognitionBackgroundService.cs:25]
        ↓
_queue.DequeueAllAsync(stoppingToken)   (await foreach)   [InMemoryClassroomPhotoQueue.cs:27]
        ↓  channel.Reader.WaitToReadAsync → TryRead
   Interlocked.Decrement(ref _queuedCount)   ← queue depth returns to 0 HERE
        ↓  yield return message
   ── per-message try block ──
        ↓
   _scopeFactory.CreateAsyncScope()                       [line 39]
        ↓
   scope.ServiceProvider.GetRequiredService<IClassroomRecognitionPipeline>()   [line 40]
        ↓
   pipeline.ProcessAsync(message, stoppingToken)          [line 41]
```

### Pipeline (`ClassroomRecognitionPipeline.ProcessAsync`) — `Recognition/ClassroomRecognitionPipeline.cs:50`

```
FirstOrDefaultAsync(session)  ?? throw KeyNotFoundException   [line 53-55]   ← OUTSIDE try/catch
        ↓
── try ──                                                     [line 57]
   if (Status == Draft) MoveToProcessing skipped; here Status == Pending
   session.MoveToProcessing()          [Pending → Processing, IN MEMORY ONLY]   [line 64]
   session.StartedUtc = now
   session.RecognitionProvider = _faceDetectionService.ProviderName   (constant string)
   session.RecognitionModel    = _faceDetectionService.ModelName      (config string; NO model load)
   session.RecognitionPipelineVersion = _insightFaceOptions.PipelineVersion
        ↓
   ConcurrencyExceptionHelper.SaveChangesAsync()   ← COMMIT #2: persists Status = Processing   [line 69]
        ↓
   _mediaReader.ReadObjectAsync()/ReadVariantAsync()          [line 71-73]
        ↓
   _faceDetectionService.DetectAsync()                        [line 74]
        ↓
   InsightFaceEngine.DetectAsync()                            [InsightFace/InsightFaceEngine.cs:33]
        ↓
   Image.Load<Rgb24>()  → DetectFaces()                       [InsightFaceEngine.cs:38-39,103]
        ↓
   _modelHost.GetDetectionSession()                           [InsightFaceEngine.cs:105]
        ↓
   InsightFaceOnnxModelHost.EnsureLoaded()                    [InsightFace/InsightFaceOnnxModelHost.cs:34]
        ↓
   File.Exists(path) == false  →  throw FileNotFoundException [InsightFaceOnnxModelHost.cs:49-55]   ← STOPS HERE (models missing)
        ↓
   ── the rest is NEVER reached when models are missing ──
   _faceMatcher.Match()  → build AttendanceRecognition rows
   session.MoveToAwaitingReview()   [Processing → AwaitingReview]
   ConcurrencyExceptionHelper.SaveChangesAsync()   ← COMMIT #3 (success path)   [line 128]
   _queue.MarkCompleted(session.Id)
── catch (Exception ex) ──                                    [line 138]
   session.ProcessingError = ex.Message
   session.CompletedUtc = now
   session.ProcessingMilliseconds = elapsed
   session.MoveToFailed()             [Processing → Failed, IN MEMORY]          [line 143]
   ConcurrencyExceptionHelper.SaveChangesAsync()   ← COMMIT (failure path): persists Status = Failed  [line 144]
   _queue.MarkCompleted(session.Id)
   throw   ← rethrows to the worker
```

---

## STEP 2 — Logging Inventory

| # | Class | Method | Message (level) | Executes relative to `MoveToProcessing()` |
|---|-------|--------|-----------------|-------------------------------------------|
| 1 | `AttendancePhotoService` | `UploadToSessionAsync` | "Classroom photo stored…" (**Information**) | Before (enqueue side) |
| 2 | `AttendancePhotoService` | `QueueProcessingAsync` | "Classroom recognition job enqueued… QueueDepth=…" (**Information**) | Before (enqueue side) |
| 3 | `AttendancePhotoService` | `UploadClassroomPhotoAsync` | "Classroom photo upload failed…" (**Warning**) | Before (only on upload failure) |
| 4 | `ClassroomRecognitionBackgroundService` | `ExecuteAsync` | "Classroom recognition background worker started." (**Information**) | Before (once at worker startup) |
| 5 | `ClassroomRecognitionBackgroundService` | `ExecuteAsync` | "Classroom recognition job dequeued… QueueDepth=…" (**Information**) | **Before** `MoveToProcessing` (first line inside the per-job try, before the scope/pipeline call) |
| 6 | `ClassroomRecognitionBackgroundService` | `ExecuteAsync` | "Classroom recognition job failed…" (**Error**) | After (worker-level catch; fires only if the whole per-job block throws) |
| 7 | `ClassroomRecognitionPipeline` | `ProcessAsync` | "Classroom recognition completed…" (**Information**) | After (success path only — never reached with missing models) |
| 8 | `InsightFaceOnnxModelHost` | `EnsureLoaded` | "InsightFace {Label} ONNX model loaded from {Path}" (**Information**) | After (only on *successful* load — never fires with missing models) |
| 9 | `InsightFaceEngine` | `ParseDetectionOutputs` | "SCRFD output parsing produced zero candidates…" (**Warning**) | After (only if a model *was* loaded) |
| 10 | `ApplicationDbContext` | `SaveChangesAsync` | "Attendance session created…" (**Information**) | Before (on session insert) |
| 11 | `ApplicationDbContext.UnitOfWork` | `ExecuteInTransactionAsync` | "Transaction rollback due to failure." (**Warning**) | Before (enqueue-side transaction only; the pipeline does **not** use this) |

**Critical observation:** There is **no** log statement between "job dequeued" (#5)
and the pipeline's first meaningful action. If the failure occurs at the worker
level (scope creation / DI resolution / the `FirstOrDefaultAsync` session-load
that sits *outside* the pipeline try), the **only** trace produced is the
worker-level **Error** log (#6) — and that handler never touches the session.

---

## STEP 3 — Is `MoveToProcessing()` Actually Called?

| Item | Detail |
|------|--------|
| Definition | `AttendanceSession.MoveToProcessing()` → `TransitionTo(Processing)` — `Domain/Entities/AttendanceSession.StateMachine.cs:15` |
| Caller | `ClassroomRecognitionPipeline.ProcessAsync` — `Recognition/ClassroomRecognitionPipeline.cs:64` |
| Precondition to reach it | (1) worker dequeues the message, (2) DI resolves `IClassroomRecognitionPipeline`, (3) `FirstOrDefaultAsync` finds the session (line 53), (4) execution enters the `try` (line 57) |
| Transition legality | `Pending → Processing` is **valid** (`AttendanceSession.StateMachine.cs:101`). Sessions are created as `Draft` (`AttendanceSession.Factory.cs:78`) and moved to `Pending` + committed during upload, so the row is `Pending` when the worker loads it — the transition will not throw. |
| Persistence | In-memory only until **COMMIT #2** at `ClassroomRecognitionPipeline.cs:69`. |

**Conclusion:** `MoveToProcessing()` is wired correctly and, *if the pipeline body
executes*, it runs and is committed at line 69 **before** any model file is
touched (the provider/model assignments on lines 66-68 read config strings, not
ONNX files). Therefore, had the pipeline reached line 69, the DB status would be
**Processing (≥25%)** and then **Failed (0%)** once `DetectAsync` throws — **not**
Pending (15%). The persistence of Pending-only is the central clue (see Root Cause).

---

## STEP 4 — Where `InsightFaceOnnxModelHost.EnsureLoaded()` Is Called

| Item | Detail |
|------|--------|
| Definition | `InsightFaceOnnxModelHost.EnsureLoaded(ref session, modelFile, label)` — `InsightFace/InsightFaceOnnxModelHost.cs:34` |
| Invoked by | `GetDetectionSession()` (line 24) and `GetRecognitionSession()` (line 30) |
| Those invoked by | `InsightFaceEngine.DetectFaces()` (line 105) and `ExtractEmbedding()` (line 119) |
| Those invoked by | `InsightFaceEngine.DetectAsync()` (line 39/50) |
| That invoked by | `InsightFaceDetectionService.DetectAsync()` (line 29) |
| That invoked by | `ClassroomRecognitionPipeline.ProcessAsync()` line 74 |
| Exception raised | `throw new FileNotFoundException("InsightFace {label} model not found at '{path}'…")` when `File.Exists(path)` is false — `InsightFaceOnnxModelHost.cs:49-55` |
| Local handling | **None.** Neither `EnsureLoaded`, `InsightFaceEngine`, nor `InsightFaceDetectionService` wraps this in try/catch. |
| Propagation | The `FileNotFoundException` **propagates up** to the pipeline's `catch (Exception ex)` at `ClassroomRecognitionPipeline.cs:138`, which maps the session to **Failed** and rethrows. |

**So:** the `FileNotFoundException` is **not swallowed at the model layer** — it
propagates and, *if the pipeline body is running*, it is converted into a
`Failed` session (0%) with `ProcessingError` = the "model not found" message.

---

## STEP 5 — Every `catch` Between Worker → Pipeline → Model Loader

| Location | Catch | Logs? | Updates session? | MoveToFailed? | SaveChanges? | Rethrow / Swallow |
|----------|-------|-------|------------------|---------------|--------------|-------------------|
| `ClassroomRecognitionBackgroundService.cs:43` | `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)` | No | No | No | No | `break` (graceful shutdown) |
| `ClassroomRecognitionBackgroundService.cs:47` | `catch (Exception ex)` | **Yes (Error)** | **No** | **No** | **No** | **Swallow** (loop continues to next message) |
| `ClassroomRecognitionPipeline.cs:138` | `catch (Exception ex)` | No (sets `ProcessingError` field) | **Yes** | **Yes** | **Yes (COMMIT Failed)** | **Rethrow** |
| `ConcurrencyExceptionHelper.cs:25` | `catch (DbUpdateConcurrencyException ex)` | No | No | No | No | Rethrow as `ConcurrencyConflictException` |
| `ApplicationDbContext.UnitOfWork.cs:47` | `catch (Exception ex)` | Yes (Warning) | No | No | Rollback | Rethrow (enqueue-side transaction only; **not** used by the pipeline) |
| `InsightFaceOnnxModelHost` / `InsightFaceEngine` / `InsightFaceDetectionService` | *(none)* | — | — | — | — | Exceptions propagate unhandled |

**The decisive asymmetry:** The pipeline's own catch (row 3) reliably marks the
session **Failed** and commits it. But that catch can only fire for exceptions
thrown **inside `ProcessAsync`'s try block** (i.e., after the session is loaded
on line 53 and after entering the try on line 57). Any failure **before** that —
`CreateAsyncScope()`, `GetRequiredService<IClassroomRecognitionPipeline>()`, or
the `FirstOrDefaultAsync … ?? throw KeyNotFoundException` on lines 53-55 — is
caught **only** by the worker-level `catch (Exception)` (row 2), which **logs and
swallows without ever touching `AttendanceSession.Status`.** In that path the
session stays **Pending (15%) permanently**.

---

## STEP 6 — Sequence Diagram

```
Teacher (browser)        Upload Endpoint        DB (Postgres)        Channel Queue        Worker Thread          Pipeline / Engine / ModelHost
      |                        |                     |                     |                    |                             |
      | POST classroom photo   |                     |                     |                    |                             |
      |----------------------->|                     |                     |                    |                             |
      |                        | validate + store    |                     |                    |                             |
      |                        | MoveToPending()      |                     |                    |                             |
      |                        | SaveChanges #1 ----->| Status=Pending (15%)|                    |                             |
      |                        | EnqueueAsync --------------------->| +1 (depth=1)              |                             |
      |<-- 200 (Queued) -------|                     |                     |                    |                             |
      |                        |                     |                     | WaitToReadAsync    |                             |
      |                        |                     |                     |<-- TryRead --------| depth=0 (Interlocked--)     |
      |                        |                     |                     |                    | log "job dequeued"          |
      |                        |                     |                     |                    | CreateAsyncScope()          |
      |                        |                     |                     |                    | GetRequiredService(pipeline)|
      |                        |                     |                     |                    | ProcessAsync() ------------>| load session (Pending)
      |                        |                     |                     |                    |                             | MoveToProcessing() [in mem]
      |                        |                     |<-------------------- SaveChanges #2 ------------------------------------| Status=Processing (25%)
      |                        |                     |                     |                    |                             | ReadObject(image)
      |                        |                     |                     |                    |                             | DetectAsync()
      |                        |                     |                     |                    |                             | GetDetectionSession()
      |                        |                     |                     |                    |                             | EnsureLoaded(): File.Exists?
      |                        |                     |                     |                    |                             | ==> throw FileNotFoundException
      |                        |                     |                     |                    |          (pipeline catch)   |
      |                        |                     |<-------------------- SaveChanges (Failed) ------------------------------| MoveToFailed(); Status=Failed (0%)
      |                        |                     |                     |                    |<-- rethrow -----------------| MarkCompleted()
      |                        |                     |                     |                    | log "job failed" (Error)    |
```

> The diagram shows the **expected** current-source behavior when models are
> missing: the session ends at **Failed (0%)** with an error message. The gap
> that produces the **observed Pending (15%)** is the branch where the worker
> fails **before** `ProcessAsync` reaches SaveChanges #2 — see Root Cause.

---

## Deliverable Summary

| Question | Finding |
|----------|---------|
| **Does `MoveToProcessing()` execute?** | Only if the pipeline body runs. It is correctly wired (`ClassroomRecognitionPipeline.cs:64`) and the `Pending → Processing` transition is legal. It is persisted at COMMIT #2 (line 69) *before* any model file is read. |
| **Does `MoveToFailed()` execute?** | Only for exceptions thrown **inside** `ProcessAsync`'s try block. It is **not** reachable for worker-level failures (scope creation, DI resolution, or the out-of-try `FirstOrDefaultAsync`/`KeyNotFoundException`). |
| **Does `SaveChanges()` execute?** | COMMIT #1 (Pending) always runs on the enqueue side. COMMIT #2 (Processing) and the failure-path commit only run once the pipeline body is entered. |
| **Can `Pending` remain permanently?** | **Yes** — if the per-job work throws before entering the pipeline try, the worker-level `catch (Exception)` (line 47) logs and swallows without updating the session. |

### Root-Cause Hypothesis

1. **Confirmed trigger:** the required ONNX models (`det_10g.onnx`,
   `w600k_r50.onnx`) are missing from the resolved model directory (verified via
   `GET /health`). This guarantees `EnsureLoaded()` throws `FileNotFoundException`
   the moment the pipeline calls `DetectAsync()`.

2. **Key nuance:** with the *current source*, a missing-model failure occurs
   **after** `MoveToProcessing()` + COMMIT #2, so it would drive the session to
   **Failed (0%)** with `ProcessingError = "InsightFace detection model not
   found…"`. The observed **Pending (15%)** is therefore **not** consistent with
   the pipeline body having executed to line 69.

3. **Most probable explanation for the Pending-15% stall** (in priority order):
   - **(a) A worker-level failure before the pipeline try** — e.g. a dependency
     that cannot be resolved for `IClassroomRecognitionPipeline` in the background
     scope, or the out-of-try session load throwing — caught only by the
     worker's log-and-swallow `catch` (line 47), which never marks the session
     Failed. This is the one code path that leaves a session **Pending forever**.
   - **(b) A stale running binary** — the live process (`Abhyanvaya.API.exe`,
     started 16:18) may predate the current pipeline/state-machine wiring, so its
     runtime behavior may differ from the source traced here.

### Read-Only Verification Steps (no code changes)

To disambiguate (a) vs (b) vs the normal missing-model path, inspect the running
API's console/Debug output for the affected `SessionId` and look for, in order:

1. `Classroom recognition job enqueued… QueueDepth=…` — confirms enqueue.
2. `Classroom recognition job dequeued… QueueDepth=…` — confirms the worker
   pulled the job (log #5).
3. Then **either**:
   - `Attendance session created…` / a `SaveChanges` for `Status=Processing`
     followed by `Classroom recognition job failed…` (**Error**) whose exception
     is `FileNotFoundException` → the normal missing-model path (session should be
     **Failed**, not Pending); **or**
   - `Classroom recognition job failed…` (**Error**) with an exception thrown
     from scope/DI resolution or `KeyNotFoundException`, and **no** intervening
     `Status=Processing` save → confirms hypothesis (a): the session is stuck
     **Pending** because the failure occurred before `MoveToProcessing()` was
     persisted and the worker-level catch never updates the session.

*(This document is investigation-only; the remediation — placing the ONNX models,
and separately deciding whether the worker-level catch should also mark the
session Failed — is intentionally out of scope here.)*
