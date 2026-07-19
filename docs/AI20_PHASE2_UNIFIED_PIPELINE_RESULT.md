# AI20.PHASE2.0.12 — Unified Pipeline Result Model (Capstone Review)

**Type:** Contract/architecture review only. No production code was written or modified to produce this document. No business logic, HTTP endpoint, background worker, DI registration, database update, migration, or image processing is implemented here. Every C# block below is a **proposed** contract rendered inside a markdown code block — it does not exist as a `.cs` file and this milestone commits no code.

**Role of this milestone:** this is the **capstone review** of the Phase 2 contract-enhancement wave (AI20.PHASE2.0.1 → 0.11). It does not add a new stage feature; it steps back and asks a single architectural question of the whole system those eleven documents describe:

> Should the pipeline stop returning a *focused DTO per stage* and instead compose everything into one immutable **`EnrollmentPipelineResult`** object — conceptually `{ DownloadResult, ValidationResult, StorageResult, EmbeddingResult, AuditResult, Statistics, ElapsedTime }` — threaded through the pipeline and rebuilt after every stage?

**Documents reviewed as the evidence base (all read in full):**
`AI20_PHASE2_ENGINE_CONTRACTS.md` (0.1), `AI20_PHASE2_PIPELINE_CONTEXT.md` (0.2), `AI20_PHASE2_PIPELINE_TELEMETRY.md` (0.3), `AI20_PHASE2_EMBEDDING_METADATA.md` (0.4), `AI20_PHASE2_PIPELINE_STAGE_DESIGN.md` (0.5), `AI20_PHASE2_AUDIT_ENHANCEMENT.md` (0.6), `AI20_PHASE2_HEALTH_METRICS.md` (0.7), `AI20_PHASE2_RESULT_WRITER_REVIEW.md` (0.8), `AI20_PHASE2_PROGRESS_STREAMING.md` (0.9), `AI20_PHASE2_STORAGE_ASSETS.md` (0.10), `AI20_PHASE2_VALIDATION_REPORT.md` (0.11), plus `AI20_ENROLLMENT_BACKGROUND.md` for batch-scale context.

---

## 1. The proposal, stated precisely

The candidate architecture replaces the current per-stage return values with a single accumulating record:

```csharp
// CANDIDATE (under review — NOT recommended in its threaded form; see §3-§10).
public sealed record EnrollmentPipelineResult
{
    public StudentPhotoFetchResult?   DownloadResult  { get; init; }
    public EnrollmentValidationResult? ValidationResult { get; init; }
    public EnrollmentStorageResult?    StorageResult   { get; init; }
    public EnrollmentEmbeddingResult?  EmbeddingResult { get; init; }
    public EnrollmentAuditEvent?       AuditResult     { get; init; }
    public EnrollmentStatistics?       Statistics      { get; init; }
    public TimeSpan?                   ElapsedTime     { get; init; }
}
```

The critical, and frequently glossed-over, detail is **the lifetime of this object across the pipeline**. There are two fundamentally different ways to use it, and the prompt's phrasing ("built up and passed around", "rebuilt after every stage") describes the first:

- **(T) Threaded / accumulating.** The object is created at `t=0` (before any stage runs), passed into every stage, and rebuilt via `with { … }` after each stage populates its slot. Because nothing has executed at construction, **every sub-field must be nullable**.
- **(A) Additive terminal aggregate.** The per-stage DTOs are unchanged and remain the real return values; a lightweight summary is constructed **once, at the very end**, after all stages have completed, purely to hand off to the terminal consumers in one shot.

These are not the same design, and the analysis below is decisive against **(T)** and constructively in favor of a narrower form of **(A)**. Sections §3–§9 evaluate the seven dimensions the task requires; §10 reconciles with 0.5 and 0.3; §11 gives the decision; §12 designs the recommended middle ground; §13 shows the sequence.

---

## 2. What each stage returns today (the baseline being challenged)

Grounded in the exact contracts as they stand after the 0.1–0.11 wave:

