# AI20.PHASE2.0.8 — Result Writer Future-Proofing Review

**Type:** Contract design review only. No production code was written or modified. The interfaces and method signatures discussed below are **proposed design options**, not implemented contracts.

**Decision:** Keep `IEnrollmentResultWriter` limited to `WriteSuccessAsync`, `WriteFailureAsync`, and `WriteRetryAsync`. Do not add `WritePartialFailureAsync`, `WriteCleanupAsync`, or `WriteRollbackAsync`. The first has no distinct business outcome in the current pipeline; orphan-media cleanup belongs to a future periodic reconciliation service; and undoing an already-committed enrollment is a compensating business operation, not a transaction rollback.

---

## 1. Baseline Contract and Invariants

The baseline in `AI20_PHASE2_ENGINE_CONTRACTS.md` establishes this sequence:

1. `IEnrollmentStorageService.PersistEnrollmentPhotoAsync` writes the photo to object storage.
2. The storage method returns a non-null, non-empty key only after the write succeeds; if nothing was written, it throws.
3. `IEnrollmentEmbeddingService.GenerateAsync` produces the embedding in memory.
4. `IEnrollmentResultWriter.WriteSuccessAsync` executes one database transaction containing:
   - the `StudentFaceEmbedding` insert;
   - the `Student.PhotoKey` and `Student.PhotoUploadedUtc` update;
   - the `StudentEnrollmentItem` completion and metadata update; and
   - the `StudentEnrollmentBatch` counter update.

This ordering deliberately chooses the safe failure direction: an upload may exist without a database reference, but the database must never contain a `PhotoKey` for an object that was not successfully written.

The existing result-writer methods model all current item-level outcomes:

| Method | Item-level meaning | Database boundary |
|---|---|---|
| `WriteSuccessAsync` | The item completed and has a durable photo reference plus embedding | One all-or-nothing transaction across embedding, student, item, and batch |
| `WriteFailureAsync` | The item reached a terminal expected failure | One short transaction across item outcome and counters |
| `WriteRetryAsync` | The item reached a transient failure and must retry, or reached the retry cap | One short transaction across retry state, item outcome, and counters |

`IEnrollmentResultWriter` is therefore an item-outcome finalizer. It is not a general storage lifecycle manager, administrative enrollment command service, or transaction-control facade.

---

## 2. `WritePartialFailureAsync` Scenario Analysis

### 2.1 Concrete candidate scenarios

The strongest apparent example is:

1. photo validation succeeds;
2. `PersistEnrollmentPhotoAsync` succeeds;
3. embedding generation fails; and
4. the item is written as `RetryRequired` or `Failed`.

Another apparent example is:

1. upload and embedding generation succeed;
2. `WriteSuccessAsync` begins;
3. one database operation fails; and
4. the success transaction rolls back.

Both involve completed technical work before the item fails, but neither creates a distinct **partial business outcome**. The item has not enrolled successfully until all success invariants commit. Its durable item status remains either `RetryRequired` or `Failed`, which the existing methods already express.

### 2.2 Why upload-success/embedding-failure is not a partial result

An uploaded object is an external side effect, not a partially completed enrollment state. If embedding generation fails, the orchestrator should classify the failure and call:

- `WriteRetryAsync` for a transient embedding-engine failure; or
- `WriteFailureAsync` if policy determines that the failure is terminal.

The unreferenced object is a storage-hygiene concern. Encoding that concern as `PartialFailure` on the item would mix two independent state machines:

- the enrollment item's business outcome; and
- the storage object's reference/cleanup lifecycle.

It would also weaken the invariant that `Completed` is the only successful enrollment state. No current dashboard, counter, retry rule, or domain enum defines a partially enrolled student, and no caller has a concrete recovery action that requires such a state.

### 2.3 Why a rolled-back success write is not a partial result

When `WriteSuccessAsync` fails, its database transaction rolls back the embedding insert, student update, item completion, and counter update together. From the database's perspective, none of the success result was written. The orchestrator can then use the existing retry/failure path. The uploaded media may remain, but that is again an external orphan rather than a partially committed item.

### 2.4 Recommendation

Do **not** add `WritePartialFailureAsync` to any interface now. `WriteFailureAsync` and `WriteRetryAsync` already represent the real item outcome, while orphan handling must remain separate. A partial-result method should be reconsidered only if the domain later introduces an explicit, user-visible partial state with its own transition rules, counters, retry semantics, and operator action.

---

## 3. `WriteCleanupAsync` Scenario Analysis

### 3.1 What cleanup would actually mean

The baseline explicitly states that a rolled-back success write can leave an orphan storage object and treats it as a future orphan-audit concern. Cleanup in this design is therefore primarily about **orphan media**, not partially committed database rows.

