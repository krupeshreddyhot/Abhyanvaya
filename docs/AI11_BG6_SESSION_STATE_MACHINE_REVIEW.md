# AI11.BG.6 — AttendanceSession State Transition Verification (Investigation Only)

> **Scope:** Read-only investigation. No source, database, or business logic was
> modified and no logging was added. This document verifies whether
> `AttendanceSession.Status` actually changes in PostgreSQL during runtime, and
> whether a session can remain permanently in `Pending`.

---

## Background Facts

- `AttendanceSession.Status` has a **private setter** (`Domain/Entities/AttendanceSession.cs:145`);
  it can only change through the state-machine methods in
  `Domain/Entities/AttendanceSession.StateMachine.cs`.
- The single choke point is `TransitionTo(target)` (`AttendanceSession.StateMachine.cs:83`),
  which throws `DomainException` for any transition not whitelisted in
  `CanTransitionTo` (lines 97-119), and blocks all changes once `Completed` or
  `Cancelled` (`EnsureNotCompleted`/`EnsureNotCancelled`, lines 121-135).
- Persistence is via EF Core. `ApplicationDbContext.SaveChangesAsync` stamps a
  fresh `RowVersion` (optimistic-concurrency token) on every modified
  `AttendanceSession` (`Persistence/ApplicationDbContext.cs:338-343`).

---

## STEP 1 — Every Writer of `AttendanceSession.Status`

All writes go through the `Move*` / `Approve` / `Complete` / `Cancel` methods
(no direct assignment exists outside the entity). Writers relevant to the AI
photo flow:

| Caller (class · method · line) | Transition | Persisted by (SaveChanges) | In a DB transaction? |
|---|---|---|---|
| `AttendanceSessionCreator.CreateAndUploadClassroomPhotoAsync` (:126) | `Draft → Pending` | `ConcurrencyExceptionHelper.SaveChangesAsync` (:127) | **Yes** — `ExecuteInTransactionAsync` |
| `AttendancePhotoService.UploadClassroomPhotoAsync` (:82) | `Draft → Pending` | `ConcurrencyExceptionHelper.SaveChangesAsync` (:85) | **Yes** — `ExecuteInTransactionAsync` |
| `ClassroomRecognitionPipeline.ProcessAsync` (:61) | `Draft → Pending` (defensive; only if still Draft) | shares COMMIT at :69 | No (autonomous SaveChanges) |
| `ClassroomRecognitionPipeline.ProcessAsync` (:64) | `Pending → Processing` | `ConcurrencyExceptionHelper.SaveChangesAsync` (:69) | No (autonomous SaveChanges) |
| `ClassroomRecognitionPipeline.ProcessAsync` (:127) | `Processing → AwaitingReview` | `ConcurrencyExceptionHelper.SaveChangesAsync` (:128) | No (autonomous SaveChanges) |
| `ClassroomRecognitionPipeline.ProcessAsync` (:143, catch) | `Processing → Failed` | `ConcurrencyExceptionHelper.SaveChangesAsync` (:144) | No (autonomous SaveChanges) |
| Finalization service (`Approve`) | `AwaitingReview → Approved` | finalizer SaveChanges | Yes |
| Finalization / completion (`Complete`) | `Approved → Completed` | finalizer SaveChanges | Yes |
| Cancellation (`Cancel`) | `* → Cancelled` | caller SaveChanges | Yes |

### Expected happy-path chain

```
Draft → Pending → Processing → AwaitingReview → Approved → Completed
                      └────────────────────────────→ Failed   (on any pipeline exception)
```

---

## STEP 2 — The `Move*` Methods: Caller, Conditions, Transaction Scope

| Method | Definition | Callers | Legal source states (`CanTransitionTo`) | Transaction scope |
|--------|-----------|---------|------------------------------------------|-------------------|
| `MoveToPending()` | `StateMachine.cs:12` | `AttendanceSessionCreator:126`, `AttendancePhotoService:82`, `Pipeline:61` | `Draft` only | Enqueue-side callers: inside `ExecuteInTransactionAsync`. Pipeline: autonomous. |
| `MoveToProcessing()` | `StateMachine.cs:15` | `Pipeline:64` | `Pending` only | Autonomous SaveChanges (no ambient tx) |
| `MoveToAwaitingReview()` | `StateMachine.cs:18` | `Pipeline:127` | `Processing` only | Autonomous SaveChanges (no ambient tx) |
| `MoveToFailed()` | `StateMachine.cs:21` | `Pipeline:143` (catch) | `Draft`, `Pending`, or `Processing` | Autonomous SaveChanges (no ambient tx) |
| `Approve()` | `StateMachine.cs:26` | finalization | `AwaitingReview`/`Draft`/`Pending`/`Processing`/`Failed` | Finalizer transaction |
| `Complete()` | `StateMachine.cs:51` | completion | `Approved` | Finalizer transaction |
| `Cancel()` | `StateMachine.cs:60` | cancellation | any except `Completed`/`Cancelled` | Caller transaction |