| Stage / source | Return DTO (as finalized by the wave) | Populated at | Memory-heavy members |
|---|---|---|---|
| Download | `StudentPhotoFetchResult` (reused/frozen, 0.1 §1; 0.3 §7) | after fetch | raw photo `byte[]` |
| Validate | `EnrollmentValidationResult` **+ required `ValidationReport`** (0.11 §9) | after validate | `AlignedFaceBytes` (`byte[]?`) |
| Storage | `EnrollmentStorageResult` **(multi-asset, keyed dict)** (0.10 §4) | after upload | keys/checksums only (no bytes) |
| Embedding | `EnrollmentEmbeddingResult` **+ full provenance** (0.4 §1) | after embed | `NormalizedVector` (`float[512]`) |
| Any stage (telemetry) | `EnrollmentStageResult` **+ `EnrollmentStageTelemetry`** (0.3 §3) | per stage | none |
| Audit | `EnrollmentAuditEvent` (0.1 §12; 0.2 §7.3; 0.6) | per lifecycle action | none (bytes/vectors forbidden) |
| Terminal write | `EnrollmentSuccessWrite` / `EnrollmentFailureWrite` (0.1 §11; 0.2 §7.2; 0.4 §10) | at finalize | embeds `NormalizedVector` via `Embedding` |
| Read models | `EnrollmentStatistics` / `EnrollmentHealthDto` (0.1 §7; 0.7) | on demand, out-of-band | none |

Two facts from the table dominate the whole review:

1. **Each DTO is fully populated (non-nullable in its own right) at the exact instant its stage completes.** `EnrollmentValidationResult.Report` is `required` (0.11 §9); `EnrollmentEmbeddingResult`'s nine members are all `required` (0.4 §1). These are honest, closed, complete values.
2. **`Statistics` and `AuditEvent` are not per-item pipeline outputs at all.** `EnrollmentStatistics` is a batch-level read model served out-of-band by `IEnrollmentStatisticsService`/`IEnrollmentHealthService` (0.7); `EnrollmentAuditEvent` is an append-only side-channel record (0.6). The candidate `EnrollmentPipelineResult` conflates three different scopes — per-stage outputs, a batch-level aggregate, and a side-channel event — into one per-item object. That conflation is itself a design smell examined in §9.

---

## 3. Simplicity — does one object simplify the orchestrator, or relocate complexity?

**Finding: the threaded form (T) *relocates and increases* complexity; it does not remove it.**

Concretely, trace what (T) forces on the orchestrator (whose contract, per 0.1 §4 and 0.2 §7.1, is "sequencing and branching only"):

- At `t=0` the orchestrator must construct an `EnrollmentPipelineResult` in which **every sub-field is `null`**, because download has not run. This immediately violates the wave's core DTO discipline — 0.1 §0.4 mandates `required`/`init` members and 0.11 §9.3 deliberately made `ValidationReport` `required` precisely so "no caller has to null-check the report itself." A t=0 unified object re-introduces exactly the nullability the wave engineered away.
- After **every** stage the orchestrator rebuilds the object: `result = result with { ValidationResult = … }`, then `with { StorageResult = … }`, etc. That is five `with` allocations per item instead of five local variables — more code and more garbage, not less.
- Downstream code can no longer rely on the type: any reader of `EnrollmentPipelineResult.EmbeddingResult` must first prove the embedding stage ran and succeeded, so `if (result.EmbeddingResult is null)` guards proliferate at every use site. The current design encodes "this ran and succeeded" in *control flow* (the orchestrator only reaches the embedding call after validation returned `Succeeded`), which is simpler and compiler-checked.

By contrast, the current per-stage flow the orchestrator already implements is the textbook-simple shape:

```csharp
// Illustrative orchestrator body (per 0.1 §4 + 0.5 §7.2) — focused locals, no giant record.
var download   = await photo.FetchPhotoAsync(req, ct);        // fully populated here
if (download.Outcome != Ok) return RouteFailure(download);
var validation = await validator.ValidateAsync(bytes, ct);    // fully populated here
if (!validation.IsValid) return RouteFailure(validation);
var storage    = await store.PersistEnrollmentAssetsAsync(req, ct);
var embedding  = await embedder.GenerateAsync(alignedFace, ct);
await writer.WriteSuccessAsync(new EnrollmentSuccessWrite { … }, ct); // composed ONCE, at the end
```

Each local is non-null and complete the moment it exists; there is exactly one place (the `WriteSuccessAsync` construction) where a composite is assembled — and **the wave already designed that composite**: `EnrollmentSuccessWrite` (0.4 §10) is the single terminal aggregate for the success path. So the "one thing to build up" appeal is already satisfied at the correct point (the end), without threading.

**Verdict on simplicity: NO net simplification from (T). The current design is simpler; the only genuine "single object" convenience already exists as `EnrollmentSuccessWrite`.**

---

## 4. Extensibility & Open/Closed — the decisive dimension

**Finding: a closed unified record with a named property per stage violates the Open/Closed Principle against the seven future stages 0.5 already scoped; the per-DTO design does not. This is the single strongest argument, and it resolves against (T).**

