# AI20.PHASE2.0.4 — Embedding Metadata Contracts

**Type:** Contract/interface design only. No production code was written or modified to produce this document. This milestone enhances the **proposed** `EnrollmentEmbeddingResult` DTO from `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §10 with embedding provenance metadata, and reconciles how that metadata flows into the proposed `EnrollmentSuccessWrite` (§11). Nothing here changes embedding generation: `InsightFaceEngine`, `IEmbeddingGenerator`, `EmbeddingNormalizer`, and the ONNX sessions are untouched.

**Status of the code blocks below:** every C# block is a **proposed** contract enhancement rendered inside a markdown code block. None of it exists as a `.cs` file, and this milestone commits no code.

---

## 0. Baseline recap and inputs

The enhancement is anchored to what already exists so the new metadata neither duplicates nor contradicts the real persistence layer.

| Source | Field | Meaning today |
|---|---|---|
| `EnrollmentEmbeddingResult` (proposed, §10) | `NormalizedVector`, `Model`, `Version`, `Quality`, `Dimension` | The current, minimal shape being enhanced here. |
| `IEmbeddingGenerator` (real) | `ProviderName`, `ModelName`, `Version` | The precedent for provider/model/version naming. `ProviderName` ∈ `EmbeddingProviders`. |
| `EmbeddingGenerationResult` (real) | `EmbeddingVector`, `Model`, `Version`, `Quality`, `ExpectedDimension` | The real result record. Note it *already* separates the actual vector from an `ExpectedDimension`. |
| `StudentFaceEmbedding` (real entity) | `EmbeddingModel`, `EmbeddingVersion`, `EmbeddingDimension`, `EmbeddingQuality`, `PhotoVersion` | The persisted columns. New metadata must map cleanly onto these, not fight them. |
| `EmbeddingQuality` (real enum) | `Unknown / Poor / Fair / Good / Excellent` | Reused unchanged; no new quality vocabulary is introduced. |
| `EmbeddingProviders` (real constants) | `InsightFace / FaceNet / AzureFace / OpenCV` | The provider vocabulary `EngineProvider` draws from. |
| `IEmbeddingProviderFactory` (real) | `GetProvider(name)` / `GetDefaultProvider()` / `GetRegisteredProviders()` | Resolves one provider per name — the multi-provider seam this doc reasons about. |

Two naming collisions must be resolved explicitly before adding anything: **`Dimension` vs `EmbeddingDimension`** (§3) and **`Version` vs `EmbeddingVersion`** (§4). Both are resolved in favor of *distinct concepts*, not renames-only.

---

## 1. Enhanced `EnrollmentEmbeddingResult`

The enhanced DTO stays an immutable `sealed record` with `init`-only members, matching the design conventions of §2/§4 of the baseline doc.

```csharp
namespace Abhyanvaya.Application.Enrollment;

using Abhyanvaya.Domain.Enums; // EmbeddingQuality

/// <summary>
/// Immutable output of <c>IEnrollmentEmbeddingService.GenerateAsync</c>: the normalized vector plus the
/// full provenance needed to (a) persist a StudentFaceEmbedding row, (b) later detect that this embedding
/// was produced by an outdated model/provider, and (c) support regression + re-embedding rollouts.
/// Wraps IEmbeddingGenerator output; adds no image/vector logging obligations of its own.
/// </summary>
public sealed record EnrollmentEmbeddingResult
{
    /// <summary>L2-normalized 512-d vector ready for cosine similarity (a dot product post-normalization).</summary>
    public required float[] NormalizedVector { get; init; }

    /// <summary>Provider's model name/deployment id (from IEmbeddingGenerator.ModelName), e.g. "w600k_r50".</summary>
    public required string Model { get; init; }

    /// <summary>Provider's own model/pipeline version string (from IEmbeddingGenerator.Version), e.g. "1.0".</summary>
    public required string Version { get; init; }

    /// <summary>Advisory quality bucket (reused Domain enum; mapped from the §Composite Quality Score).</summary>
    public required EmbeddingQuality Quality { get; init; }

    // ── Added by AI20.PHASE2.0.4 ─────────────────────────────────────────────