There are two current paths to an unreferenced enrollment object:

| Path | Sequence | Result |
|---|---|---|
| Failure after upload, before success write | upload succeeds → embedding generation or another later stage fails → `WriteRetryAsync`/`WriteFailureAsync` records the item outcome | Object exists; no `StudentEnrollmentItem.PhotoKey` or `Student.PhotoKey` references it |
| Success-write rollback | upload succeeds → embedding generation succeeds → `WriteSuccessAsync` starts → any database operation fails → the entire transaction rolls back | Object exists; embedding/student/item/batch success changes do not |

The second path is the exact baseline risk. More explicitly:

`IEnrollmentStorageService.PersistEnrollmentPhotoAsync` succeeds → an object now exists → `IEnrollmentResultWriter.WriteSuccessAsync` executes its database transaction → the transaction fails and rolls back for any reason → no database row records the returned key.

This is the same safe asymmetry documented for recognition thumbnails in AI18: upload-before-row prevents a dangling database key, while a later processing or database failure can leave an orphaned object. AI18 also establishes that deterministic retry may overwrite the same key and naturally resolve some orphans, but never-retried or permanently failed work can leave them indefinitely.

### 3.2 Detection

Cleanup must not be a blind delete in the request-scoped result writer. A future reconciliation sweep should:

1. enumerate objects under the enrollment/student-photo key space;
2. build the set of live keys referenced by `StudentEnrollmentItem.PhotoKey` and, where relevant, `Student.PhotoKey`;
3. identify storage keys with no live database reference;
4. apply a safety age/grace period so an in-flight upload is not deleted before its result transaction runs;
5. report candidates before deletion, with tenant, key, age, and correlation metadata where available; and
6. delete confirmed orphans idempotently, recording success/failure for retry.

Comparing only against `StudentEnrollmentItem.PhotoKey` is insufficient if the same student-photo key space is also used by manual uploads or other legitimate `Student.PhotoKey` values. The audit must understand all authoritative references for that key space.

The existing `IMediaStorageService.DeleteObjectAsync` already provides the delete capability, so deletion itself is not a new storage primitive. However, `IMediaStorageService` does not expose object enumeration. A future sweep would need a suitable storage-listing abstraction or an upload ledger; that capability should be designed with the audit milestone rather than smuggled into the result writer.

### 3.3 Ownership

`IEnrollmentResultWriter` is the wrong owner because:

- it owns short database finalization transactions, not object-storage enumeration or deletion;
- `WriteSuccessAsync` may throw before a request-scoped cleanup call can be durably scheduled;
- immediate cleanup can race a retry or another in-flight attempt using the deterministic key;
- deleting storage cannot participate atomically in the database transaction; and
- a periodic sweep can recover failures after process termination, whereas an inline cleanup call cannot.

The appropriate future boundary is a periodic service such as:

```csharp
public interface IEnrollmentOrphanAuditService
{
    Task<EnrollmentOrphanAuditResult> ReconcileAsync(
        EnrollmentOrphanAuditRequest request,
        CancellationToken cancellationToken = default);
}
```

The method should support report-only mode before destructive cleanup and should be called by a scheduled background sweep analogous to the periodic `StuckEnrollmentJobRecoveryService` pattern. If an explicit deletion method is preferred, `CleanupOrphansAsync` belongs on this interface, not on `IEnrollmentResultWriter`.

### 3.4 Recommendation

Do **not** add `WriteCleanupAsync` to `IEnrollmentResultWriter`. When storage hygiene becomes an operational requirement, add `IEnrollmentOrphanAuditService.ReconcileAsync` (or `CleanupOrphansAsync`) as a separate periodic, cross-item contract. No new low-level delete capability is required because `IMediaStorageService.DeleteObjectAsync` already exists; reconciliation/listing, safety policy, observability, and retry orchestration are the actual missing capabilities.

---

## 4. Orphan Media Resolution

### 4.1 Cause

Orphan media is possible because object storage and the relational database do not share an atomic transaction. The contract intentionally uploads first:

`PersistEnrollmentPhotoAsync` succeeds → storage object exists → `WriteSuccessAsync` fails/rolls back → no `Student`, `StudentEnrollmentItem`, or embedding-linked success state references that key.

It can also arise when any stage between upload and successful finalization fails. This is acceptable relative to the more dangerous inverse: a committed database key whose object was never written.

### 4.2 Detection and cleanup policy

The recommended audit is a periodic set reconciliation between stored keys and authoritative database references, partitioned by tenant and protected by a grace period. The first operational mode should report only. Deletion should be enabled only after key ownership, deterministic retry behavior, manual-photo references, retention, and in-flight race handling are proven.