0.5 §6 scopes seven concrete future stages: Liveness, Mask, Occlusion, Spoof, Face Quality Ranking, Duplicate Detection, Face Normalization. Consider adding stage #8 under each design:

| Action needed to add a new stage | Unified `EnrollmentPipelineResult` (closed record) | Current per-stage DTOs |
|---|---|---|
| Where the new result type lives | A **new named property** (`LivenessResult`, `MaskResult`, …) added to the shared record | A **new independent DTO** (`EnrollmentLivenessResult`) in its own file |
| Who is forced to recompile / re-touch | Every producer and every consumer of `EnrollmentPipelineResult` — a widely-referenced type threaded through the whole pipeline | Nothing except the new stage and the orchestrator hook that calls it |
| Open/Closed compliance | **Violated** — the shared type is *modified* (opened) for every extension | **Honored** — the system is *extended* by adding a new closed type |
| Property count trajectory | Grows unbounded; a mature pipeline has a 12+ property record where any given item populates a handful | Flat — each stage owns exactly its own shape |

This is not hypothetical for AI20: 0.5 explicitly designed the extension mechanism (`IEnrollmentPipelineStage` + `EnrollmentPipelineContext`) so that adding a stage is "an enum value, not a DTO reshape" (echoing 0.10 §4.1's identical reasoning for asset kinds, and 0.4's "single source of truth, adding a field requires no change to `EnrollmentSuccessWrite`"). A closed unified result record runs directly counter to a decision the wave has already made in three separate documents.

Crucially, 0.5 §3 **already answered this exact sub-question**: it decided stages return the shared `EnrollmentStageResult` (disposition only) and push produced *data* into the write-once `EnrollmentPipelineContext` accumulator — explicitly **not** a shared "result with a slot per stage." The unified `EnrollmentPipelineResult` is the design 0.5 considered and rejected as "Option A / the untyped-or-wide shape," under a different name.

**Verdict on extensibility: decisively AGAINST (T). Adding a stage must not require editing a shared, widely-referenced type.**

---

## 5. Memory impact — object lifetime under thousand-student batches

**Finding: threading one accumulating object keeps memory-heavy fields alive far longer than necessary; at batch scale this is a real, avoidable GC/residency cost.**

`AI20_ENROLLMENT_BACKGROUND.md` §51 confirms "a SuperAdmin can enqueue **thousands of students** in one batch," and the worker processes items with bounded concurrency (0.1 §6/§8 semaphores) — so several items are in flight simultaneously, each holding one context.

The memory-heavy fields, located precisely in the docs:

- **Raw downloaded photo** — `byte[]` from `StudentPhotoFetchResult` / `EnrollmentPipelineContext.DownloadedPhoto` (0.5 §2.2). Full-resolution source image; the largest single item (often hundreds of KB to multiple MB per exambranch-style photo).
- **`AlignedFaceBytes`** — WebP-encoded aligned crop on `EnrollmentValidationResult` / `EnrollmentValidatedFace` (0.1 §8; 0.11; 0.5 §2.2). Smaller, but still image bytes.
- **`NormalizedVector`** — `float[512]` on `EnrollmentEmbeddingResult` (0.4 §1) ≈ 2 KB, but multiplied across concurrent in-flight items and retained if threaded.
- **Storage asset inputs** — `EnrollmentAssetInput.Bytes` per asset (0.10 §4), potentially Original + Aligned + Audit bytes held together.

Object-lifetime contrast:

| Field | Current per-stage lifetime | Threaded `EnrollmentPipelineResult` lifetime |
|---|---|---|
| raw downloaded photo | can be released as soon as validation has consumed it and storage has the bytes it needs — its local goes out of scope mid-pipeline | pinned in `DownloadResult` for the **entire** item, alive through embedding + write |
| `AlignedFaceBytes` | consumed by storage + embedding, then droppable before the write commits | pinned in `ValidationResult` until the whole object is discarded |
| `NormalizedVector` | needed only from embed → write; the `EnrollmentSuccessWrite` carries it exactly across that gap | pinned in `EmbeddingResult` for the full object lifetime, even on branches that don't need it |

The current design lets the GC reclaim each large buffer at its natural end-of-use; a single growing object holds **the union of all heavy buffers** alive until the item finishes — the worst residency profile precisely when N items run concurrently over a batch of thousands. 0.5 §2.2 chose the write-once accumulator deliberately as "the minimal, contained mutation surface … discarded when the item completes," but even that accumulator is a *transient working buffer*, not a value returned and retained; promoting it to a threaded return object would extend its life further and is not what 0.5 proposed.