    /// <summary>ACTUAL length of <see cref="NormalizedVector"/> (NormalizedVector.Length). Replaces the old
    /// ambiguous <c>Dimension</c>; name aligned 1:1 with StudentFaceEmbedding.EmbeddingDimension. See §3.</summary>
    public required int EmbeddingDimension { get; init; }

    /// <summary>DECLARED/expected dimension from config for the active model (e.g. 512). When it differs from
    /// <see cref="EmbeddingDimension"/> the embedding is a dimension-drift reject, not a valid result. See §3.</summary>
    public required int ExpectedDimension { get; init; }

    /// <summary>Platform-owned, comparable schema/identity token for this embedding, e.g. "insightface-r50-v1.0".
    /// Distinct from the provider's raw <see cref="Version"/>; this is the value written to
    /// StudentFaceEmbedding.EmbeddingVersion and compared for re-embedding decisions. See §4 and §8.</summary>
    public required string EmbeddingVersion { get; init; }

    /// <summary>Which provider produced this vector (an <c>EmbeddingProviders</c> value, e.g. "InsightFace").
    /// Mirrors IEmbeddingGenerator.ProviderName. Gates cross-provider comparison. See §7.</summary>
    public required string EngineProvider { get; init; }

    /// <summary>Vector normalization applied before persistence, e.g. "L2". Together with EngineProvider it lets
    /// the matcher reject an incomparable pair rather than emit a garbage similarity score. See §7.</summary>
    public required string NormalizationMethod { get; init; }

    /// <summary>Content hash (SHA-256, hex) of the underlying model artifact (e.g. w600k_r50.onnx). Detects a
    /// silent model-binary swap even when Model/Version strings are unchanged. See §6 and §9.</summary>
    public required string ModelChecksum { get; init; }