The AI18 precedent supports this posture: the analogous recognition flow documents orphaned uploaded thumbnails as low-severity storage hygiene, recommends a scheduled audit if measurable, and keeps cleanup outside the request-scoped persistence service.

### 4.3 Correct owner

A future `IEnrollmentOrphanAuditService` owns detection and cleanup policy. It may compose:

- a read model of all valid enrollment/student photo references;
- a storage inventory abstraction;
- `IMediaStorageService.DeleteObjectAsync`;
- a clock and configurable grace period; and
- audit/metrics output.

The result writer may emit structured evidence that helps reconciliation, but it must not own the sweep.

---

## 5. Orphan Embeddings Resolution

### 5.1 Can the current transaction design create one?

**No.** Under the baseline contract, a `StudentFaceEmbedding` row cannot become an orphan merely because the enclosing success write fails.

The embedding insert, `Student.PhotoKey` update, `StudentEnrollmentItem` update (including `StudentFaceEmbeddingId`), and batch-counter update are all inside the same `IUnitOfWork.ExecuteInTransactionAsync` call. If any operation fails, automatic rollback removes the embedding insert together with every other uncommitted success mutation. There is no committed embedding row left to clean up.

This answer depends on implementations honoring the stated contract: `IEmbeddingStorage` must participate in the same database context/ambient transaction and must not open or commit an independent transaction. An implementation that commits the embedding separately would violate the baseline result-writer transaction boundary; it would be a defect to fix, not a reason to add `WriteCleanupAsync`.

### 5.2 What can exist without a row

The generated vector can exist transiently in memory before `WriteSuccessAsync`, but it is not a durable orphan. If the transaction rolls back, no `StudentFaceEmbedding` row remains. Therefore, a dedicated embedding-cleanup write path is unnecessary for the proposed Phase 2 design.

### 5.3 Later deletion is a different problem

An embedding that was validly committed and later needs removal during an administrative un-enrollment is not an orphan caused by transaction failure. It is durable state that must be removed by a new compensating business operation with authorization, audit, concurrency, and retention rules.

---

## 6. `WriteRollbackAsync` Scenario Analysis

### 6.1 Database rollback is already owned by `IUnitOfWork`

`IUnitOfWork.ExecuteInTransactionAsync` provides automatic commit-or-rollback behavior. `IUnitOfWork.RollbackAsync` also exists for an active ambient transaction. A result-writer-level `WriteRollbackAsync` would duplicate transaction mechanics without defining a new domain outcome.

During `WriteSuccessAsync`, rollback means discarding **uncommitted** database changes in the currently active transaction. That is infrastructure behavior, not another result to write. Attempting to record a rollback result inside the failing transaction would be self-defeating because that record would roll back too; recording retry/failure afterward is already handled by `WriteRetryAsync` or `WriteFailureAsync` in a new short transaction.

### 6.2 Undoing committed enrollment is not rollback

A plausible future request is: a SuperAdmin un-enrolls a student, removes the active embedding, clears or restores `Student.PhotoKey`, updates enrollment history, and possibly deletes media.

The original enrollment transaction has already committed and no longer exists. `RollbackAsync` cannot rewind it. The correct operation is a **compensating transaction**: a new, independently authorized transaction that applies inverse business effects to current state.

Calling this `WriteRollbackAsync` would conflate two materially different concepts:

| Concept | Applies when | Meaning |
|---|---|---|
| ACID rollback | Original database transaction is still active and uncommitted | Discard that transaction's writes |
| Compensating transaction | Original transaction already committed | Execute new business operations intended to semantically undo or offset the earlier result |

The future boundary should be explicit, for example:

```csharp
public interface IStudentUnenrollmentService
{
    Task<UnenrollmentResult> UnenrollAsync(
        UnenrollmentRequest request,
        CancellationToken cancellationToken = default);
}
```

That service would need its **own new database transaction**, separate from the original enrollment transaction. It would also need a defined history model before it could “revert” `PhotoKey`: clearing the current key is possible, but restoring a previous key requires that the prior value and media lifecycle were deliberately retained.

Any post-commit media deletion would remain a separate storage compensation. A robust design would commit authoritative DB un-enrollment state first, then perform or durably schedule idempotent media cleanup; a failed deletion leaves a recoverable orphan rather than a dangling DB reference.

### 6.3 Recommendation

Do **not** add `WriteRollbackAsync` to `IEnrollmentResultWriter`. Use `IUnitOfWork` for rollback of the active enrollment transaction. If product requirements later introduce administrative un-enrollment, add `IStudentUnenrollmentService.UnenrollAsync` as a dedicated command with its own transaction and explicit compensation/cleanup policy.