**Verdict on memory: AGAINST (T). It maximizes the lifetime of exactly the fields the docs flag as byte-heavy, at the batch scale the background doc warns about.**

---

## 6. Serialization — over-exposure risk

**Finding: a unified "whole result" object is a standing invitation to serialize internal-only data (vectors, image bytes) across a process boundary; focused DTOs are independently reviewable and keep that data contained.**

Two boundaries in the wave could serialize a result:

- **Progress streaming (0.9).** The transport serializes **only `EnrollmentProgress`** (counters/status/timestamps) with an ETag from `RowVersion` (0.9 §1, §5). It deliberately never touches per-item internals. If a unified `EnrollmentPipelineResult` existed and someone wired "stream the result," they would ship `NormalizedVector` and `AlignedFaceBytes` to a browser dashboard — a serious leak and a bandwidth disaster. With focused DTOs, `EnrollmentProgress` is the only thing in scope to stream; the leak is structurally impossible.
- **Audit export (0.6).** 0.6 and 0.1 §12 are emphatic: audit "never records image bytes or embedding vectors," enforced at the contract by the constrained `Detail` field. A unified object that bundles the vector *next to* the audit event makes "serialize the whole result to the audit sink" a one-line mistake that defeats that rule. 0.3 §7 made the same call for telemetry — it kept telemetry *out* of `EnrollmentAuditEvent`, correlating by `ExecutionTraceId` instead of merging.

The general principle the wave repeatedly applies: each DTO is independently reviewable for "is this safe to expose?" — `EnrollmentProgress` yes, `ValidationReport` (measurements only, no bytes — 0.11 §9.4) yes, `NormalizedVector` almost never. A unified object erases those per-type review boundaries and collapses them into one "the whole thing," which is the easiest possible surface on which to accidentally over-serialize.

**Verdict on serialization: AGAINST (T). Focused DTOs preserve per-type "safe-to-expose" review; a unified object destroys it.**

---

## 7. Testing — mocking and unit-test ergonomics

**Finding: focused per-stage results are strictly easier to construct and assert in isolation; a shared shape degrades test-double ergonomics.**

Testing `IEnrollmentValidationService.ValidateAsync()` in isolation (0.1 §8) today means: feed bytes, assert on a fully-populated `EnrollmentValidationResult` whose `Report` is `required` and complete (0.11). The test constructs and inspects exactly the surface under test.

If the contract even *implied* a shared `EnrollmentPipelineResult`, a validation test would have to instantiate an object with `DownloadResult`, `StorageResult`, `EmbeddingResult`, `AuditResult`, `Statistics`, `ElapsedTime` all set to `null` just to exercise the validation slot — noise that has nothing to do with validation, and a standing temptation to over-specify. Mocking a stage that returns a focused DTO is a one-liner; mocking one that must return/accept a giant partially-null envelope forces every test to know the whole pipeline shape — reintroducing the coupling 0.5 §1.1 rejected for the interface itself.

The wave's own testing posture supports this: 0.6 §2 praises "central action-to-category mapping is easier to exhaustively test," and 0.5 §1.1 chose the non-generic uniform stage interface specifically so stages stay independently testable. Focused results are the same instinct at the DTO level.

**Verdict on testing: AGAINST (T). Focused DTOs are the more testable shape.**

---

## 8. Future AI stages — Open/Closed through the 0.5 lens (reconciliation)

This dimension is the extensibility argument (§4) restated against the *specific* seven-stage roadmap, and it is where 0.5 must be reconciled explicitly.

0.5's `IEnrollmentPipelineStage` design **is already a considered, compatible answer to 0.12's question**, and it answers it in the *opposite* direction from the unified object:

- 0.5 §3 has stages return the **existing focused `EnrollmentStageResult`** (disposition), with produced data flowing into the `EnrollmentPipelineContext` accumulator by **named, write-once, per-artifact slot** — and it explicitly rejects both the "untyped bag" and the "orchestrator-threaded typed inputs" options. A unified `EnrollmentPipelineResult` with a fixed property per stage is a third framing of the same rejected idea (a shared object every stage reads/writes), just made a *return value* instead of an accumulator.
- 0.5 §7 keeps the five core stages directly orchestrated and adds new stages at two extension points **without touching a shared result type**. Under a unified result, each of the seven future stages would instead need its own named property on `EnrollmentPipelineResult` — reintroducing precisely the "orchestrator must know every stage" coupling 0.5 §2.1 eliminated.

