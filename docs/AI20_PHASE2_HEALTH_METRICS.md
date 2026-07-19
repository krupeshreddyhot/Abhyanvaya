# AI20.PHASE2.0.7 — Enrollment Statistics & Health Contracts

**Type:** Contract/interface design only. No production code was written or modified to produce this document. No endpoint, worker, persistence change, telemetry collector, or UI implementation is included.

**Status of the code blocks below:** every C# block is a **proposed** contract that does not yet exist in the codebase.

---

## 0. Design Decision: Summary and Health Stay Separate

`EnrollmentDashboardSummaryDto` remains the business-facing projection for the five existing SuperAdmin dashboard tiles: `TotalStudents`, `Embedded`, `Pending`, `Failed`, and `ProcessedToday`.

`EnrollmentHealthDto` is a distinct operational projection. It answers different questions: whether work is flowing, where latency is accumulating, whether workers are active, and which pipeline stages are failing. Keeping it separate is preferable to extending the summary DTO because:

1. The summary is stable business data; health is operational/SRE data with runtime gauges and diagnostic rates.
2. Summary tiles can poll slowly, while queue depth and active-worker gauges may require a shorter polling cadence.
3. Health data may require tighter authorization and may later be consumed by operations tooling rather than only the enrollment dashboard.
4. The health response combines persisted aggregates with process-local runtime state, while the existing summary is entirely database-derived.
5. Existing Phase 1 UI consumers can adopt the summary contract without receiving unrelated operational fields.

The health contract is therefore an additional API projection, not a replacement for `EnrollmentDashboardSummaryDto` or `EnrollmentSpeedMetricsDto`.

---