---

## 7. Transaction Boundaries

The current boundaries remain correct and should not be widened:

| Operation | Boundary | Reason |
|---|---|---|
| Photo upload | No database transaction | Object storage cannot join the relational transaction; long network I/O must not hold DB locks |
| Embedding generation | No database transaction | CPU work produces an in-memory value only |
| `WriteSuccessAsync` | One `ExecuteInTransactionAsync` call | Atomically inserts embedding, updates student, completes item, and updates counters |
| `WriteFailureAsync` / `WriteRetryAsync` | One short transaction each | Atomically records item disposition and counter changes |
| Orphan audit/cleanup | Future periodic operation, outside result-write transactions | Cross-item storage reconciliation is eventually consistent and independently retryable |
| Administrative un-enrollment | Future new transaction | It compensates for an earlier committed transaction; it cannot reuse or roll back that transaction |

None of the three candidate methods should expand `WriteSuccessAsync` across storage and database boundaries. Such a transaction is impossible with the current abstractions and would hold database resources across external I/O.

If `WriteSuccessAsync` throws, its transaction rolls back. A subsequent `WriteRetryAsync` or `WriteFailureAsync` is necessarily a separate transaction because the failed transaction cannot be reused to durably record the new outcome.

---

## 8. Compensating Actions

In the Saga/compensating-transaction pattern, a **compensating action** is a new operation that semantically reverses or mitigates an earlier side effect after the original transactional boundary has ended. It is not a time-reversal mechanism and does not guarantee restoration of every external observation.

Two future needs in this review fit that definition:

1. **Cleaning up an orphaned storage object.** The upload already succeeded outside the database transaction. A later delete compensates for that external side effect.
2. **Undoing an already-committed enrollment.** A new transaction removes/deactivates the embedding and changes current student state. It compensates for the original committed enrollment.

This framing drives the naming:

- use `Reconcile`, `CleanupOrphans`, or `Unenroll` to describe the actual business/operational action;
- reserve `Rollback` for an active uncommitted transaction; and
- do not call a failed item's ordinary terminal/retry result a `PartialFailure` merely because an external side effect awaits compensation.

Compensation may itself fail and must therefore be idempotent, observable, and retryable. Those requirements are another reason not to hide compensation behind a request-scoped result-writer method.

---

## 9. Final Recommendations

| Candidate method | Recommendation | Target interface | Justification |
|---|---|---|---|
| `WritePartialFailureAsync` | **Do not add** | None | There is no current partial enrollment business state. Upload-success followed by embedding/result-write failure is still an item failure or retry; the unreferenced object is a separate cleanup concern. |
| `WriteCleanupAsync` | **Do not add to the result writer. Add a differently scoped capability only when operationally required.** | Future `IEnrollmentOrphanAuditService.ReconcileAsync` or `CleanupOrphansAsync` | Cleanup is periodic cross-item storage reconciliation, not item-result persistence. `DeleteObjectAsync` already exists; inventory, reference comparison, grace periods, reporting, and retry policy are the missing concerns. |
| `WriteRollbackAsync` | **Do not add** | Existing `IUnitOfWork` for active rollback; future `IStudentUnenrollmentService.UnenrollAsync` for committed-state reversal | Active transaction rollback is already automatic/explicit in `IUnitOfWork`. Undo after commit is a new compensating transaction and must be named and authorized as an un-enrollment operation. |

The recommended Phase 2 `IEnrollmentResultWriter` contract remains unchanged:

```csharp
public interface IEnrollmentResultWriter
{
    Task WriteSuccessAsync(
        EnrollmentSuccessWrite write,
        CancellationToken cancellationToken = default);

    Task WriteFailureAsync(
        EnrollmentFailureWrite write,
        CancellationToken cancellationToken = default);

    Task WriteRetryAsync(
        EnrollmentFailureWrite write,
        CancellationToken cancellationToken = default);
}
```

This narrow contract preserves one responsibility: atomically finalize the current attempt's durable item outcome.

---

## Constraints Confirmed

- No `.cs`, `.csproj`, `.tsx`, `.ts`, or other production/non-markdown file was created or modified.
- No interface, implementation, DI registration, background service, endpoint, database schema, migration, storage operation, or business logic was implemented.
- Every C# block in this document is illustrative/proposed only.
- The review uses the existing `IUnitOfWork` transaction primitives and the existing `IMediaStorageService.DeleteObjectAsync` capability without changing either contract.
- The recommendation keeps `IEnrollmentResultWriter` unchanged and separates future orphan reconciliation and administrative un-enrollment into appropriately scoped contracts.
- Deliverable produced: `docs/AI20_PHASE2_RESULT_WRITER_REVIEW.md`.