**Reconciliation statement:** 0.5 and 0.12 cannot both bless a shared per-stage result object, because 0.5 already decided against one. They are reconciled by adopting 0.5's answer as authoritative: **the pipeline stays per-DTO / per-stage; there is no threaded unified result.** 0.12 does not overturn 0.5; it confirms and generalizes it, and (in §12) adds only a *terminal, additive* aggregate that 0.5 did not need to address because 0.5 was about stage plumbing, not about the single hand-off at the very end.

**Verdict: AGAINST (T); in agreement with 0.5.**

---

## 9. Future telemetry — does a singular `ElapsedTime` regress 0.3?

**Finding: YES — a unified object with a singular `ElapsedTime` is a concrete regression against the per-stage telemetry 0.3 deliberately designed. This alone disqualifies the candidate's literal shape.**

0.3 §3 designed `EnrollmentStageTelemetry` to carry **per stage**: `Stage`, `ElapsedMilliseconds` (from a monotonic `Stopwatch`, §4.1), `DiagnosticCode`, `StartedUtc`, `CompletedUtc`, `MachineName`. It embedded this on **each** per-stage DTO *and* on the umbrella `EnrollmentStageResult` (0.3 §7), specifically so a dashboard can "group durations by stage" and map 1:1 to App Insights `DependencyTelemetry` / OpenTelemetry child spans (0.3 §9). The whole point was granular, per-stage, queryable timing — `AvgDownloadMs` / `AvgValidationMs` / `AvgEmbeddingMs` (0.7 §3; 0.1 §7).

The candidate `EnrollmentPipelineResult` in the prompt carries a single `ElapsedTime`. Taking that literally throws away the per-stage breakdown: you can no longer answer "which stage was the bottleneck?" — you get one aggregate number for the whole item, which is strictly less information than `EnrollmentStageTelemetry` already provides per stage. Even if the unified object nested each stage's telemetry inside its sub-result, that granularity **already exists** on the focused DTOs; the unified wrapper adds nothing to telemetry and, in its singular-`ElapsedTime` form, actively removes it.

**Verdict on telemetry: AGAINST (T). The singular `ElapsedTime` is a regression; 0.3's per-stage embedding already achieves queryable per-stage telemetry without any unified object.**

---

## 10. Summary of the seven-dimension verdict

| Dimension | Threaded unified `EnrollmentPipelineResult` (T) | Winner |
|---|---|---|
| Simplicity (§3) | All-nullable t=0 object, five `with` rebuilds, null-guards everywhere; `EnrollmentSuccessWrite` already is the end-composite | **Per-DTO** |
| Extensibility / Open-Closed (§4) | Stage #8 = modify a shared, widely-referenced record | **Per-DTO** |
| Memory (§5) | Pins raw photo + `AlignedFaceBytes` + `NormalizedVector` alive for the whole item × concurrent items × thousands/batch | **Per-DTO** |
| Serialization (§6) | Invites shipping vectors/bytes to dashboards/audit; erases per-type review | **Per-DTO** |
| Testing (§7) | Forces whole-pipeline shape into isolated stage tests | **Per-DTO** |
| Future AI stages / 0.5 (§8) | Re-introduces the "know every stage" coupling 0.5 removed | **Per-DTO** |
| Future telemetry / 0.3 (§9) | Singular `ElapsedTime` regresses 0.3's per-stage granularity | **Per-DTO** |

Seven of seven favor the existing per-stage architecture. The decision is not close.

---

## 11. Decision

**NO — the threaded/accumulating unified `EnrollmentPipelineResult` is not superior and must not be adopted. Retain the current per-stage DTO architecture (0.1–0.11) unchanged.**

**AND — adopt a narrow MIDDLE GROUND (§12): a lightweight, *additive*, *terminal-only* summary that the orchestrator composes exactly once, after all stages complete, purely to hand off to the terminal consumers (`IEnrollmentResultWriter`, `IEnrollmentAuditService`, `IEnrollmentProgressReporter`) in one call-cluster.** It captures the *only* legitimate appeal of "one result" — a single object to write/audit/report at the very end — while structurally avoiding every Open/Closed, memory-lifetime, serialization, and telemetry problem of threading, because it is never passed *into* any stage and never held during the live pipeline.

This is deliberately consistent with the wave's own precedents:
- `EnrollmentSuccessWrite` (0.4 §10) is *already* a terminal composite for the success path — the middle ground generalizes that pattern; it does not invent a new philosophy.
- It is additive and non-authoritative, exactly like 0.3's telemetry and 0.7's health DTO: it wraps existing focused DTOs by reference and changes none of them.