## 1. Proposed Health Contract

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Operational health snapshot for the enrollment pipeline.
/// Nullable timing/throughput values mean that no valid sample is currently available; they must
/// not be serialized as zero because zero is a real measurement.
/// </summary>
public sealed record EnrollmentHealthDto
{
    public double? AverageDownloadTimeMs { get; init; }
    public double? AverageValidationTimeMs { get; init; }
    public double? AverageEmbeddingTimeMs { get; init; }
    public double? AverageStorageTimeMs { get; init; }
    public double? ItemsPerMinute { get; init; }
    public required int QueueLength { get; init; }
    public required int ActiveWorkers { get; init; }
    public required int StorageFailures { get; init; }
    public required int EmbeddingFailures { get; init; }
    public required int ValidationFailures { get; init; }
    public required decimal RetryRatePercentage { get; init; }
    public required decimal SuccessRatePercentage { get; init; }
}
```

### Type and unit conventions

- Durations use nullable `double` milliseconds, matching the proposed `EnrollmentSpeedMetricsDto` convention. Milliseconds are directly chartable and avoid JSON `TimeSpan` formatting ambiguity.
- `ItemsPerMinute` is a nullable `double`. It is `null` when fewer than two valid completion observations exist or the observed interval is zero; it is never fabricated as `0`.
- Queue, worker, and failure values are non-negative `int` counts.
- Rates use `decimal` percentages in the inclusive `0–100` range, rounded to one decimal place. This deliberately matches `EnrollmentStatistics.CompletionPercentage` and `EnrollmentStatistics.SuccessRatePercentage`.
- All count and rate fields return `0` for an empty analytical scope. Timing and throughput fields return `null` when there is no valid sample.

---

## 2. Scope Model

The persisted metrics use one explicitly selected **analytical scope**:

1. **Batch scope:** one `BatchId`, tenant-checked.
2. **Tenant/college scope:** all matching batches for one tenant/college.
3. **Cross-tenant scope:** all tenants, permitted only when the authenticated caller is a SuperAdmin operating through the existing `IsSuperAdminCrossTenant` query-filter behavior.

Scope authorization must be derived from the authenticated caller and tenant context. A caller-supplied `IsSuperAdminCrossTenant` flag is intentionally not proposed because it would turn an authorization decision into request data.

`QueueLength` and `ActiveWorkers` are exceptions: they describe the shared worker system and are therefore global across tenants. `QueueLength` is deployment-database-global; `ActiveWorkers` is current-process-global. A response must document that mixed scope rather than imply that these two gauges were filtered to the selected batch or tenant.

For a future multi-instance deployment, process-local worker gauges must either be labeled per instance or aggregated by a metrics backend. Summing them cannot be inferred from one API process.

---

## 3. Complete Field Definitions

| Field | Type / unit | Scope | Conceptual source | Notes |
|---|---|---|---|---|
| `AverageDownloadTimeMs` | `double?`, milliseconds | Selected batch, tenant/college, or authorized cross-tenant analytical scope | Mean of valid `DownloadedUtc - DownloadStartedUtc` deltas over the most recent completed sample | Exclude missing, negative, or otherwise invalid deltas. `null` means no usable sample. |
| `AverageValidationTimeMs` | `double?`, milliseconds | Selected analytical scope | Mean of valid `ValidatedUtc - ValidationStartedUtc` deltas over the recent sample | Includes the complete validation gate represented by those persisted timestamps. |
| `AverageEmbeddingTimeMs` | `double?`, milliseconds | Selected analytical scope | Mean of valid `CompletedUtc - EmbeddingStartedUtc` deltas over the recent completed sample | Follows the existing speed-metrics design. It includes final result persistence after embedding because the current timestamp pair cannot separate those operations. |
| `AverageStorageTimeMs` | `double?`, milliseconds | Selected analytical scope | Mean of storage-stage duration observations emitted around `IEnrollmentStorageService.PersistEnrollmentPhotoAsync` | The current item model has no storage-start/storage-complete timestamps. Do not approximate this with `EmbeddingStartedUtc - ValidatedUtc`, which also includes orchestration and transition overhead. Until dedicated stage timing telemetry or timestamps exist, return `null`. |
| `ItemsPerMinute` | `double?`, completed items/minute | Selected analytical scope | For the recent completed sample: completed observation count divided by elapsed minutes from the earliest to latest `CompletedUtc` | Requires at least two valid completion timestamps and a positive interval. It measures observed completion throughput, not queue intake. |
| `QueueLength` | `int`, items | Global across all tenants in the deployment database | Count of `StudentEnrollmentItem` rows with `Status` in (`Pending`, `RetryRequired`) whose batch is eligible to claim | This is the durable work backlog, not the bounded in-memory wake-signal channel length. Exclude work blocked by a batch cancellation request because the worker cannot claim it. |
| `ActiveWorkers` | `int`, currently processing item slots | Global across all tenants in the current process | Runtime gauge incremented after a worker successfully claims an item and decremented in `finally` after that item leaves processing | Not a database query and not the configured maximum concurrency. It is a process-local gauge; `0` can be healthy when `QueueLength == 0`. |
| `StorageFailures` | `int`, distinct affected items | Selected analytical scope | Count of distinct items with a storage-stage failure outcome in the reporting window/snapshot | `FailureCategory.StorageUploadFailed` is the current category-level signal. Exact historical counts, including failures later recovered by retry, require stage-aware failure telemetry/history; current item state alone can undercount recovered incidents. |
| `EmbeddingFailures` | `int`, distinct affected items | Selected analytical scope | Count of distinct items with an embedding-stage failure outcome in the reporting window/snapshot | Do not blindly count every `EmbeddingEngineFailed`: the baseline contract also uses that category for engine errors during validation. Exact stage attribution requires the emitting stage in telemetry/history. |
| `ValidationFailures` | `int`, distinct affected items | Selected analytical scope | Count of distinct items rejected or errored in the validation stage | Includes validation quality categories (`InvalidImage`, `CorruptImage`, `NoFaceDetected`, `MultipleFacesDetected`, `BlurRejected`, `LowResolutionRejected`) and validation-stage engine errors when stage-aware telemetry is available. |
| `RetryRatePercentage` | `decimal`, percentage `0–100` | Selected analytical scope | `100 × count(items where RetryCount > 0) / TotalItems` | Measures the share of items that consumed at least one **automatic** retry. Each item contributes once regardless of retry count, preventing one pathological item from dominating the rate. Round to one decimal place. |
| `SuccessRatePercentage` | `decimal`, percentage `0–100` | Selected analytical scope | `100 × Completed / (Completed + Failed + Cancelled)` | Exactly follows `EnrollmentStatistics.SuccessRatePercentage`: success among terminal items. Return `0m` when the terminal count is zero; round to one decimal place. |

### Source distinction

The DTO intentionally combines three source classes:

- **Persisted item/batch state:** queue length, failure classifications, retry counts, terminal counts, and timestamp deltas.
- **Stage timing telemetry:** storage latency and exact stage-attributed failure incidents where current item state is insufficient.
- **Runtime process gauge:** active workers.

That distinction is contract-significant. A read-only service may read all three without mutating them, but an implementation cannot obtain `ActiveWorkers` from an EF Core repository, and it cannot derive exact storage latency from the timestamps currently present on `StudentEnrollmentItem`.

---

## 4. Retry Rate and Success Rate Are Not Complements

### Retry rate

```text
RetriedItems = count of in-scope items where RetryCount > 0
RetryRatePercentage = round(RetriedItems × 100 / TotalItems, 1)
```

`RetryCount` is already defined as automatic-retry attempts consumed. Manual SuperAdmin requeues do not contribute unless a later automatic retry increments `RetryCount`. The item-based definition was selected over `AutoRetryAttempts / TotalAttempts` because it communicates the proportion of the workload affected by instability and is not disproportionately inflated by one item repeatedly hitting the retry cap.

### Success rate

```text
TerminalCount = Completed + Failed + Cancelled
SuccessRatePercentage = round(Completed × 100 / TerminalCount, 1)
```

This is the existing domain definition and deliberately excludes pending/in-flight items from the denominator.

The rates are not complements:

- An item can retry and later complete, contributing to both `RetriedItems` and `Completed`.
- Pending items contribute to the retry-rate denominator but not the success-rate denominator.
- Cancelled terminal items reduce success rate without increasing retry rate.
- Therefore `RetryRatePercentage + SuccessRatePercentage` has no requirement to equal `100`.

Example: of 100 total items, 80 are completed, 10 failed, 10 remain pending, and 20 of the completed items retried. Retry rate is `20.0%`; success rate is `88.9%` (`80 / 90`). They are independent operational signals.

---

## 5. Interface Placement Recommendation

**Recommendation: introduce a sibling `IEnrollmentHealthService`; do not add this method to `IEnrollmentStatisticsService`.**

```csharp
namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Read-only operational health surface for the enrollment pipeline. Composes persisted
/// enrollment aggregates, stage telemetry, and process-local runtime gauges; never mutates them.
/// </summary>
public interface IEnrollmentHealthService
{
    Task<EnrollmentHealthDto> GetHealthAsync(
        EnrollmentHealthScope scope,
        CancellationToken cancellationToken = default);
}