**Note on the pipeline:** `ProcessAsync` does **not** open its own
`ExecuteInTransactionAsync`. Each `SaveChangesAsync` call therefore commits
**autonomously and immediately** (EF Core default). Consequently `Processing`
(line 69) is durably committed on its own, independent of whether the later
`AwaitingReview`/`Failed` commit succeeds.

---

## STEP 3 — Does `MoveToProcessing()` Commit Immediately?

**Yes.** `ClassroomRecognitionPipeline.cs`:

```
64:  session.MoveToProcessing();               // in-memory: Pending → Processing
65-68: StartedUtc / RecognitionProvider / RecognitionModel / PipelineVersion   (config strings only)
69:  await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);   // COMMIT — Status=Processing
```

Because there is no ambient transaction around the pipeline, line 69 is a
standalone commit. Crucially, **nothing between line 64 and line 69 touches a
model file** — the provider/model/version assignments read configuration strings,
not ONNX files — so `Processing` is guaranteed to be persisted **before**
`DetectAsync()` (line 74) can throw a missing-model exception.

---

## STEP 4 — Does `MoveToFailed()` Always Commit, Even When Recognition Throws?

**Only when the exception is thrown *inside* `ProcessAsync`'s try block.**

The failure path (`ClassroomRecognitionPipeline.cs:138-147`):

```
catch (Exception ex)
{
    session.ProcessingError = ex.Message;
    session.CompletedUtc = DateTime.UtcNow;
    session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;
    session.MoveToFailed();                                  // Processing → Failed
    await ConcurrencyExceptionHelper.SaveChangesAsync(...);  // COMMIT — Status=Failed
    _queue.MarkCompleted(session.Id);
    throw;
}
```

- A `FileNotFoundException` from `EnsureLoaded()` (missing models) **does** enter
  this catch, so it **does** persist `Failed`.
- **But** this catch cannot fire for failures that happen **before** the try is
  entered (worker `CreateAsyncScope()`, `GetRequiredService<IClassroomRecognitionPipeline>()`,
  or the `FirstOrDefaultAsync … ?? throw KeyNotFoundException` on lines 53-55,
  which sits **outside** the try). Those are caught only by the worker-level
  `catch (Exception)` (`ClassroomRecognitionBackgroundService.cs:47`), which
  **logs and swallows and never calls `MoveToFailed()` or `SaveChanges()`.**

**Edge case within the catch itself:** if `MoveToFailed()`'s own
`SaveChangesAsync` (line 144) throws (e.g. `DbUpdateConcurrencyException` →
`ConcurrencyConflictException`), the `Failed` write is lost; but by then
`Processing` (line 69) is already committed, so the row would read **Processing
(≈25%)**, not Pending.

---

## STEP 5 — Can `SaveChanges()` Be Skipped?

| Cause | Effect on `Status` persistence |
|-------|-------------------------------|
| **Early return** | None in `ProcessAsync` before the commits; N/A. |
| **Exception before line 69** *(worker scope/DI resolution, or `FirstOrDefaultAsync`/`KeyNotFoundException` at :53-55)* | **COMMIT #2 skipped.** Session stays at last committed state = **Pending (15%)**. Worker catch only logs. **← permanent-Pending path.** |
| **Exception at/after line 74 (DetectAsync)** | COMMIT #2 (Processing) already durable; failure-path commit sets **Failed (0%)**. |
| **Disposed scope** | The `await using` scope disposes only after `ProcessAsync` returns/throws; the DbContext lives for the whole call, so in-flight `SaveChanges` are unaffected. Premature disposal is not present in this code path. |
| **Cancelled token** (host shutdown) | `OperationCanceledException` → worker `catch` at :43 `break`s. In-flight `SaveChanges` may be cancelled; the session remains at its last committed state (Pending or Processing). No `Failed` is written. |
| **Failed transaction** (enqueue side only) | `ExecuteInTransactionAsync` rolls back and rethrows; the create/upload is aborted, so no half-written Pending row (all-or-nothing). The pipeline itself uses no transaction. |
| **Concurrency conflict** (`DbUpdateConcurrencyException`) | Mapped to `ConcurrencyConflictException` and rethrown; the offending commit is lost, leaving the prior committed state. |

---

## STEP 6 — Complete State Machine (with callers)