---

## 12. The recommended middle ground — `EnrollmentPipelineSummary` (terminal, additive)

### 12.1 Design constraints

1. **Terminal-only.** Constructed once, at the `Writing` stage boundary (0.2 §4 `EnrollmentStage.Writing`), *after* embedding (success) or at the point of a terminal failure. Never created at `t=0`, never passed into a stage, never rebuilt via `with` mid-pipeline.
2. **By-reference wrapper, not a copy.** It holds references to the already-produced focused DTOs; it does not re-declare their fields. Adding a future stage's DTO does **not** require adding a property here (see §12.3 — the optional-signals collection absorbs new advisory stages), so it stays Open/Closed-compatible.
3. **No byte-heavy internals in its serialized projection.** It exposes a `ToLogView()` / audit projection that excludes `NormalizedVector` and any `byte[]`, honoring 0.6 and 0.9.
4. **Per-stage telemetry preserved.** It references each stage's `EnrollmentStageTelemetry` (0.3) as a list — it does **not** flatten to a singular `ElapsedTime` (though it may expose a derived `TotalElapsed` convenience alongside, never instead of, the per-stage records).

### 12.2 Proposed contract (illustrative, additive)

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// ADDITIVE, TERMINAL-ONLY hand-off aggregate. Composed ONCE by the orchestrator after all stages
/// complete (at EnrollmentStage.Writing) so the terminal consumers — IEnrollmentResultWriter,
/// IEnrollmentAuditService, IEnrollmentProgressReporter — receive one object instead of several
/// loose locals. It WRAPS the existing focused DTOs by reference; it does NOT replace them, is NEVER
/// threaded into a stage, and is NEVER rebuilt mid-pipeline. See AI20_PHASE2_UNIFIED_PIPELINE_RESULT.md.
///
/// Immutability: a plain sealed record; because it is built only at the end, every reference it holds
/// is already fully populated — none of its members needs to be nullable "because a stage hasn't run"
/// (contrast the rejected threaded EnrollmentPipelineResult). Members are nullable ONLY where the
/// terminal outcome legitimately lacks them (e.g. a validation-stage failure has no EmbeddingResult).
/// </summary>
public sealed record EnrollmentPipelineSummary
{
    /// <summary>The enriched, immutable per-item context (0.2 §4), CurrentStage == Writing here.</summary>
    public required EnrollmentItemContext Context { get; init; }

    /// <summary>The terminal disposition (0.1 §2). Success or the first non-Succeeded stage's verdict.</summary>
    public required EnrollmentStageResult Outcome { get; init; }

    // ---- References to whichever focused DTOs were produced before the pipeline terminated ----
    // Nullable ONLY because a pipeline that fails at stage K legitimately never produced stage K+1's DTO.

    public EnrollmentValidationResult? Validation { get; init; }   // incl. required ValidationReport (0.11)
    public EnrollmentStorageResult?    Storage    { get; init; }   // multi-asset keyed result (0.10)
    public EnrollmentEmbeddingResult?  Embedding  { get; init; }   // full provenance (0.4)

    /// <summary>Per-stage telemetry, in execution order (0.3). NOT a singular ElapsedTime — the
    /// per-stage breakdown 0.3 designed is preserved. TotalElapsed below is a derived convenience only.</summary>
    public required IReadOnlyList<EnrollmentStageTelemetry> StageTelemetry { get; init; }

    /// <summary>Convenience roll-up = sum/window of StageTelemetry; NEVER the sole timing record.</summary>
    public TimeSpan? TotalElapsed { get; init; }

    /// <summary>Advisory signals from optional stages (0.5 §2.2 StageSignals: liveness, spoof, …).
    /// A NEW optional stage adds a dictionary entry, NOT a new property — this is the Open/Closed seam.</summary>
    public IReadOnlyDictionary<string, double> StageSignals { get; init; }
        = new Dictionary<string, double>();

    /// <summary>Audit/log-safe projection: excludes NormalizedVector and all byte[] (0.6, 0.9).</summary>
    public EnrollmentSummaryLogView ToLogView() => new()
    {
        ExecutionTraceId = Context.ExecutionTraceId,
        CorrelationId    = Context.CorrelationId,
        Outcome          = Outcome.Outcome,
        FailureCategory  = Outcome.FailureCategory,
        FailedAtStage    = Context.CurrentStage,
        ValidationReport = Validation?.Report,          // measurements only — no bytes (0.11 §9.4)
        EmbeddingVersion = Embedding?.EmbeddingVersion, // provenance token, not the vector (0.4)
        StorageKeys      = Storage?.Assets.Keys.ToArray(),
        StageTelemetry   = StageTelemetry,
        TotalElapsed     = TotalElapsed,
    };
}

