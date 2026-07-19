# AI20.PHASE2.0.13 — Pipeline Stage Enumeration

**Type:** Contract design and reconciliation only. No production code was written or modified. Every C# block below is an illustrative proposal; no enum, DTO, entity, migration, worker, pipeline stage, telemetry emitter, or UI behavior has been implemented.

**Decision:** Keep the existing Domain `EnrollmentStatus` unchanged as the durable business lifecycle. Formalize one separate Application-layer `EnrollmentPipelineStage` enum by consolidating the overlapping stage identities already proposed in AI20.PHASE2.0.2, AI20.PHASE2.0.3, and AI20.PHASE2.0.5. The consolidated enum is attempt-scoped and diagnostic only: it may identify a live in-memory `CurrentStage` or label telemetry/log records, but it must never become a second persisted item-status column or drive claiming, retry, dashboard counts, or audit status transitions.

---

## 0. Reviewed baseline

This decision is reconciled against the implemented Domain model and the Phase 2 contract documents:

- `EnrollmentStatus` is the real persisted item lifecycle: `Pending`, `Downloading`, `Downloaded`, `Validating`, `Embedding`, `Completed`, `Failed`, `RetryRequired`, and `Cancelled`.
- `BatchStatus` is a separate batch aggregate lifecycle: `Created`, `Running`, `Completed`, `PartiallyFailed`, and `Cancelled`. It does not describe per-attempt execution position.
- `StudentEnrollmentItem.Status` is typed as `EnrollmentStatus`. Its durable timeline also records `CreatedUtc`, `DownloadStartedUtc`, `DownloadedUtc`, `ValidationStartedUtc`, `ValidatedUtc`, `EmbeddingStartedUtc`, and `CompletedUtc`.
- `AI20_PHASE2_ENGINE_CONTRACTS.md` §15 fixes the current core call sequence as download → validate → storage → embedding → finalize. Storage and final result writing are real execution units even though the Domain enum has no `Uploading`/`Storing` or `Persisting`/`Writing` status.
- `AI20_PHASE2_PIPELINE_CONTEXT.md` proposes an immutable `EnrollmentItemContext.CurrentStage` snapshot and a separate `EnrollmentStage` enum for in-flight diagnostics.
- `AI20_PHASE2_PIPELINE_TELEMETRY.md` independently proposes `EnrollmentPipelineStage` to label measured units of work.
- `AI20_PHASE2_PIPELINE_STAGE_DESIGN.md` proposes `EnrollmentStagePhase`, `StageName`, and `Order` for seven optional stages around the five fixed core stages.

An important baseline detail is that durable status is already intentionally coarse. The entity timestamps capture several boundaries without requiring one persisted status per internal call. Conversely, the timestamp set is not a complete execution-stage model: there is no storage timestamp pair, no explicit embedding-completed timestamp, and no result-write-started timestamp. Neither `EnrollmentStatus` nor the timestamps can precisely answer which internal call a worker is executing at an arbitrary instant.

There is also a contract discrepancy that this design must not hide: the implemented Domain enum contains `Downloaded`, while the current Phase 2 sequence diagram transitions directly from `Downloading` to `Validating`. That transition policy must be resolved when the orchestrator is implemented, but it does not justify deriving execution-stage identity from status. The two vocabularies have different responsibilities.

---

## 1. Business status versus execution stage

| Dimension | `EnrollmentStatus` | `EnrollmentPipelineStage` |
|---|---|---|
| Question answered | “What is the current durable state of this student's enrollment?” | “Which internal unit of work is this worker executing on this attempt right now?” |
| Layer | Domain | Application |
| Lifetime | Across transactions, process restarts, delays, retries, and deployments | One live processing attempt; copied into logs/telemetry as a point-in-time observation |
| Authority | Authoritative business state | Non-authoritative diagnostic label |
| Persistence | Stored in `StudentEnrollmentItem.Status` | Never stored as item lifecycle state and never added as a second entity column |
| Behavioral use | Claim eligibility, retry policy, recovery, terminal-state rules, counters, filters, audit transitions | Log scopes, traces, per-stage timing, failure localization, optional-stage identity |
| Between attempts | Meaningful: an item can remain `RetryRequired` for hours | No active stage exists because no worker is executing the item |
| Granularity | Coarse lifecycle checkpoints | Exact core or optional call being executed |
| Typical examples | `Pending`, `RetryRequired`, `Failed`, `Completed` | `Storage`, `ResultWrite`, `Liveness`, `DuplicateDetection` |