public sealed record EnrollmentHealthScope
{
    public Guid? BatchId { get; init; }
    public int? TenantId { get; init; }
    public int? CollegeId { get; init; }
    public int? AcademicYear { get; init; }
    public int SampleSize { get; init; } = 50;
}
```

Scope rules:

- `BatchId` selects one tenant-checked batch and takes precedence over broader filters.
- Without `BatchId`, `TenantId` selects one tenant and optional college/year filters narrow that tenant.
- An all-null business scope is accepted only for an authenticated cross-tenant SuperAdmin; the service must not infer elevated access from the DTO.
- `SampleSize` applies to recent timing/throughput observations, not to counts or rates.

Reading an in-memory gauge is still read-only, transaction-free, and side-effect-free, so adding health to `IEnrollmentStatisticsService` would not technically violate its “no mutation” contract. It would, however, weaken interface cohesion and force its currently repository-only implementation to depend on worker runtime state and possibly a telemetry source. A sibling interface preserves the existing service's clear responsibility for database read models while allowing health to compose:

1. `IEnrollmentStatisticsService` or repositories for persisted aggregates;
2. a future read-only stage telemetry source for storage timing and exact stage failures; and
3. a singleton runtime gauge published by `EnrollmentBackgroundService`.

`IEnrollmentHealthService` remains read-only and transaction-free. It does not move progress mutation out of `IEnrollmentProgressReporter`; that reporter remains the sole writer of item transitions and denormalized counters.

---

## 6. Relationship to Progress Reporting and Concurrency

`QueueLength` is related to progress but is not another denormalized progress counter. It is the global claimable backlog across batches, derived from durable `Pending`/`RetryRequired` item state. Batch progress continues to use `EnrollmentProgress` and the O(1) batch counters exposed by `IEnrollmentProgressReporter`.

`ActiveWorkers` measures item claims currently executing, not:

- the number of hosted-service instances;
- the configured download concurrency;
- the configured validation/embedding concurrency; or
- the number of items in a particular status.

The worker design has independent I/O and CPU concurrency gates. A future richer DTO could expose configured/occupied slots per gate, but this contract intentionally defines one unambiguous gauge: concurrently claimed items currently being processed by this process.

---

## 7. Phase 1 UI Mapping

| Contract field | Future UI element | Display/health behavior |
|---|---|---|
| `AverageDownloadTimeMs` | Advanced “Pipeline Health” latency row/chart | Display as milliseconds or humanized seconds; show `--` when `null`. |
| `AverageValidationTimeMs` | Pipeline-stage latency row/chart | Helps identify image-quality/face-detection bottlenecks. |
| `AverageEmbeddingTimeMs` | Pipeline-stage latency row/chart | Helps identify ONNX CPU pressure. |
| `AverageStorageTimeMs` | Media Storage detail/latency row | Show `--` until dedicated storage timing observations are available. |
| `ItemsPerMinute` | Throughput metric in the future health panel | Display completion throughput, not “students queued per minute.” |
| `QueueLength` | Background Worker tile detail and health panel backlog card | Non-zero is informational while workers are active; sustained growth may become warning criteria. |
| `ActiveWorkers` | Existing `AiSystemStatusCard` “Background Worker” status | `ActiveWorkers > 0` can show `ready` / “Running”. `ActiveWorkers == 0 && QueueLength > 0` can show `starting` / warning. `ActiveWorkers == 0 && QueueLength == 0` is idle/healthy, not offline. |
| `StorageFailures` | Media Storage tile detail and future failures panel | Drives warning/error context for the existing “Media Storage” subsystem tile. |
| `EmbeddingFailures` | Embedding Engine tile detail and future failures panel | Drives warning/error context for the existing “Embedding Engine” subsystem tile. |
| `ValidationFailures` | Future validation-stage failure card/filter | Validation is currently part of the enrollment flow rather than a dedicated Phase 1 status tile. |
| `RetryRatePercentage` | Reliability metric/trend in the future health panel | Elevated retry rate indicates transient instability even when items eventually succeed. |
| `SuccessRatePercentage` | Outcome metric/trend in the future health panel | Uses terminal outcomes and remains independent of retry rate. |

The five `StatCard`s already rendered by `StudentEnrollmentPage` map to the existing `EnrollmentDashboardSummaryDto`:

| Existing Phase 1 StatCard | Existing summary field |
|---|---|
| Total Students | `TotalStudents` |
| Embedded | `Embedded` |
| Pending | `Pending` |
| Failed | `Failed` |
| Processed Today | `ProcessedToday` |

Those cards must continue using the summary DTO. `EnrollmentHealthDto` feeds a separate advanced/future health panel and selected status details in `AiSystemStatusCard`; it does not replace the five business summary tiles.

---

## 8. Failure Attribution and Sampling Rules

1. Failure counts are counts of **distinct affected items**, not raw exception occurrences.
2. The reporting window must be explicit at the endpoint/service implementation boundary. If no historical failure-event store exists, the initial projection may only describe the current item snapshot and must label itself accordingly.
3. A recovered retry may clear or replace the item's last failure category. Exact historical stage-failure counts therefore require stage-aware telemetry/history; current `StudentEnrollmentItem` state is not sufficient by itself.
4. Timing averages use the most recent `SampleSize` valid observations in the selected analytical scope.
5. Invalid timestamp pairs are excluded and should be logged/observed as data-quality issues; they must not become negative durations or zeroes.
6. Runtime gauge reads are snapshots and need not be transactionally consistent with database aggregates. The DTO is operational telemetry, not an accounting ledger.

---

## Constraints Confirmed

- No `.cs`, `.csproj`, `.tsx`, `.ts`, SQL, migration, endpoint, background worker, DI registration, or other production file was written or modified.
- Every C# block above is an illustrative **proposed contract** only.
- No implementation or persistence change was made.
- The existing `EnrollmentDashboardSummaryDto`, `EnrollmentSpeedMetricsDto`, `EnrollmentStatistics`, `EnrollmentProgress`, `AiSystemStatusCard`, and `StudentEnrollmentPage` were not modified.
- The health DTO is additional and does not replace the existing five-card dashboard summary.
- Deliverable produced: `docs/AI20_PHASE2_HEALTH_METRICS.md`.