/// <summary>Deliberately vector-free / byte-free view safe to log, audit-correlate, or (future) stream.</summary>
public sealed record EnrollmentSummaryLogView
{
    public required Guid ExecutionTraceId { get; init; }
    public Guid? CorrelationId { get; init; }
    public required EnrollmentStageOutcome Outcome { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public EnrollmentStage FailedAtStage { get; init; }
    public ValidationReport? ValidationReport { get; init; }
    public string? EmbeddingVersion { get; init; }
    public IReadOnlyList<EnrollmentAssetKind>? StorageKeys { get; init; }
    public required IReadOnlyList<EnrollmentStageTelemetry> StageTelemetry { get; init; }
    public TimeSpan? TotalElapsed { get; init; }
}
```

### 12.3 Why this is NOT the rejected object in disguise

| Property of the rejected (T) | `EnrollmentPipelineSummary` (middle ground) |
|---|---|
| Created at `t=0`, all-nullable | Created at `Writing`, references already fully populated |
| Threaded into every stage | Never passed into any stage |
| Rebuilt via `with` after each stage | Built exactly once |
| New stage ⇒ new named property (Open/Closed violation) | New advisory stage ⇒ a `StageSignals` entry; the summary type is untouched |
| Holds heavy buffers alive for the whole item | Holds them only for the brief write/audit hand-off at the end (they were alive at that instant anyway) |
| Singular `ElapsedTime` (regresses 0.3) | `IReadOnlyList<EnrollmentStageTelemetry>` preserved; `TotalElapsed` is additive only |
| Serializing "the whole result" leaks vectors | `ToLogView()` is the serialized surface and is vector/byte-free by construction |

### 12.4 Relationship to the existing terminal DTOs

- `EnrollmentSuccessWrite` / `EnrollmentFailureWrite` (0.1 §11, 0.4 §10) **remain the writer's inputs, unchanged.** The summary does not replace them; on the success path the orchestrator can build `EnrollmentSuccessWrite` from the same locals and pass the summary to audit/telemetry. Optionally, `EnrollmentSuccessWrite` could be constructed *from* the summary, but that is an implementation nicety, not a contract change.
- `EnrollmentStatistics` / `EnrollmentHealthDto` are **not** included. They are batch-level read models served out-of-band (0.7); folding a batch aggregate into a per-item summary would repeat the scope-confusion of the candidate (§2). The summary is strictly per-item.
- `EnrollmentAuditEvent` is **not** a field; the summary *feeds* `IEnrollmentAuditService.RecordAsync` via `ToLogView()`, keeping audit a side-channel (0.6) rather than a slot in a result.

### 12.5 Is the middle ground even necessary?

Honestly, it is **optional**. The orchestrator already has focused locals and already composes `EnrollmentSuccessWrite` at the end. `EnrollmentPipelineSummary` earns its place only if the terminal hand-off to *three* consumers (writer + audit + progress) benefits from one parameter instead of several — a modest ergonomic win and a clean single object to log per item. It is recommended as a **low-cost, additive convenience**, explicitly not required for correctness, and trivially droppable. That framing matches the wave's consistent posture (0.7, 0.8, 0.9 all add narrowly and defer anything speculative).

---

## 13. Updated sequence diagram — where composition happens under the recommendation

Composition occurs **once, at the `Writing` boundary** (mapping onto baseline 0.1 §15 / 0.2 §8), after all stages produced their focused DTOs — never threaded through the earlier stages.

```mermaid
sequenceDiagram
    participant BG as EnrollmentBackgroundService
    participant ORCH as IEnrollmentOrchestrator
    participant PHOTO as IStudentPhotoProvider
    participant VAL as IEnrollmentValidationService
    participant STORE as IEnrollmentStorageService
    participant EMB as IEnrollmentEmbeddingService
    participant SUM as EnrollmentPipelineSummary (built ONCE, terminal)
    participant WRITE as IEnrollmentResultWriter
    participant AUD as IEnrollmentAuditService
    participant PROG as IEnrollmentProgressReporter

    BG->>ORCH: ProcessItemAsync(context)

    ORCH->>PHOTO: FetchPhotoAsync()
    PHOTO-->>ORCH: StudentPhotoFetchResult (focused, fully populated) + Download telemetry
    Note over ORCH: local var only — NOT threaded into a giant object