    /// <summary>Wall-clock inference time for this single embedding, in milliseconds. Purely observational —
    /// never gates success; feeds latency-regression detection across deploys. See §9.</summary>
    public required double InferenceMilliseconds { get; init; }
}
```

All members are `required init` because a first-class enrollment embedding always knows its own provenance at generation time — there is no partially-populated valid result. (The single exception a reader might expect, `InferenceMilliseconds`, is always available: the service times its own call.) Backward-compatibility of this choice with the baseline `Dimension`/`Version` fields is analyzed in §3, §4 and §11.

---

## 2. Per-field rationale

| Field | Kind | Source of value | Maps to persistence | Why it exists |
|---|---|---|---|---|
| `NormalizedVector` | `float[]` | `EmbeddingNormalizer` over `IEmbeddingGenerator` output | `StudentFaceEmbedding.EmbeddingVector` | The embedding itself; L2-normalized so matching is a dot product. Unchanged. |
| `Model` | `string` | `IEmbeddingGenerator.ModelName` | `StudentFaceEmbedding.EmbeddingModel` | Provider's model identity for audit + persistence. Unchanged. |
| `Version` | `string` | `IEmbeddingGenerator.Version` | *(folded into `EmbeddingVersion`)* | The provider's *own* version string. Retained for audit/provenance; **not** the platform's comparison token (§4). |
| `Quality` | `EmbeddingQuality` | Composite quality score → bucket map | `StudentFaceEmbedding.EmbeddingQuality` | Advisory bucket for the UI + Bulk Regenerate threshold. Reused enum, unchanged. |
| `EmbeddingDimension` | `int` | `NormalizedVector.Length` | `StudentFaceEmbedding.EmbeddingDimension` | **Actual** vector length. Renamed from `Dimension` to match the persisted column name exactly (§3). |
| `ExpectedDimension` | `int` | Active-model config (mirrors `EmbeddingGenerationResult.ExpectedDimension`) | *(not persisted; validation input)* | **Declared** dimension. `EmbeddingDimension != ExpectedDimension` ⇒ drift reject (§3). |
| `EmbeddingVersion` | `string` | Composed by the embedding service (§8) | `StudentFaceEmbedding.EmbeddingVersion` | **Platform-owned** comparable schema token for re-embedding/migration decisions (§4, §6, §8). |
| `EngineProvider` | `string` | `IEmbeddingGenerator.ProviderName` (`EmbeddingProviders`) | *(encoded into `EmbeddingVersion`; dedicated column deferred)* | Identifies the producing provider; gates cross-provider comparison (§7). |
| `NormalizationMethod` | `string` | The normalization helper used | *(encoded into `EmbeddingVersion`; dedicated column deferred)* | Guards comparability; two providers "L2" is necessary-but-not-sufficient for comparison (§7). |
| `ModelChecksum` | `string` | SHA-256 of the ONNX artifact at load | *(observability; dedicated column deferred)* | Detects a silent binary swap under an unchanged version string (§6, §9). |
| `InferenceMilliseconds` | `double` | `Stopwatch` around the inference call | *(observability/metrics sink)* | Latency-regression baseline before/after a deploy (§9). Never gates success. |

> **Persistence note.** Only `EmbeddingModel`, `EmbeddingVersion`, `EmbeddingDimension`, and `EmbeddingQuality` map to existing `StudentFaceEmbedding` columns. `EngineProvider`, `NormalizationMethod`, `ModelChecksum`, and `InferenceMilliseconds` are **carried in the DTO but not persisted as new columns** in this milestone — adding columns is a schema change and out of scope. `EngineProvider` + `NormalizationMethod` are *encoded into* the `EmbeddingVersion` token (§8) so their essential meaning survives to the persisted row without a migration; `ModelChecksum`/`InferenceMilliseconds` flow to structured logs/metrics. A future DB milestone may promote any of these to first-class columns without changing this contract.

---

## 3. Resolving `Dimension` vs `EmbeddingDimension`

**These are two genuinely distinct concepts, not one field renamed.** The baseline §10 `Dimension` was ambiguous — it did not say whether it meant the length of the returned vector or the length the system expected. The real `EmbeddingGenerationResult` already hints at the distinction by carrying `ExpectedDimension`. This milestone splits the concept in two:

| Concept | Field | Definition | Failure semantics |
|---|---|---|---|
| **Actual** | `EmbeddingDimension` | `NormalizedVector.Length` as returned by the engine right now. | Purely descriptive; it is whatever the model produced. |
| **Declared** | `ExpectedDimension` | The dimension the active model is *configured* to produce (e.g. 512). | The gate: `EmbeddingDimension != ExpectedDimension` ⇒ the vector is a **dimension-drift reject**. |

**Decision:**
- The old `Dimension` is **renamed to `EmbeddingDimension`** (meaning: *actual*). This aligns the DTO name 1:1 with the persisted column `StudentFaceEmbedding.EmbeddingDimension`, eliminating a name-mapping mismatch the writer would otherwise have to bridge. Because the baseline `Dimension` is a *proposed* field that never shipped as code, the rename is a pure design clarification with zero runtime-compat cost (§11).
- `ExpectedDimension` is **added** as the distinct declared value.

**Why the drift check matters here.** The baseline §10 already says a "dimension mismatch (≠ expected 512) **throws**" and is classified transient `EmbeddingEngineFailed`. Carrying both fields on the result makes that check *data-driven and auditable* rather than a hardcoded literal: the embedding service compares `EmbeddingDimension` against `ExpectedDimension` (sourced from config, mirroring `EmbeddingGenerationResult.ExpectedDimension`) and only emits a success result when they match. A mismatch is a strong signal that the loaded model artifact is not the one the config expects — exactly the class of silent upgrade regression §9 is designed to catch.

---

## 4. Resolving `Version` vs `EmbeddingVersion`

**Also two distinct concepts.** The confusion is that both the real `IEmbeddingGenerator` and the persisted `StudentFaceEmbedding` use the word "version," but they answer different questions:

| Concept | Field | Owner of the string | Example | Answers |
|---|---|---|---|---|
| **Provider's model version** | `Version` | The provider (InsightFace, Azure, …) | `"1.0"` | "What does the provider call the model it ran?" |
| **Platform embedding schema/identity** | `EmbeddingVersion` | *Abhyanvaya* (this platform) | `"insightface-r50-v1.0"` | "Which of *our* embedding generations is this, for comparison/migration?" |

**Decision:**
- `Version` is **kept** and continues to mean *the provider's own version string* (straight from `IEmbeddingGenerator.Version`). It is provenance/audit data.
- `EmbeddingVersion` is **added** as a **platform-owned** composite token (scheme in §8). It is deliberately *independent* of what the provider calls its version, so that a provider silently bumping its internal version string, or two providers reusing "1.0", never confuses the platform's own migration logic.

**Reconciliation with the persisted column.** Today `StudentFaceEmbedding.EmbeddingVersion` is written from `EmbeddingGenerationResult.Version` (the raw provider string). This milestone proposes that, **for enrollment**, the value written to `StudentFaceEmbedding.EmbeddingVersion` becomes the *platform* `EmbeddingVersion` token. This is a refinement of the column's existing intent, not a contradiction: the column exists precisely to answer "was this embedding produced by an old model, and does it need regeneration?" — which the composite token answers robustly and the raw provider string answers poorly. The raw provider `Version` is not lost: it is embedded *inside* the composite token and also retained on the DTO for audit. The manual-upload path is unaffected and may continue writing the raw string; the two paths remain independently correct because the token is a superset the comparison logic can parse either way.

---

## 5. Metadata → `StudentFaceEmbedding` mapping (persistence reconciliation)

The result writer (`IEnrollmentResultWriter.WriteSuccessAsync`, §11 of the baseline) is the only component that maps this DTO onto the entity, keeping the mapping in one place.

| `EnrollmentEmbeddingResult` | `StudentFaceEmbedding` column | Notes |
|---|---|---|
| `NormalizedVector` | `EmbeddingVector` | Direct. |
| `EmbeddingDimension` | `EmbeddingDimension` | Direct; names now match (§3). |
| `Model` | `EmbeddingModel` | Direct. |
| `EmbeddingVersion` (composite) | `EmbeddingVersion` | Composite token replaces the raw provider string for enrollment (§4). |
| `Quality` | `EmbeddingQuality` | Direct; reused enum. |
| `Version`, `EngineProvider`, `NormalizationMethod`, `ModelChecksum`, `InferenceMilliseconds` | *(no column)* | `EngineProvider` + `NormalizationMethod` are encoded in the composite token; the rest go to logs/metrics. New columns are a deferred DB milestone. |

`PhotoVersion` on the entity is **not** the embedding service's concern — it is stamped by the writer from `Student.PhotoUploadedUtc` at persistence time, exactly as the manual path does. Nothing in this DTO duplicates or overrides it.

---

## 6. Future model upgrades and controlled re-embedding rollout

`ModelChecksum` + `EngineProvider` + `EmbeddingVersion` together let a future system answer "was this student's embedding generated by an old model?" **deterministically**, and drive the SuperAdmin **Bulk Regenerate** feature (`docs/AI20_ENROLLMENT_UI.md` §2, §5) safely.

**Detection.** At runtime the platform knows the *currently configured* embedding identity — call it `CurrentEmbeddingVersion` (e.g. `"insightface-r50-v1.1"`) and `CurrentModelChecksum`. For any stored embedding:

1. **Version comparison (primary).** `StudentFaceEmbedding.EmbeddingVersion != CurrentEmbeddingVersion` ⇒ the embedding predates the current model/pipeline and is a **regeneration candidate**. This is a cheap indexed string comparison over the persisted column — it scales to a whole tenant.
2. **Checksum comparison (defense-in-depth).** Even when the version *string* matches, `ModelChecksum != CurrentModelChecksum` reveals a **silent binary swap** (someone shipped a new `.onnx` under the same version label). Because `ModelChecksum` is not persisted as a column in this milestone, this stronger check runs against structured logs/metrics at deploy time (§9); the version comparison is the persisted, per-row signal.
3. **Provider comparison.** `EngineProvider` distinguishes "old model, same provider" from "different provider entirely" (§7) — the latter is never a safe in-place comparison and is always a regeneration candidate for a tenant standardizing on one provider.

**Controlled rollout.** Rather than a big-bang re-embed:
- The SuperAdmin dashboard surfaces a count of "stale" embeddings (version mismatch) per college/year scope.
- **Bulk Regenerate** enqueues those as a normal enrollment batch (`docs/AI20_ENROLLMENT_UI.md` §2 says it "retries all `Failed`/`RetryRequired` jobs"; the same batch machinery re-enrolls stale-version students), so the rollout inherits the existing batching, throttling (`AI20_ENROLLMENT_BACKGROUND.md` §3.5 semaphores), retry, and progress-reporting behavior for free.
- Because regeneration produces a new active `StudentFaceEmbedding` row superseding the prior one (the existing filtered-unique-index behavior in `EmbeddingStorage`), old and new coexist only momentarily and matching always uses the active row — no downtime, no half-migrated tenant.
- The rollout can be scoped and paced (per college, per year, per quality threshold) precisely because "staleness" is a queryable predicate over the persisted `EmbeddingVersion`.

---

## 7. Multiple embedding providers and cross-provider comparison

`EngineProvider` slots directly onto the existing `IEmbeddingProviderFactory` seam: the factory already resolves *one* `IEmbeddingGenerator` per provider name (`GetProvider(name)` / `GetDefaultProvider()`), and the provider vocabulary already exists (`EmbeddingProviders.InsightFace/FaceNet/AzureFace/OpenCV`). The enrollment embedding service records *which* resolved provider produced the vector into `EngineProvider`.

**Can embeddings from two different providers be compared with cosine similarity? No.** Different models learn different embedding spaces: the axes, the scale, and the very geometry differ. A cosine similarity between an InsightFace vector and a FaceNet vector is **not** a meaningful face-similarity — it is numerical noise that happens to fall in `[-1, 1]`. Even two providers that both "L2-normalize 512-d vectors" are not comparable, because normalization only fixes magnitude, not the learned coordinate system. This is why `NormalizationMethod` alone is insufficient and must be paired with `EngineProvider`.

**Recommendation (clear):**
> **Never mix providers within a single tenant's active embedding set.** All embeddings that will be matched against each other must share the same `EngineProvider` *and* `NormalizationMethod`. The matching pipeline should treat `EngineProvider` (and `NormalizationMethod`) as a **hard precondition**: if a probe embedding's provider/normalization does not match a gallery embedding's, the pair is **rejected as incomparable** and excluded from scoring, rather than silently producing a garbage similarity score. Switching a tenant to a new provider is a *full re-embed of that tenant* (via the §6 Bulk Regenerate rollout), after which the old-provider rows are superseded and no cross-provider comparison ever occurs.

This makes `EngineProvider` + `NormalizationMethod` a **safety interlock**, not just metadata: the failure mode of a mismatched comparison shifts from "silently wrong attendance" to "explicit refuse-to-compare," which is the correct behavior for a recognition system.

---

## 8. AI versioning scheme for `EmbeddingVersion`

**Proposed scheme (platform-owned, human-readable, comparable):**

```
{provider}-{modelFamily}-v{major}.{minor}
```

Examples:
- `insightface-r50-v1.0` — InsightFace ArcFace ResNet-50 (`w600k_r50.onnx`), the current v1 enrollment pipeline.
- `insightface-r50-v1.1` — same model artifact, but a pipeline change that affects the embedding *space* (e.g. a different alignment or normalization) — bump **minor**.
- `insightface-r100-v2.0` — a materially different model — bump **major**.
- `azureface-default-v1.0` — a hypothetical future Azure provider.

**Rules:**
- **Major** increments on any change that makes new vectors *incomparable* with old ones (different model, different embedding space, different dimension). A major bump ⇒ mandatory re-embed before mixing.
- **Minor** increments on changes that alter the vector but stay in a comparable-enough space to warrant regeneration for accuracy without a hard incompatibility (e.g. a re-tuned normalization). Policy still treats a minor mismatch as a regeneration candidate (§6), conservatively.
- `provider` and `modelFamily` are drawn from `EngineProvider`/`Model`, so the token *encodes* both — which is why §5 can drop dedicated provider/normalization columns without losing the information.

**How this differs from `Model`/`Version`:**
- `Model` and `Version` describe the **provider's own identity** for its model — strings the platform does not control and cannot assume are stable, unique, or monotonic (two providers can both say `Version = "1.0"`; a provider can rename its model between releases).
- `EmbeddingVersion` is the platform's **own abstraction version**, deliberately independent of the provider's naming. It is the single token the platform's migration/comparison logic trusts. The provider's `Version` is *an input* to constructing it, not a substitute for it.

The embedding service composes `EmbeddingVersion` deterministically from `(EngineProvider, Model, platform pipeline revision)`; the platform pipeline revision is a config constant that the platform bumps when *its* alignment/normalization changes even if the provider artifact did not.

---

## 9. Regression analysis after a model/dependency upgrade

`InferenceMilliseconds` + `ModelChecksum` + `EngineProvider` give two independent regression signals across a deploy — one for **performance**, one for **correctness/identity** — without changing any embedding math.

**Performance regression (latency).**
- Each enrollment emits `InferenceMilliseconds` to the metrics/log sink, tagged with `EngineProvider`, `Model`, `EmbeddingVersion`, and `ModelChecksum`.
- Comparing the **distribution** (p50/p95, not a single value) of `InferenceMilliseconds` before vs after a deploy detects a slowdown introduced by a new ONNX runtime, a new model artifact, or a hardware/thread-config change. Because the samples are tagged with `EmbeddingVersion`/`ModelChecksum`, a latency shift can be attributed to the exact artifact that changed rather than guessed at.
- This is observational only — a slow embedding is still a *correct* embedding, so `InferenceMilliseconds` never gates success (it is measured around the call, not asserted on).

**Correctness / identity regression (drift).**
- `ModelChecksum` is the ground truth of *which bytes ran*. If a deploy is supposed to be a no-op for the model but `ModelChecksum` changes, that is an unexpected artifact swap — flagged immediately at deploy time. Conversely, if an *intended* model upgrade ships but `ModelChecksum` did **not** change, the new artifact never actually deployed.
- Combined with the §3 `EmbeddingDimension`/`ExpectedDimension` gate, a checksum change that coincides with a dimension change is caught as a hard failure rather than silently persisting incompatible vectors.
- `EngineProvider` scopes all of the above per provider, so a multi-provider environment does not blend two providers' latency/checksum baselines into one meaningless aggregate.

**In short:** `InferenceMilliseconds` answers "did it get slower?", `ModelChecksum` answers "did the model actually change, and only when we intended?", and `EngineProvider` keeps both questions scoped to the right model — the minimum set needed to catch a bad upgrade before it degrades recognition for a whole tenant.

---

## 10. `EnrollmentSuccessWrite` reconciliation

The baseline §11 `EnrollmentSuccessWrite` **flattens** three embedding fields (`EmbeddingModel`, `EmbeddingVersion`, `EmbeddingQuality`) directly onto itself and separately carries the raw `NormalizedVector`. With the enhanced result this flattening now duplicates a subset of a richer DTO and would silently drop the new provenance (checksum, provider, normalization, timings, dimensions).

**Decision: stop flattening. Embed the whole `EnrollmentEmbeddingResult`.** The success write should carry the embedding as one composite reference, so the result writer maps *result → entity* in a single place and no field is lost or duplicated.

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Transactional success payload for IEnrollmentResultWriter.WriteSuccessAsync. Carries the storage
/// outcome, the validation-derived source metadata, and the FULL embedding result as one composite —
/// replacing the previously flattened EmbeddingModel/EmbeddingVersion/EmbeddingQuality fields so there
/// is exactly one source of truth for embedding provenance.
/// </summary>
public sealed record EnrollmentSuccessWrite
{
    public required EnrollmentItemContext Context { get; init; }

    // ── From IEnrollmentStorageService (unchanged) ──────────────────────────
    public required string PhotoKey { get; init; }
    public required string Checksum { get; init; }
    public required int ByteSize { get; init; }

    // ── From IEnrollmentValidationService (unchanged) ───────────────────────
    public required int SourceWidth { get; init; }
    public required int SourceHeight { get; init; }
    public required float QualityScore { get; init; }

    // ── From IEnrollmentEmbeddingService (was 3 flattened fields + vector) ──
    /// <summary>The complete embedding result. The writer maps its fields onto StudentFaceEmbedding
    /// (§5): NormalizedVector→EmbeddingVector, Model→EmbeddingModel, EmbeddingVersion→EmbeddingVersion,
    /// EmbeddingDimension→EmbeddingDimension, Quality→EmbeddingQuality; provenance extras go to logs.</summary>
    public required EnrollmentEmbeddingResult Embedding { get; init; }
}
```