`EnrollmentStatus.Downloading`, `.Validating`, and `.Embedding` overlap by name with internal work, but that overlap does not make the execution-stage concept redundant. They are durable checkpoints used for recovery and reporting. They do not represent:

- the storage call between validation and embedding;
- the final transactional result write;
- any of the seven optional AI stages;
- a per-attempt measurement identity;
- the distinction between an item waiting at `RetryRequired` and a worker actively executing a later retry.

The reverse distinction is equally important. `Pending`, `Downloaded`, `RetryRequired`, `Failed`, `Completed`, and `Cancelled` are meaningful durable states, not necessarily live executable steps. In particular, `RetryRequired` is the state between attempts; `Retrying` must not be invented as an execution stage unless a worker is performing a concrete retry-specific operation. A new attempt simply executes `Download`, `Validation`, and subsequent stages again under a new `PipelineExecutionId` and incremented `AttemptNumber`.

---

## 2. Resolved conclusion

`EnrollmentStatus` alone is sufficient for all durable business-state requirements, but it is not sufficient for exact per-attempt diagnostics. A distinct stage enum is therefore needed as a contract, subject to four firm boundaries:

1. It is **not a new Domain concept**. It belongs in `Abhyanvaya.Application.Enrollment` with the context and telemetry contracts.
2. It is **not persisted on `StudentEnrollmentItem`**. `EnrollmentItemContext.CurrentStage` remains an immutable in-memory snapshot. Emitted log or telemetry records may retain the observed label, but those records do not become lifecycle authority.
3. It **never drives business behavior**. Claiming, retry eligibility, recovery, dashboard aggregation, counters, and status transitions continue to use `EnrollmentStatus`.
4. It is **one canonical enum**, not separate `EnrollmentStage`, `EnrollmentPipelineStage`, and free-form `StageName` vocabularies.

This is a consolidation of already-proposed Phase 2 concepts, not a proposal for a second status machine.

---

## 3. Canonical Application-layer enum

The canonical enum should name actual executable units in the fixed core pipeline and the seven optional stage contracts. `Claim` is included because worker claim diagnostics may be measured before `ProcessItemAsync`; `Pipeline` is retained solely as the end-to-end telemetry aggregate established by AI20.PHASE2.0.3.

```csharp
// Illustrative proposal only. No C# file was created or modified.
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Non-authoritative identity of an enrollment unit of work being executed or measured
/// during one processing attempt. Never persisted as StudentEnrollmentItem lifecycle state.
/// </summary>
public enum EnrollmentPipelineStage
{
    Claim = 0,

    // Fixed core stages
    Download = 10,
    Validation = 20,
    Storage = 30,
    Embedding = 40,
    ResultWrite = 50,

    // Optional PostValidation stages
    Liveness = 100,
    Mask = 110,
    Occlusion = 120,
    Spoof = 130,
    FaceQualityRanking = 140,
    FaceNormalization = 150,

    // Optional PostEmbedding stage
    DuplicateDetection = 200,

    // Telemetry aggregate only; never a CurrentStage value
    Pipeline = 1000,
}
```

The numeric gaps are for contract evolution, not execution ordering. Runtime ordering remains explicit configuration plus `Order` within an `EnrollmentStagePhase`; code must not sort stages by enum numeric value.

The example values `Pending`, `Completed`, `Retrying`, and `Cancelled` should **not** appear in this enum. They describe lifecycle/disposition, not a currently executing unit of work. `Downloading`, `Validating`, `Uploading`, `Embedding`, and `Persisting` express the right kind of concept, but noun-form identities (`Download`, `Validation`, `Storage`, `Embedding`, `ResultWrite`) match the telemetry document, the service boundaries, and stable configuration names more clearly.

`EnrollmentItemContext.CurrentStage` should use `EnrollmentPipelineStage?`, with `null` meaning “no pipeline unit is currently executing.” It should be set to a new value through immutable `with` snapshots at stage entry. It should never be set to `Pipeline`, and it should return to `null` only in the caller's completed snapshot if such a snapshot is retained. Durable completion remains `EnrollmentStatus.Completed`.

---

## 4. Redundancy across the prior documents

The prior documents contain two duplicated stage enums and a third stage-identity mechanism:

| Document | Exact proposed identity members | Purpose stated there | Reconciliation |
|---|---|---|---|
| AI20.PHASE2.0.2 Pipeline Context | `EnrollmentStage`: `NotStarted`, `Claimed`, `Downloading`, `Validating`, `Storing`, `Embedding`, `Writing`, `Completed` | Immutable `CurrentStage` snapshots for logs/telemetry | Replace with nullable canonical `EnrollmentPipelineStage`. Drop lifecycle-like `NotStarted`/`Completed`; map `Claimed`→`Claim`, `Downloading`→`Download`, `Validating`→`Validation`, `Storing`→`Storage`, `Writing`→`ResultWrite`. |
| AI20.PHASE2.0.3 Pipeline Telemetry | `EnrollmentPipelineStage`: `Download`, `Validation`, `Storage`, `Embedding`, `ResultWrite`, `Pipeline` | Stage grouping, duration measurement, and span/dependency names | Keep the name and core vocabulary as the canonical base; extend it with `Claim` and the seven known optional stages. |
| AI20.PHASE2.0.5 Pipeline Stage Design | `StageName` strings for `Liveness`, `Mask`, `Occlusion`, `Spoof`, `FaceQualityRanking`, `FaceNormalization`, `DuplicateDetection`; `EnrollmentStagePhase`: `PostValidation`, `PostEmbedding` | Stable config identity plus phase/order of optional stages | Replace the free-form identity property with `EnrollmentPipelineStage Stage`. Retain `EnrollmentStagePhase` and `Order`; they answer placement and ordering, not identity. Configuration serializes canonical enum names. |

The 0.2 and 0.3 enums are the same architectural fact proposed independently with divergent member names and boundaries. They should not coexist. The 0.5 `EnrollmentStagePhase` enum is **not** a duplicate: `PostValidation`/`PostEmbedding` identify extension slots, not the stage executing within a slot. However, 0.5's `StageName` string would create a third naming authority, so it should be replaced by the canonical enum.

The following nearby types also remain distinct:

- `EnrollmentStageOutcome` describes a stage's disposition (`Succeeded`, `Failed`, `RetryRequired`, `Cancelled`), not its identity.
- `EnrollmentStageTimestamp` selects which durable entity timestamp to stamp, not which unit is currently executing.
- `EnrollmentStagePhase` constrains where optional stages run.
- `Order` and configuration determine sequence within a phase.

Consolidation must therefore remove duplicate **identity** vocabularies without collapsing outcome, timestamp selection, phase, or ordering into the canonical enum.

---

## 5. Where each enum is used

| Use site | Mapping | Precise rule |
|---|---|---|
| Database column / persisted entity state | `EnrollmentStatus` | `StudentEnrollmentItem.Status` remains the sole durable item lifecycle. Do not add a persisted `EnrollmentPipelineStage` column. |
| Job-claiming / retry-eligibility queries | `EnrollmentStatus` | Query `Status IN (Pending, RetryRequired)` and atomically claim into the appropriate durable in-flight status. A diagnostic stage must never make an item claimable. |
| SuperAdmin dashboard counts/filters | `EnrollmentStatus` | Total/Embedded-or-Completed/Pending/Failed/RetryRequired and per-batch counters derive from durable status/entity data. Stage telemetry may power a separate operational timeline, never these business tiles. |
| Structured logging / diagnostic traces | **Both** | Log old/new `EnrollmentStatus` for durable transitions and canonical `EnrollmentPipelineStage` for the exact executing/measured unit, together with attempt/correlation identifiers. |
| `EnrollmentItemContext.CurrentStage` | `EnrollmentPipelineStage` | This is the same canonical enum, represented as a nullable immutable snapshot. It is attempt-scoped, in-memory, and non-authoritative. Remove the separate `EnrollmentStage` proposal. |
| `EnrollmentStageResult.Stage` / `EnrollmentStageTelemetry` | `EnrollmentPipelineStage` | Use the same canonical enum as `CurrentStage`; do not define a telemetry-only duplicate. In the current telemetry shape the field belongs on `EnrollmentStageTelemetry.Stage`; an enhanced result reaches it through `Telemetry`. |
| `IEnrollmentPipelineStage` stage ordering | `EnrollmentPipelineStage` | Use canonical `Stage` for identity/configuration. Retain `EnrollmentStagePhase` for extension slot and configured `Order` for sequence. Enum numeric values are not ordering. |
| Audit events (`FromStatus` / `ToStatus`) | **Both, in separate fields** | `FromStatus`/`ToStatus` remain `EnrollmentStatus?` because they record durable business transitions. A diagnostic per-item audit/log event may additionally carry `EnrollmentPipelineStage? Stage`; it must not substitute stage for status. |