    ORCH->>VAL: ValidateAsync(bytes)
    VAL-->>ORCH: EnrollmentValidationResult (+ required ValidationReport) + Validation telemetry
    Note over ORCH: AlignedFaceBytes consumed by next stages, then droppable

    ORCH->>STORE: PersistEnrollmentAssetsAsync()
    STORE-->>ORCH: EnrollmentStorageResult (multi-asset) + Storage telemetry

    ORCH->>EMB: GenerateAsync(alignedFace)
    EMB-->>ORCH: EnrollmentEmbeddingResult (+ provenance, NormalizedVector) + Embedding telemetry

    rect rgb(230,255,230)
    Note over ORCH,SUM: === WRITING boundary — compose ONCE from the focused locals ===
    ORCH->>SUM: new EnrollmentPipelineSummary { Validation, Storage, Embedding, StageTelemetry[…] }
    end

    ORCH->>WRITE: WriteSuccessAsync(EnrollmentSuccessWrite)   %% single DB transaction (0.1 §11)
    ORCH->>AUD: RecordAsync(from summary.ToLogView())          %% vector/byte-free (0.6)
    ORCH->>PROG: (transition already atomic; progress read is O(1), out-of-band 0.9)
    ORCH-->>BG: EnrollmentStageResult (terminal, already persisted)
    BG->>PROG: FinalizeBatchIfCompleteAsync(batchId)
```

On a **failure** branch (e.g. validation rejects), the summary is composed at that terminal point with `Embedding == null` — legitimately null because embedding never ran — and fed to `WriteFailureAsync` + audit identically. No earlier stage ever saw a partial object.

---

## 14. Cross-cutting summary

| Concern | Decision |
|---|---|
| Threaded/accumulating `EnrollmentPipelineResult` | **Rejected** on all seven dimensions (§3–§9). |
| Per-stage focused DTOs (0.1–0.11) | **Retained unchanged.** They are fully-populated, Open/Closed-friendly, memory-lean, independently reviewable, and testable. |
| 0.5 `IEnrollmentPipelineStage` reconciliation | **In agreement** — 0.5 already rejected a shared per-stage result object; 0.12 confirms it (§8). |
| 0.3 per-stage telemetry | **Preserved** — a singular `ElapsedTime` would regress it; the summary carries `IReadOnlyList<EnrollmentStageTelemetry>` (§9, §12). |
| Middle ground | **Adopt (optional, additive):** `EnrollmentPipelineSummary`, terminal-only, by-reference, vector/byte-free log view — the one legitimate "single object at the end" without the threading harms (§12). |
| Read models (`EnrollmentStatistics`, `EnrollmentHealthDto`) | **Excluded** from any per-item summary — they are batch-level, out-of-band (0.7). |
| `EnrollmentSuccessWrite` / audit / progress | **Unchanged** as terminal inputs; the summary feeds them, it does not replace them. |

---

## Constraints Confirmed

- **No production code was written or modified.** Every C# block above (`EnrollmentPipelineResult` candidate, `EnrollmentPipelineSummary`, `EnrollmentSummaryLogView`) is a **proposed/illustrative** contract rendered inside markdown — no `.cs`, `.csproj`, `.tsx`, `.ts`, or any other non-markdown file was created, edited, or deleted anywhere in the repository.
- **Nothing was implemented.** No business logic, HTTP endpoint, background worker, orchestrator, DI registration, database update, migration, or image processing was written or changed.
- **This is a design/documentation-only capstone review.** It evaluates the architecture the eleven prior documents collectively describe and reaches a decisive recommendation; it does not amend those documents' committed contracts.
- **All existing Application-layer contracts and Domain enums/value objects are reused without modification** (`EnrollmentItemContext`, `EnrollmentStageResult`, `EnrollmentStageTelemetry`, `EnrollmentValidationResult`/`ValidationReport`, `EnrollmentStorageResult`, `EnrollmentEmbeddingResult`, `EnrollmentSuccessWrite`, `EnrollmentAuditEvent`, `EnrollmentStatistics`, `EnrollmentHealthDto`, `FailureCategory`).
- **The single output artifact of this milestone is exactly one new markdown file:** `docs/AI20_PHASE2_UNIFIED_PIPELINE_RESULT.md`, containing the seven-dimension analysis, the explicit reconciliation with 0.5's `IEnrollmentPipelineStage` design and 0.3's per-stage telemetry design, the decisive recommendation, the proposed middle-ground object model as C# code blocks, an updated Mermaid sequence diagram showing where composition happens, and this Constraints Confirmed section.