**Why compose rather than keep flattening:**
- **Single source of truth.** The embedding's provenance lives in exactly one type; adding a future field (e.g. a persisted `ModelChecksum` column) requires no change to `EnrollmentSuccessWrite`.
- **No lossy hand-off.** Flattening only three of nine fields would discard `ModelChecksum`/`EngineProvider`/`NormalizationMethod`/`InferenceMilliseconds` at the write boundary — exactly the metadata §6/§9 depend on.
- **`QualityScore` vs `Quality` are intentionally both present.** `QualityScore` (`float`, from validation) is the raw composite; `Embedding.Quality` (`EmbeddingQuality`, bucketed) is the persisted enum. They are different types serving the UI's numeric sort vs the entity's bucket column — not a duplication, and both map to distinct destinations.

The writer's mapping is then a small, single-location translation, consistent with the §11 rule that the writer is the *only* place that touches the entity.

---

## 11. Backward-compatibility summary

Because the baseline `EnrollmentEmbeddingResult` and `EnrollmentSuccessWrite` are **proposed contracts that never shipped as code**, "backward compatibility" here means: compatible with the baseline design doc's intent and with the **real** persisted entity/enum vocabulary.

| Baseline element | Disposition | Justification |
|---|---|---|
| `Dimension` | **Renamed** → `EmbeddingDimension` (actual) | Matches the persisted column name; splits out `ExpectedDimension` (declared) as a distinct concept (§3). No runtime break — never shipped. |
| `Version` | **Kept** (provider's version) | Distinct from the added platform `EmbeddingVersion` (§4); retained for audit and as an input to the composite token. |
| `Model`, `Quality`, `NormalizedVector` | **Kept unchanged** | Map 1:1 to existing entity columns / reused enum. |
| Added fields | `ExpectedDimension`, `EmbeddingVersion`, `EngineProvider`, `NormalizationMethod`, `ModelChecksum`, `InferenceMilliseconds` | Provenance for upgrades/providers/versioning/regression; only the first two persist (into existing columns) — the rest are logs/metrics or encoded into the token, so **no schema change** is required (§2, §5). |
| `EnrollmentSuccessWrite` flattened embedding fields | **Replaced** by a composite `Embedding` reference | One source of truth; no lossy hand-off (§10). |

No existing embedding math, `IEmbeddingGenerator`, `EmbeddingNormalizer`, ONNX session, or `StudentFaceEmbedding` schema is modified by these proposals.

---

## Constraints Confirmed

- **No code was written or modified.** This milestone produced exactly one new markdown file, `docs/AI20_PHASE2_EMBEDDING_METADATA.md`. No `.cs`, `.csproj`, `.tsx`, `.ts`, or other non-markdown file was created or edited.
- **Embedding generation is untouched.** `InsightFaceEngine`, `IEmbeddingGenerator`, `EmbeddingNormalizer`, `IEmbeddingStorage`, and the ONNX model sessions are unchanged; no embedding math, threshold, or normalization behavior was altered.
- **Every C# block above is a PROPOSED contract enhancement**, rendered inside markdown, not a committed source file.
- **All designs reuse existing Domain/Application vocabulary** (`EmbeddingQuality`, `EmbeddingProviders`, `IEmbeddingProviderFactory`, `StudentFaceEmbedding` columns) without redefining or contradicting it; collisions (`Dimension`/`EmbeddingDimension`, `Version`/`EmbeddingVersion`) are resolved explicitly (§3, §4).
- **No business logic, HTTP endpoint, background worker, DI registration, database migration, or column addition** was implemented or required.
- Deliverable produced: `docs/AI20_PHASE2_EMBEDDING_METADATA.md`.
