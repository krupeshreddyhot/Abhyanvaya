# AI20.PHASE2.0.3 — Pipeline Telemetry Contracts

**Type:** Contract/DTO shape design only. No production code was written or modified to produce this document. No telemetry is implemented, no `ILogger`/metrics/APM call is wired, and no `.cs`/`.csproj`/`.tsx`/`.ts` file is created or changed anywhere in the repository. Every C# block below is a **proposed** enhancement to a Phase‑2 contract that does not yet exist in the codebase.

**Baseline being enhanced:** `EnrollmentStageResult` as defined in `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §2. This milestone enriches that result envelope (and the per‑stage result DTOs of §8–§11 of that document) with per‑stage telemetry metadata so a future dashboard / on‑call engineer can answer "how long did each stage take, on which instance, and with what technical failure code" — **without** changing the business branching vocabulary (`EnrollmentStageOutcome` / `FailureCategory`) that Phase 2 already froze.

**Alignment:** timing/correlation vocabulary follows the conventions already established in `docs/AI20_ENROLLMENT_BACKGROUND.md` §3.7 (per‑transition structured fields keyed by `ExecutionTraceId`, "Upload Started/Completed/Failed" style, batch‑level `DurationMs` summary) and the `ExecutionTraceId` correlation model documented in `docs/AI17_RUNTIME8_FORENSIC_ANALYSIS.md` (per‑stage checkpoints with start/finish/duration under one trace id). This doc designs the **shape** those conventions would serialize into; it does not emit any log line.

---

## 0. Design principles (carried from Phase 2)

The telemetry additions obey the same rules the §0 of `AI20_PHASE2_ENGINE_CONTRACTS.md` set out:

1. **Interfaces + DTOs live in `Abhyanvaya.Application`; Domain stays untouched.** `FailureCategory`/`EnrollmentStatus` in `Abhyanvaya.Domain.Enums` are reused **unchanged** — telemetry adds *no* Domain member. The new telemetry types are `Abhyanvaya.Application.Enrollment` DTOs, exactly like `EnrollmentStageResult`.
2. **Telemetry is additive and non‑authoritative.** No telemetry field ever drives retry, branching, or persistence decisions. `Outcome` + `FailureCategory` remain the *only* fields the orchestrator branches on (design principle #2 of the baseline). Telemetry is observed, never obeyed.
3. **DTOs are `sealed record` with `init` members.** The telemetry sub‑record mirrors the `StudentPhotoFetchResult` result‑envelope precedent: nullable/optional where a value may legitimately be absent, non‑throwing.
4. **No sensitive data.** Telemetry carries stage names, durations, timestamps, machine names, and short technical codes only — never image bytes, vectors, URLs with secrets, or PII, matching the `Detail`/`Reason` constraint on `EnrollmentAuditEvent` and `EnrollmentStageResult`.
5. **Backward compatible.** Every new member is optional (nullable or defaulted). Existing `EnrollmentStageResult.Ok()/Fail()/Retry()` factory call‑sites in the baseline continue to compile with telemetry defaulted to `null`; telemetry is layered on via `with`‑expression enrichment, so no existing proposed call‑site breaks.

---

## 1. Baseline recap — what `EnrollmentStageResult` carries today

From `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §2, the current envelope has exactly three members:

| Field | Type | Role |
|---|---|---|
| `Outcome` | `EnrollmentStageOutcome` | Business branching: `Succeeded` / `Failed` / `RetryRequired` / `Cancelled`. |
| `FailureCategory` | `FailureCategory?` | The **business** classification (Domain enum) that drives retry policy + the UI's reason filter. |
| `Reason` | `string?` | Human‑readable, non‑sensitive note for logs/audit. |

It answers *"what happened and what should the pipeline do next"*. It does **not** answer any *operational* question — how long, when, where, or with what fine‑grained technical code. Those are the questions this milestone adds, and they are deliberately a **separate concern** from the three business fields above (see §5 for why `DiagnosticCode ≠ FailureCategory`).

---

## 2. Recommendation up front — split telemetry into a reusable sub‑record

**Decision: introduce a dedicated `EnrollmentStageTelemetry` sub‑record and embed it (as an optional `Telemetry` property) on both `EnrollmentStageResult` and each per‑stage result DTO — rather than flattening the six new fields directly onto `EnrollmentStageResult`.**