```
                         ┌──────────────────────────────────────────────┐
                         │                                              │
                    (create)                                            │
                         │                                              │
                         ▼                                              │
                     ┌───────┐   MoveToPending()                        │
                     │ Draft │──────────────────────┐                  │
                     └───┬───┘  (Creator:126 /       │                  │
                         │       PhotoService:82 /    │                  │
                         │       Pipeline:61)         ▼                  │
                         │                        ┌─────────┐           │
                         │                        │ Pending │  15%      │
                         │                        └────┬────┘           │
                         │      MoveToProcessing()      │ (Pipeline:64)  │
                         │                              ▼                │
                         │                        ┌────────────┐        │
                         │                        │ Processing │ 25-90% │
                         │                        └─────┬──────┘        │
                         │      MoveToAwaitingReview()   │ (Pipeline:127)│
                         │                               ▼               │
                         │                        ┌────────────────┐    │
                         │                        │ AwaitingReview │100% │
                         │                        └───────┬────────┘    │
                         │            Approve()           │ (finalizer)  │
                         │                                ▼              │
                         │                          ┌──────────┐        │
                         │                          │ Approved │        │
                         │                          └────┬─────┘        │
                         │              Complete()        │ (finalizer)  │
                         │                                ▼              │
                         │                          ┌───────────┐       │
                         │                          │ Completed │ (terminal)
                         │                          └───────────┘       │
                         │                                              │
   MoveToFailed() from Draft / Pending / Processing (Pipeline:143 catch)│
                         └───────────────► ┌────────┐ 0%               │
                                           │ Failed │ ─── Approve() ───►│ (re-approvable)
                                           └────────┘                   │
                                                                        │
   Cancel() from Draft/Pending/Processing/AwaitingReview/Approved/Failed→ ┌───────────┐
                                                                          │ Cancelled │ (terminal)
                                                                          └───────────┘
```

Legal transitions are exactly those in `CanTransitionTo` (`StateMachine.cs:97-119`).
`Completed` and `Cancelled` are terminal (guarded by `EnsureNotCompleted` /
`EnsureNotCancelled`).

---

## STEP 7 — Exception Paths → Expected Final `Status`

| Failure scenario | Where it throws | Caught by | Expected final `AttendanceSession.Status` |
|------------------|-----------------|-----------|-------------------------------------------|
| **Model missing** (`FileNotFoundException` from `EnsureLoaded`) | Pipeline line 74, **inside** try (after COMMIT #2) | Pipeline catch (:138) | **Failed (0%)**, `ProcessingError` = "…model not found…" |
| **Recognition/detection error** (bad image, parse error) | Pipeline lines 74-124, inside try | Pipeline catch (:138) | **Failed (0%)** |
| **Database exception during Processing save** (line 69) | Pipeline line 69 | Pipeline catch (:138) → then MoveToFailed save may also fail | If line 69 failed, Processing was **not** committed → catch tries `Failed` save; if that also fails, rethrow → row stays **Pending (15%)** |
| **Database concurrency conflict** | Any pipeline SaveChanges | `ConcurrencyExceptionHelper` → rethrow → pipeline/worker catch | Last successfully-committed state (Pending or Processing) |
| **Worker-level failure** (scope/DI resolution, or out-of-try `FirstOrDefaultAsync`/`KeyNotFoundException`) | Worker lines 39-40 or Pipeline lines 53-55 (outside try) | Worker catch (:47) — **log only** | **Pending (15%) — permanent** |
| **Cancellation / host shutdown** | Any awaited call | Worker catch (:43) → `break` | Last committed state (Pending or Processing); **no `Failed` written** |
| **Timeout** (surfaces as an exception inside try) | Pipeline inside try | Pipeline catch (:138) | **Failed (0%)** |

---

## Deliverable Summary

- **Every status transition** is routed through the entity state machine
  (`TransitionTo`), guaranteeing only whitelisted transitions and blocking
  changes after `Completed`/`Cancelled`.
- **Every `SaveChanges`** on the AI path: COMMIT #1 (`Pending`, transactional,
  enqueue side); COMMIT #2 (`Processing`, autonomous, pipeline line 69); then
  either `AwaitingReview` (line 128) or `Failed` (line 144), both autonomous.
- **`MoveToProcessing()` commits immediately** at line 69, **before** any model
  file is read.
- **`MoveToFailed()` reliably commits** — but **only** for exceptions thrown
  inside `ProcessAsync`'s try block.

### Can `Pending` Remain Permanently? — **Yes.**

`Pending` is persisted (COMMIT #1) on upload. It is only advanced by
`MoveToProcessing()` + COMMIT #2 **inside** the pipeline try. If the job fails
**before** entering that try — worker scope creation, DI resolution of
`IClassroomRecognitionPipeline`, or the out-of-try session load — the only
handler is the worker-level `catch (Exception)` (line 47), which **logs and
swallows and never marks the session `Failed`**. There is no retry, no timeout
sweeper, and no reconciliation job, so the row remains `Pending (15%)`
indefinitely.

### Root-Cause Hypothesis

1. The **missing ONNX models** (confirmed via `GET /health`) are the definite
   functional blocker for recognition.
2. However, the missing-model exception fires **after** `Processing` is committed,
   so on the current source it should yield **Failed (0%)** — **not** the observed
   **Pending (15%)**.
3. The observed permanent-`Pending` therefore points to a failure occurring
   **before** `MoveToProcessing()` is persisted, landing in the worker's
   log-and-swallow catch (the only code path that can strand a session at
   `Pending`) — **or** the live process is a stale build predating this wiring.
   Confirm by reading the running API's Debug output for the affected `SessionId`
   (see AI11.BG.5 → "Read-Only Verification Steps"): the presence/absence of a
   `Status=Processing` save immediately before the `Classroom recognition job
   failed` error line distinguishes the two cases.

*(Investigation only — no remediation applied. Any fix, such as providing the
models or extending the worker-level catch to mark the session `Failed`, is out
of scope for this document.)*