`BatchStatus` remains the batch-level counterpart for batch screens and business audit events. It is not interchangeable with either item enum.

---

## 6. Lifecycle and attempt examples

### 6.1 Waiting for an automatic retry

An item may persist as:

```text
EnrollmentStatus = RetryRequired
CurrentStage = null
```

for minutes or hours. That is correct: the durable state says work remains eligible, while the null execution stage says no worker is running it now.

### 6.2 Storage call during a live attempt

While the orchestrator executes `IEnrollmentStorageService.PersistEnrollmentPhotoAsync`:

```text
EnrollmentStatus = Validating
CurrentStage = Storage
```

This apparent mismatch is intentional, not corruption. The business state has not crossed its next durable checkpoint, while the finer diagnostic snapshot identifies the exact internal call. On failure, status becomes `RetryRequired` or `Failed`; the emitted telemetry/log record retains `Stage = Storage`.

### 6.3 Final transactional write

While `IEnrollmentResultWriter.WriteSuccessAsync` performs the terminal transaction:

```text
EnrollmentStatus = Embedding
CurrentStage = ResultWrite
```

Only after the transaction commits does the durable status become `Completed`. There is no need for a persisted `Persisting` status merely to expose this short internal interval.

### 6.4 Optional stage

During liveness evaluation:

```text
EnrollmentStatus = Validating
CurrentStage = Liveness
Phase = PostValidation
```

Here identity (`Liveness`), placement (`PostValidation`), and durable state (`Validating`) are three related but non-interchangeable facts.

---

## 7. Persistence, telemetry, and recovery rules

1. `EnrollmentPipelineStage` must not be added to `StudentEnrollmentItem`, a migration, a batch counter, or a job-claim index.
2. A process crash discards `CurrentStage`; recovery intentionally uses durable `EnrollmentStatus`, `LastAttemptUtc`, retry count, row version, and stage timestamps.
3. Structured logs and telemetry may persist the observed stage outside the entity database. Such records are historical diagnostics, not resumable checkpoints.
4. The recovery sweep continues to identify stuck items from durable in-flight statuses and staleness. It must not depend on the last emitted stage log, which can be delayed, sampled, or lost.
5. Dashboard business counts continue to be transactionally consistent with status. Per-stage latency/failure charts may consume telemetry separately and tolerate its non-authoritative delivery semantics.
6. Audit `FromStatus`/`ToStatus` values remain durable Domain facts. A stage field is supplemental diagnostic context and follows the routing/retention distinction established in the audit enhancement design.

---

## 8. Recommendation summary

| Question | Decision |
|---|---|
| Is `EnrollmentStatus` sufficient for persisted business state? | **Yes.** Keep it unchanged and authoritative. |
| Is it sufficient for exact live execution diagnostics? | **No.** Storage, result writing, optional stages, and per-attempt timing require finer identity. |
| Is a separate enum needed? | **Yes, as one consolidated Application-layer diagnostic contract.** |
| Is it a Domain enum or second state machine? | **No.** It is neither persisted nor behavior-driving. |
| What happens to 0.2's `EnrollmentStage`? | Replace it with nullable canonical `EnrollmentPipelineStage` on `CurrentStage`. |
| What happens to 0.3's enum? | Its name/core members become the canonical base and gain claim + optional-stage identities. |
| What happens to 0.5's phase/order design? | Keep `EnrollmentStagePhase` and configured `Order`; replace free-form `StageName` identity with the canonical enum. |
| Do status-like stage values belong in the new enum? | **No.** `Pending`, `Retrying`, `Failed`, `Completed`, and `Cancelled` remain lifecycle/disposition concepts. |

---

## Constraints Confirmed

- No `.cs`, `.csproj`, `.tsx`, `.ts`, migration, configuration, or other non-markdown file was created, edited, or deleted.
- The implemented `EnrollmentStatus`, `BatchStatus`, `StudentEnrollmentItem`, and all existing Phase 2 documents remain unchanged.
- Every C# snippet in this document is illustrative/proposed only; no production enum or contract was implemented.
- No database column, status transition, worker, retry rule, telemetry emitter, audit sink, dashboard, or pipeline-stage implementation was changed.
- The only output artifact created for AI20.PHASE2.0.13 is `docs/AI20_PHASE2_PIPELINE_STAGE_ENUM.md`.