Rationale (the single most important design choice in this document, stated first per the baseline's convention):

- **The six telemetry fields are one cohesive concept** ("how this stage executed"), so they belong together as one value object, not scattered as six loose members. This is the same instinct that put `EnrollmentStatistics`/`EnrollmentProgress` in `Domain/ValueObjects` rather than spreading counters across the batch entity.
- **The fields must attach at more than one place.** Only `IEnrollmentOrchestrator` returns `EnrollmentStageResult`; the actual stage services (`IEnrollmentValidationService`, `IEnrollmentStorageService`, `IEnrollmentEmbeddingService`, …) return their own result DTOs (`EnrollmentValidationResult`, `EnrollmentStorageResult`, `EnrollmentEmbeddingResult`). Timing is *measured inside each stage* (each stage owns its own clock), so each stage DTO needs to carry telemetry too. A single reusable `EnrollmentStageTelemetry` embedded everywhere avoids re‑declaring the same six fields on five different records (see §7 for the full flow analysis).
- **Separation of concerns / SRP.** Business disposition (`Outcome`, `FailureCategory`, `Reason`) and observability metadata (`Stage`, timings, `DiagnosticCode`, `MachineName`) have different owners, different lifetimes, and different consumers (retry engine vs. dashboard). Keeping them in distinct records means a change to the telemetry shape (e.g. adding `MachineName`, later adding a `ThreadCount`) never touches the branching contract.
- **Clean App Insights / OpenTelemetry mapping (see §9).** A self‑contained telemetry value object maps 1:1 onto a single `DependencyTelemetry`/`Activity` span at the adapter boundary, whereas flattened fields would force the adapter to cherry‑pick members out of a mixed business+telemetry record.

`EnrollmentStageResult` therefore remains the **umbrella** the orchestrator returns, but it delegates the metadata to the embedded `Telemetry` sub‑record instead of absorbing the fields itself.

---

## 3. Proposed contracts (C# — PROPOSED, not committed)

### 3.1 The stage enum for the `Stage` field

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Which pipeline stage a telemetry record describes. Distinct from
/// <see cref="EnrollmentStageTimestamp"/> (which names a DB column to stamp) — this names the
/// *unit of work* being measured, so a future dashboard can group durations "by stage" and an
/// App Insights adapter can use it as the Dependency name / OpenTelemetry span name.
/// </summary>
public enum EnrollmentPipelineStage
{
    Download = 0,
    Validation = 1,
    Storage = 2,
    Embedding = 3,
    ResultWrite = 4,
    /// <summary>The whole item end-to-end (the orchestrator's umbrella result), not a single stage.</summary>
    Pipeline = 5,
}
```

### 3.2 The reusable telemetry sub‑record

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Additive, non-authoritative observability metadata for one executed stage (or the whole item).
/// Carries NO business meaning — the orchestrator never branches on any field here. Contains no
/// image bytes, vectors, PII, or secret-bearing URLs (design principle #4). Every member is optional
/// so a stage that could not measure a value (or a caller on the legacy path) leaves it null.
/// </summary>
public sealed record EnrollmentStageTelemetry
{
    /// <summary>Which stage this record measures. Answers "group durations by stage" on the dashboard.</summary>
    public required EnrollmentPipelineStage Stage { get; init; }

    /// <summary>
    /// Precise wall-clock duration of the stage in whole milliseconds, captured from a monotonic
    /// Stopwatch by the stage itself (NOT computed from StartedUtc/CompletedUtc — see §4). Null only
    /// if the stage was never entered (e.g. a stage short-circuited before starting its own timer).
    /// </summary>
    public long? ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Stable, finer-grained TECHNICAL code for support engineers (e.g. "DL_HTTP_TIMEOUT_30S",
    /// "EMB_ONNX_SESSION_DISPOSED"). Deliberately a separate concept from the business
    /// <see cref="Domain.Enums.FailureCategory"/> (see §5). Null on success or when no specific
    /// technical code applies. Open set by convention (§5.2), NOT a Domain enum.
    /// </summary>
    public string? DiagnosticCode { get; init; }

    /// <summary>Wall-clock UTC instant the stage began. For human correlation with logs / the History tab.</summary>
    public DateTimeOffset? StartedUtc { get; init; }

    /// <summary>Wall-clock UTC instant the stage finished (success or failure). For correlation, not precision timing.</summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// (FUTURE) Instance/host that executed the stage, for multi-instance attribution once the
    /// deployment scales horizontally. Null today on the single Render instance (§6). Populated later
    /// from Environment.MachineName at capture time — the field exists NOW so adopting it needs no
    /// contract break.
    /// </summary>
    public string? MachineName { get; init; }

    /// <summary>Convenience constructor for a completed measurement; caller supplies the Stopwatch elapsed.</summary>
    public static EnrollmentStageTelemetry For(
        EnrollmentPipelineStage stage,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        long elapsedMilliseconds,
        string? diagnosticCode = null,
        string? machineName = null) =>
        new()
        {
            Stage = stage,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            ElapsedMilliseconds = elapsedMilliseconds,
            DiagnosticCode = diagnosticCode,
            MachineName = machineName,
        };
}
```

### 3.3 The enhanced umbrella `EnrollmentStageResult`

Only one member is added (`Telemetry`); the three baseline members and all three factory methods are preserved, so every existing proposed call‑site still compiles. A `WithTelemetry` enricher is added so the orchestrator can attach measurement after the branching decision is made.

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Result envelope every stage returns; carries the business outcome, optional failure
/// classification, a reason for audit, and (NEW) optional non-authoritative telemetry metadata.
/// </summary>
public sealed record EnrollmentStageResult
{
    public required EnrollmentStageOutcome Outcome { get; init; }

    /// <summary>Set only when Outcome is Failed or RetryRequired. The BUSINESS classification (Domain enum).</summary>
    public FailureCategory? FailureCategory { get; init; }

    /// <summary>Human-readable, non-sensitive reason for logs/audit (never image data).</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// (NEW) Optional observability metadata for the stage/item this result describes. Null on the
    /// legacy call-path; never affects branching. For an orchestrator-level umbrella result this is
    /// stamped with Stage = Pipeline (the whole-item timing); per-stage services stamp their own stage.
    /// </summary>
    public EnrollmentStageTelemetry? Telemetry { get; init; }

    // --- Preserved baseline factories (telemetry defaults to null) ---
    public static EnrollmentStageResult Ok() => new() { Outcome = EnrollmentStageOutcome.Succeeded };
    public static EnrollmentStageResult Fail(FailureCategory category, string reason) =>
        new() { Outcome = EnrollmentStageOutcome.Failed, FailureCategory = category, Reason = reason };
    public static EnrollmentStageResult Retry(FailureCategory? category, string reason) =>
        new() { Outcome = EnrollmentStageOutcome.RetryRequired, FailureCategory = category, Reason = reason };

    /// <summary>(NEW) Non-mutating enrichment: attach telemetry to an already-decided result.</summary>
    public EnrollmentStageResult WithTelemetry(EnrollmentStageTelemetry telemetry) =>
        this with { Telemetry = telemetry };
}
```

---

## 4. Per‑field rationale table

For each field: the dashboard/on‑call question it answers, and precisely how it is computed (stored vs. derived).

| Field | Question it answers | How computed | Stored or computed property? |
|---|---|---|---|
| `Stage` | "Which stage is this? / group all durations by stage." | Set literally by the stage that produces the record (validation stamps `Validation`, the orchestrator umbrella stamps `Pipeline`). | **Stored** (`required init`). Constant per record; there is nothing to derive it from. |
| `ElapsedMilliseconds` | "How long did this stage take? Where is the bottleneck? Is a stage regressing over time?" | Captured from a **monotonic `Stopwatch`** started when the stage begins and read when it ends (`Stopwatch.GetElapsedTime(...)`), rounded to whole ms. | **Stored value — deliberately NOT a computed `CompletedUtc - StartedUtc` property.** See §4.1. |
| `DiagnosticCode` | "What *technically* went wrong, at a finer grain than the business category? Filter/group support tickets by exact fault." | Assigned by the stage from a stable convention (§5.2) at the point the specific fault is known (e.g. an `HttpRequestException`/timeout → `DL_HTTP_TIMEOUT_30S`). | **Stored** (`string?`). Not derivable from other fields. |
| `StartedUtc` | "When did this begin? Correlate against Render logs / OOM events / the History timeline." | `DateTimeOffset.UtcNow` (or an injected `TimeProvider`) captured when the stage starts. | **Stored.** Wall‑clock instant; independent of elapsed. |
| `CompletedUtc` | "When did this finish? Bracket the stage against external events." | `DateTimeOffset.UtcNow` captured when the stage returns. | **Stored.** |
| `MachineName` | "(future) Which instance ran this, once we scale out? Is one instance failing more?" | `Environment.MachineName` at capture time — **left `null` today** (§6). | **Stored, nullable.** |

### 4.1 Why `ElapsedMilliseconds` is a stored value, not `CompletedUtc - StartedUtc`

This is an explicit decision requested by the task. `ElapsedMilliseconds` is a **stored `long?`**, populated from a `Stopwatch`, **not** a computed property that subtracts the two timestamps. Justification:

1. **Precision & correctness of source clock.** `DateTimeOffset.UtcNow` is wall‑clock: it is subject to NTP corrections, VM clock skew, and coarse resolution (~1–16 ms on Windows). A monotonic `Stopwatch` is the correct instrument for measuring a *duration* and is immune to the clock jumping backward/forward mid‑stage (which on wall‑clock subtraction can even yield a **negative** duration). `StartedUtc`/`CompletedUtc` exist for *human correlation*; `ElapsedMilliseconds` exists for *measurement*. They are captured from different clocks on purpose.
2. **Null‑fragility of a derived property.** Both `StartedUtc` and `CompletedUtc` are nullable, so a computed `CompletedUtc - StartedUtc` would be a `TimeSpan?` that silently collapses to `null` if either endpoint is missing — hiding a real duration that the Stopwatch *did* capture. Storing the number keeps the measurement even when a wall‑clock stamp is absent.
3. **Serialization & sink stability.** A stored scalar serializes deterministically to the dashboard/App Insights as a single number; a computed property would re‑derive on every access and would not round‑trip through JSON as a first‑class field.
4. **Accepted, documented redundancy.** Yes, `ElapsedMilliseconds` and `(CompletedUtc - StartedUtc)` will *usually* be close. That small redundancy is intentional and cheap: the two answer different questions (precise duration vs. wall‑clock window), and a *divergence* between them is itself a useful signal (e.g. a large gap hints the process was paused/GC‑stalled between the Stopwatch stop and the timestamp read).

---

## 5. `DiagnosticCode` — a separate concept from `FailureCategory`

### 5.1 Why it is not redundant with `FailureCategory`

| | `FailureCategory` (Domain enum) | `DiagnosticCode` (Application telemetry `string?`) |
|---|---|---|
| **Layer** | `Abhyanvaya.Domain.Enums` — the business kernel. | `Abhyanvaya.Application.Enrollment` — an observability field. |
| **Audience** | The retry engine + the SuperAdmin "Failures" filter. | Support/on‑call engineers reading dashboards & logs. |
| **Purpose** | *Business* classification that **drives behavior**: `Classify()` maps it to retry‑vs‑terminal; the UI groups the failure screen by it. | *Technical* attribution that **drives nothing** — pure diagnostics. |
| **Cardinality / stability** | Small, closed, **stable** set (11 values). Adding a value is a deliberate Domain change that ripples into the retry table and UI. | Large, **open**, fast‑moving set. New codes appear as new fault modes are discovered, with zero Domain/contract impact. |
| **Granularity** | Coarse: `EmbeddingEngineFailed` covers *every* ONNX fault. | Fine: `EMB_ONNX_SESSION_DISPOSED` vs `EMB_DIM_MISMATCH_512` vs `EMB_ONNX_OOM` all roll up to the single business category `EmbeddingEngineFailed`. |

The relationship is **many‑to‑one**: many `DiagnosticCode`s map up to one `FailureCategory`. Collapsing them into one enum would (a) explode a closed Domain enum into dozens of infra‑specific members, (b) break the retry decision table every time a new technical fault is observed, and (c) leak Infrastructure detail (HTTP status codes, ONNX session state) into the Domain layer — violating Clean Architecture. Keeping `DiagnosticCode` as a free‑form Application telemetry string lets support answer "*which exact* embedding fault spiked today?" without touching business rules.

### 5.2 Proposed coding scheme

A stable, greppable, `SCREAMING_SNAKE_CASE` string, prefixed by a short **stage tag**, so codes sort/group by stage and never collide across stages:

```
<STAGE>_<SUBSYSTEM>_<CONDITION>[_<QUALIFIER>]
```

| Example code | Rolls up to `FailureCategory` | Meaning for support |
|---|---|---|
| `DL_HTTP_404` | `PhotoNotFound` | Source returned 404 for the constructed URL. |
| `DL_HTTP_403` | `AccessDenied` | Source returned 403 — credentials/config. |
| `DL_HTTP_TIMEOUT_30S` | *(null → transient)* | HTTP client hit the 30 s timeout; retry‑eligible. |
| `DL_HTTP_5XX` | *(null → transient)* | Upstream 5xx. |
| `VAL_NO_FACE` | `NoFaceDetected` | Detector returned zero faces. |
| `VAL_MULTI_FACE_3` | `MultipleFacesDetected` | 3 faces found; enrollment needs exactly one. |
| `VAL_BLUR_LAPLACIAN` | `BlurRejected` | Variance‑of‑Laplacian below threshold. |
| `VAL_LOWRES_SRC_640x480` | `LowResolutionRejected` | Source under the resolution floor. |
| `STO_R2_5XX` | `StorageUploadFailed` | Object store returned 5xx on PUT. |
| `EMB_ONNX_SESSION_DISPOSED` | `EmbeddingEngineFailed` | ONNX session was disposed under the call. |
| `EMB_DIM_MISMATCH_512` | `EmbeddingEngineFailed` | Vector length ≠ expected 512. |
| `WR_DB_CONCURRENCY` | *(surfaced, no category)* | Optimistic‑concurrency conflict on the item row. |
| `SYS_TIMEOUT_STUCK` | `Timeout` | Recovery sweep force‑reset a stuck item. |

**Why an open `string?` and not an enum:** the set is expected to grow as production reveals new fault modes; an enum would force a contract/Domain change (and a redeploy) for every newly‑observed code. A documented string convention gives support engineers immediate finer granularity with zero contract churn. If a subset ever stabilizes, a companion static class of `const string` constants (e.g. `EnrollmentDiagnosticCodes.EmbOnnxSessionDisposed`) can be introduced **later** as a convenience without changing the field's type — an additive, non‑breaking step, explicitly out of scope here.

---

## 6. `MachineName (future)` — why deferred, and how it's optional today

- **Why marked future.** The platform runs on a **single** Render instance today (per `AI20_ENROLLMENT_BACKGROUND.md` §3.5 and the `AI17_RUNTIME8` Render Starter context). On one instance, "which machine ran this stage" has a trivial constant answer, so populating it now would add noise with zero diagnostic value.
- **Why include the field now anyway.** The moment the deployment scales horizontally (multiple API/worker instances all polling the same DB‑row‑backed queue — the exact model `AI20_ENROLLMENT_BACKGROUND.md` §3.1 describes, where "multiple parallel workers/instances never double‑process the same job"), per‑instance attribution becomes essential ("is instance‑B failing embeddings 5× more than instance‑A?"). Adding the field **later** would be a contract break to a DTO that flows through five interfaces. Shipping the (empty) field now means multi‑instance telemetry is a pure implementation change — populate `Environment.MachineName` at capture — with **no** contract revision. This is the same forward‑compatibility instinct behind pre‑shaping the queue as DB‑row‑backed before horizontal scaling exists.
- **How it's optional today.** `MachineName` is a plain `string?` defaulting to `null`. Today every producer leaves it `null`; dashboards render `null` as "single/unknown instance". No validation requires it, no branch reads it, so a null carries no penalty. When scale‑out arrives it maps directly to App Insights `cloud_RoleInstance` / OpenTelemetry `service.instance.id` (see §9).

---

## 7. How the enhanced result flows through the stage interfaces

**Question posed by the task:** do the per‑stage result DTOs need a `Telemetry` sub‑property of this shape, or should `EnrollmentStageResult` itself be the umbrella? **Recommendation: both — the sub‑record is embedded on each per‑stage DTO *and* on the umbrella, because measurement happens per stage but disposition rolls up at the orchestrator.**

Reviewing the §3–§11 contracts of `AI20_PHASE2_ENGINE_CONTRACTS.md`:

- `IEnrollmentOrchestrator.ProcessItemAsync` is the **only** method that returns `EnrollmentStageResult`. Its result is the *whole‑item* disposition → it carries `Telemetry` with `Stage = Pipeline` and `ElapsedMilliseconds` = end‑to‑end item duration.
- The stage services return their **own** DTOs, not `EnrollmentStageResult`:
  - `IEnrollmentValidationService.ValidateAsync` → `EnrollmentValidationResult`
  - `IEnrollmentStorageService.PersistEnrollmentPhotoAsync` → `EnrollmentStorageResult`
  - `IEnrollmentEmbeddingService.GenerateAsync` → `EnrollmentEmbeddingResult`
  - `IEnrollmentResultWriter.WriteSuccess/Failure/RetryAsync` → `Task` (void)
  - Download reuses `IStudentPhotoProvider.FetchPhotoAsync` → `StudentPhotoFetchResult` (an *already‑frozen* reused contract — **not** modified here).

Because each stage is where its own `Stopwatch`/timestamps/`DiagnosticCode` are actually known, the clean design is to embed the **same** `EnrollmentStageTelemetry?` on each stage DTO the pipeline owns, then let the orchestrator (a) read each stage's telemetry, (b) forward it to the audit/dashboard sink, and (c) compose the umbrella `Pipeline` telemetry on the `EnrollmentStageResult` it returns.

Proposed additive members (each is one nullable property; no other field changes):

```csharp
// PROPOSED enrichment of the §8–§10 result DTOs — one optional Telemetry property each.

public sealed record EnrollmentValidationResult
{
    // ... existing members (IsValid, FailureCategory, Reason, AlignedFaceBytes, QualityScore, ...) unchanged ...
    public EnrollmentStageTelemetry? Telemetry { get; init; }   // Stage = Validation
}

public sealed record EnrollmentStorageResult
{
    // ... existing members (PhotoKey, Checksum, ByteSize) unchanged ...
    public EnrollmentStageTelemetry? Telemetry { get; init; }   // Stage = Storage
}

public sealed record EnrollmentEmbeddingResult
{
    // ... existing members (NormalizedVector, Model, Version, Quality, Dimension) unchanged ...
    public EnrollmentStageTelemetry? Telemetry { get; init; }   // Stage = Embedding
}
```

**Why not put telemetry *only* on the umbrella `EnrollmentStageResult`?** Because the orchestrator would then have to time each stage *from the outside*, which (a) can't see a stage's internal `DiagnosticCode`, and (b) measures "call + marshalling" rather than the stage's own work. Timing at the source is both more accurate and where the technical code is known.

**Why not put telemetry *only* on the per‑stage DTOs (skip the umbrella)?** Because the orchestrator's return value is what the worker (`EnrollmentBackgroundService`) and `FinalizeBatchIfCompleteAsync` consume; it needs a single roll‑up (`Stage = Pipeline`, total elapsed, terminal `DiagnosticCode`) without the caller reconstructing it from N stage DTOs.

**Download stage note:** `StudentPhotoFetchResult` is a **reused, frozen** contract (§1 of the baseline lists it under "reused, not redesigned"). This milestone does **not** modify it. The orchestrator instead wraps the download by timing the `FetchPhotoAsync` call and constructing an `EnrollmentStageTelemetry { Stage = Download }` itself — the one place external timing is acceptable, because we must not alter a reused contract.

`EnrollmentAuditService` already carries `ExecutionTraceId`; telemetry stays *out* of `EnrollmentAuditEvent` (audit answers "who did what", telemetry answers "how fast / on which box") — they are correlated by the shared `ExecutionTraceId`, not merged.

---

## 8. Future Dashboard Usage

Maps each field to the AI20 Student Enrollment UI's future tabs (the "History" and "Failures" tabs referenced in `AI20_ENROLLMENT_UI.md` and the speed metrics of `AI20_ENROLLMENT_BACKGROUND.md` §3.2). No UI code is written here — this is intended consumption only.

| Field | Consuming tab / widget | How it's used |
|---|---|---|
| `Stage` | **History** → per‑stage duration chart; **Failures** → "failed at stage" column. | Group/stack durations by stage to show a per‑item timeline bar (Download → Validation → Storage → Embedding → Write); pinpoint which stage a failure occurred in. |
| `ElapsedMilliseconds` | **History** → speed metrics (`AvgDownloadMs`/`AvgValidationMs`/`AvgEmbeddingMs` in `EnrollmentSpeedMetricsDto`); per‑item duration bars; regression sparklines. | Feeds the existing `EnrollmentSpeedMetricsDto` averages **directly** (today those are computed from DB stage‑timestamp deltas per §3.2; `ElapsedMilliseconds` is the higher‑precision source once emitted). Powers "slowest items" and "stage p95" charts. |
| `DiagnosticCode` | **Failures** → a *secondary* filter/facet beneath the primary `FailureCategory` filter. | Lets an engineer drill from a business bucket ("EmbeddingEngineFailed: 42 items") into exact technical codes ("38× `EMB_ONNX_OOM`, 4× `EMB_DIM_MISMATCH_512`") — the actionable signal for a support ticket. |
| `StartedUtc` / `CompletedUtc` | **History** → chronological timeline; cross‑correlation with Render deploy/OOM events. | Place each item/stage on an absolute time axis; overlay against deploy markers to spot "everything after 14:03 got slower" (the same correlation the AI17 forensic timeline performs). |
| `MachineName` | **(future)** Failures/History → an "Instance" column/filter, hidden while single‑instance. | Once scaled out: attribute failures/latency to a specific instance ("instance‑B: 3× embedding timeouts") for targeted investigation or instance recycling. |
| `Outcome` + `FailureCategory` (baseline) | **Failures** primary grouping; **History** success/fail badge. | Unchanged from baseline; telemetry augments, never replaces, these. |

---

## 9. Future Application Insights / OpenTelemetry Compatibility

If/when the platform adopts Application Insights or OpenTelemetry (explicitly out of scope today per `AI20_ENROLLMENT_BACKGROUND.md` §3.7, which defers APM), the telemetry DTO is a plain POCO that no APM SDK understands natively — a thin **adapter** at the Infrastructure boundary (e.g. a `TelemetryClient.TrackDependency` / `ActivitySource` writer) would translate it. The point of designing the shape now is that this adapter is a pure translation with **no contract change**. Mapping:

| Telemetry field | Application Insights type / property | OpenTelemetry equivalent | Already shaped correctly? |
|---|---|---|---|
| `Stage` | `DependencyTelemetry.Type` (or the operation/dependency **Name**), or a `customDimensions["Stage"]`. | `Activity`/Span **name** (one span per stage). | ✅ An enum → stable string is a trivial `.ToString()`; no reshaping. |
| `ElapsedMilliseconds` | `DependencyTelemetry.Duration` (`TimeSpan`). | Span **duration** (end − start of the `Activity`). | ✅ `TimeSpan.FromMilliseconds(ElapsedMilliseconds)` — direct. |
| `StartedUtc` | `DependencyTelemetry.Timestamp` (start of the operation). | `Activity.StartTimeUtc`. | ✅ `DateTimeOffset` maps 1:1. |
| `CompletedUtc` | Derives the operation end (`Timestamp + Duration`); or a custom dimension. | `Activity` end time. | ✅ Direct; App Insights prefers start + duration, both of which we carry. |
| `DiagnosticCode` | `DependencyTelemetry.ResultCode`, **or** `TraceTelemetry`/custom `EventTelemetry` property `customDimensions["DiagnosticCode"]`. | Span **status description** / a span **attribute** `enrollment.diagnostic_code`. | ✅ Free‑form string → `ResultCode`/attribute directly. |
| `MachineName` | `TelemetryContext.Cloud.RoleInstance` (`cloud_RoleInstance`). | Resource attribute `service.instance.id` / `host.name`. | ✅ Purpose‑built for this; null today, populated on scale‑out. |
| `Outcome` (baseline) | `DependencyTelemetry.Success` (`bool`, `Succeeded` → true) + `ResultCode`. | Span **status** (`Ok`/`Error`). | ✅ Enum → bool/status. |
| `FailureCategory` (baseline) | `customDimensions["FailureCategory"]` / `EventTelemetry` name. | Span attribute `enrollment.failure_category`. | ✅ Enum → string. |
| `ExecutionTraceId` (from `EnrollmentItemContext`) | `operation_Id` / `operation_ParentId` (end‑to‑end correlation). | `trace_id` (all stage spans share one trace). | ✅ Already the correlation key across all stages (baseline §2 + `AI20_ENROLLMENT_BACKGROUND.md` §3.7). |

**What's already shaped correctly (no change needed):**
- Per‑stage granularity maps 1:1 to one `DependencyTelemetry`/one `Activity` span **because** telemetry lives on each stage DTO (§7) — the umbrella `Pipeline` telemetry becomes the parent `RequestTelemetry`/root span, and each stage becomes a child dependency/span. This parent‑child shape falls out of the §7 design for free.
- `StartedUtc` + `ElapsedMilliseconds` are exactly the (start, duration) pair both SDKs want; we do **not** need to add anything.
- `MachineName` → `cloud_RoleInstance` / `service.instance.id` is a designed‑in slot, so multi‑instance APM needs no reshape.

**What would need to change (adapter‑level only, not a contract break):**
- A `TelemetryClient`/`ActivitySource` **adapter** must be added in Infrastructure to actually emit — the DTO stays a POCO; nothing in `Application` changes.
- `Stage` (and `Outcome`/`FailureCategory`) are enums; the adapter must `.ToString()` them into stable string tags. If APM adoption wanted guaranteed‑stable wire strings, that's an adapter concern, not a DTO change.
- If per‑span correlation (parent/child) is desired, the adapter would open one `Activity` per stage using the shared `ExecutionTraceId` as `trace_id` — a runtime concern, again no DTO change.

Net: adopting App Insights/OpenTelemetry later is an **additive Infrastructure adapter**, because the DTO was shaped to their data model up front. This mirrors §3.7's statement that the existing counters "are the natural source to feed" a future APM — this milestone makes the *per‑stage* level equally APM‑ready.

---

## 10. Cross‑cutting summary

| Concern | Decision |
|---|---|
| **Split vs. flatten** | Split into a reusable `EnrollmentStageTelemetry` sub‑record; embed as optional `Telemetry` on the umbrella `EnrollmentStageResult` **and** each per‑stage result DTO. |
| **`ElapsedMilliseconds`** | Stored `long?` from a monotonic `Stopwatch` — **not** a computed `CompletedUtc - StartedUtc` (precision, negative‑clock safety, null‑robustness). |
| **`DiagnosticCode` vs `FailureCategory`** | Separate concepts: open Application‑layer technical `string?` (many) → closed Domain business enum (one). Many‑to‑one. Never merged; Domain untouched. |
| **`MachineName`** | Nullable `string?`, `null` today (single instance); field exists now to avoid a future contract break at horizontal scale. |
| **Download stage** | `StudentPhotoFetchResult` is reused/frozen and **not** modified; the orchestrator times the download externally and builds `Stage = Download` telemetry itself. |
| **APM readiness** | Shape maps 1:1 to App Insights `DependencyTelemetry` / OpenTelemetry spans; adoption is an additive Infrastructure adapter, no contract break. |
| **Authority** | Telemetry never drives branching, retry, or persistence — purely observational and additive. |
| **Backward compatibility** | All new members optional/nullable; baseline `Ok()/Fail()/Retry()` factories preserved; enrichment via `with`/`WithTelemetry`. |

---

## Constraints Confirmed

- **No production code was written or modified.** Every C# block above is a **proposed** contract enhancement inside a markdown code block — no `.cs`, `.csproj`, `.tsx`, `.ts`, or any non‑markdown file was created or edited anywhere in the repository.
- **No telemetry was implemented.** No `ILogger`, `TelemetryClient`, `ActivitySource`, metrics, or APM integration exists or was wired — this document designs DTO **shape** only.
- **Domain untouched.** `FailureCategory` and `EnrollmentStatus` are reused unchanged; `DiagnosticCode` is deliberately an Application‑layer telemetry field, not a new Domain enum member.
- **Additive & backward‑compatible.** All new members are optional (nullable or defaulted); the baseline `EnrollmentStageResult` factories and every existing proposed call‑site continue to compile.
- **Only one file produced:** `docs/AI20_PHASE2_PIPELINE_TELEMETRY.md`. Pre‑existing uncommitted files from earlier AI20 milestones are unrelated to this task and were not touched.
