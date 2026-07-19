# AI20.PHASE2.0.11 — Validation Report Model

**Type:** Contract/model design only. No production code was written or modified to produce this document. No business logic, endpoint, worker, DB update, or image processing is implemented here — only a **proposed** immutable model (`ValidationReport`) and its relationship to the already-proposed `EnrollmentValidationResult` from `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §8.

**Status of the code blocks below:** every C# block is a **proposed** record that does not yet exist in the codebase. It is the design artifact of this milestone, not committed code. It grounds itself entirely in the validation dimensions already specified in `docs/AI20_ENROLLMENT_ENGINE.md` §3.1–3.6 and its "Composite Quality Score" section, and maps onto the existing `Abhyanvaya.Domain/Enums/FailureCategory.cs` vocabulary without contradicting it.

---

## 0. Context and scope

The baseline validation contract (`IEnrollmentValidationService` / `EnrollmentValidationResult`, `docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §8) returns a deliberately flat shape:

```csharp
// EXISTING PROPOSED baseline (AI20.PHASE2.0.1 §8) — reproduced for reference, unchanged here.
public sealed record EnrollmentValidationResult
{
    public required bool IsValid { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? Reason { get; init; }
    public byte[]? AlignedFaceBytes { get; init; }
    public float? QualityScore { get; init; }   // advisory composite (§ Composite Quality Score)
    public int? SourceWidth { get; init; }
    public int? SourceHeight { get; init; }
}
```

That shape answers **"did it pass, and if not, under which single category?"** plus one advisory `QualityScore` and the source dimensions. It does **not** expose the individual per-dimension measurements the engine already computes internally (`docs/AI20_ENROLLMENT_ENGINE.md` §3.1–3.4) on the way to that pass/fail decision. This milestone reviews whether a richer, structured `ValidationReport` should carry every dimension's own measurement, and — concluding **yes** — designs it.

The design obeys the same Phase-1 conventions as every other Phase-2 contract (`docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §0): `sealed record`, `required`/`init` members, lives in `Abhyanvaya.Application.Enrollment`, references only Domain enums, throws for nothing an operator can reasonably expect.

---

## 1. Why the flat result is insufficient

| Question a SuperAdmin / analytics view will ask | Answerable from the flat result today? |
|---|---|
| "Did this photo pass?" | Yes (`IsValid`). |
| "If it failed, which gate tripped first?" | Yes (`FailureCategory`). |
| "*By how much* did it fail — was the blur score `42` against a threshold of `100`, or `98` (a near miss)?" | **No.** Only the enum, no measured value. |
| "This photo passed but is marginal — what is its pose deviation vs. its sharpness?" | **No.** Only the single composite `QualityScore`, not its components. |
| "Across this whole batch, what is the distribution of blur scores among the `BlurRejected` failures — are they all just-below-threshold (source photos are low-quality scans) or catastrophically blurry?" | **No.** No per-dimension value is retained anywhere. |
| "What threshold *should* we ship? Give me the real distribution of the dimension, not a guess." | **No** — and `docs/AI20_ENROLLMENT_ENGINE.md` §3.3 explicitly forbids hardcoding a blur threshold "without validating against real exambranch.com-style photos first." The flat result never captures the data that validation would require. |

The engine already *computes* detection confidence, bounding-box size, variance-of-Laplacian sharpness, and a coarse pose estimate to reach its verdict (§3.1–3.4). Discarding those numbers down to one enum + one composite float throws away exactly the diagnostic and calibration data the enrollment docs say we will need. `ValidationReport` retains them.

---

## 2. Dimensions to capture (grounded in `docs/AI20_ENROLLMENT_ENGINE.md`)

Every dimension below already has a home in the engine design; the report only *surfaces* what the validator already measures.

| Dimension | Source in `docs/AI20_ENROLLMENT_ENGINE.md` | What it measures | Unit / range |
|---|---|---|---|
| **FaceCount** | §3.1 Exactly One Face / §3.6 | `candidates.Count` from the existing `DetectFaces` | int (0, 1, or ≥2) |
| **Source resolution** | §3.2 (source-image floor, ≥640×480) | whole decoded image width/height | pixels |
| **Face-crop resolution** | §3.2 (face-crop floor, ≥112×112) | detected bounding-box width/height (`DetectedFaceDto.BoundingBoxWidth/Height`) | pixels |
| **Face size ratio** | §"Composite Quality Score" (`FaceBoundingBoxArea / ImageArea`) | how much of the frame the face occupies — **distinct from raw resolution** (see §4) | 0..1 |
| **Blur (sharpness)** | §3.3 (variance-of-Laplacian on the aligned face crop) | edge energy; low ⇒ out of focus | float ≥ 0 (unbounded) |
| **Pose** | §3.4 (yaw/pitch/roll from the 5-point SCRFD landmarks) | head orientation | degrees (per axis) + aggregate deviation |
| **Brightness** | new (advisory; consistent with §3.3's "compute on the face crop") | mean luma of the aligned face crop | 0..1 |
| **Contrast** | new (advisory) | std-dev of luma of the aligned face crop | 0..1 |
| **Confidence** | detector score (`DetectedFaceDto.DetectionScore`, threshold 0.5) | the **detector's own** score for the selected face | 0..1 |
| **Eyes visible** | §3.5 (Eyes Open / No Sunglasses) | reserved — **deferred to v2** per §3.5; see §5 | bool? (null in v1) |
| **Occlusion** | not designed anywhere — sketched here (§5) | fraction of the face region covered by a detected obstruction | 0..1 (null in v1) |
| **Composite score** | §"Composite Quality Score" | advisory weighted blend of the above (see §6) — the existing `QualityScore` | 0..1 |

---

## 3. Confidence vs. Composite Score — resolved

These two are frequently conflated; the report keeps them as **separate fields with separate meanings**:

- **`DetectionConfidence`** is the **detector's own** score for the selected face — the raw `DetectedFaceDto.DetectionScore` produced by the SCRFD detection session (`det_10g.onnx`), the same value already gated by `DetectionThreshold = 0.5` for classroom recognition. It answers *"how sure is the model that this region is a face?"* It is a single-model, single-dimension output. **We do not compute it — the detector hands it to us.**

- **`CompositeScore`** is **our** advisory blend across multiple dimensions — detection confidence **×** face-size ratio **×** sharpness **×** pose frontality (§6). It answers *"overall, how good an enrollment photo is this?"* It is the existing `QualityScore` from §"Composite Quality Score", never causes rejection by itself, and drives the `EmbeddingQuality` bucket mapping (`>0.85 → Excellent`, etc.).

**Stated clearly:** Confidence is the detector's own single number; CompositeScore is the multi-dimension score we synthesize (and Confidence is *one of its four inputs*). They are never the same value, and the report exposes both so a SuperAdmin can distinguish "the detector was unsure it was even a face" (low `DetectionConfidence`) from "it's clearly a face but a poor-quality one" (high `DetectionConfidence`, low `CompositeScore`).

---

## 4. Face Size vs. Resolution — the distinction, explicitly

`docs/AI20_ENROLLMENT_ENGINE.md` §3.2 already implies two *different* pixel checks; the report keeps three related-but-distinct fields so they are never confused:

| Field | What it is | Why it is independent |
|---|---|---|
| **Source resolution** (`SourceWidth`/`SourceHeight`) | absolute pixel dimensions of the *whole* downloaded image | A 4000×3000 image passes the source floor even if the face is tiny within it. |
| **Face-crop resolution** (`FaceCropWidth`/`FaceCropHeight`) | absolute pixel dimensions of the *detected bounding box* | The network up-samples any crop below 112×112 (`RecognitionInputSize`), degrading the embedding — an *absolute* floor. |
| **Face size ratio** (`FaceSizeRatio`) | `FaceBoundingBoxArea / SourceImageArea` — a **relative** proportion | A face occupying 2% of a huge frame is a *distant* subject even if the crop technically clears 112×112. Ratio catches "face too small *relative to* the frame," which absolute resolution alone cannot. |

Concrete case both floors miss but the ratio catches: a 4000×3000 source (passes source floor) with a 120×150 face crop (passes the 112×112 crop floor) has a ratio of `≈0.0015` — a speck-in-the-corner subject that makes a poor reference photo. `FaceSizeRatio` is precisely the `FaceBoundingBoxArea / ImageArea` term already named in the §"Composite Quality Score" formula, surfaced as its own field rather than only living inside the composite.

---

## 5. The `ValidationReport` record

Immutable, init-only `sealed record`, matching the codebase's established style (`docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §0.4). One report is produced for **every** validation attempt — pass or fail — so it is a `required` non-null member of the result (see §9).

```csharp
namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// A structured, per-dimension breakdown of one enrollment-photo validation attempt. Carries every
/// dimension the validator measured on the way to its verdict (docs/AI20_ENROLLMENT_ENGINE.md §3.1-3.4
/// + "Composite Quality Score"), not just the final pass/fail + composite. Produced on EVERY attempt,
/// including rejections, so it doubles as the diagnostic surface for the SuperAdmin Failure/Student-Detail
/// screens (docs/AI20_ENROLLMENT_UI.md §4-5) and as the calibration data source §3.3 says we must collect
/// before hardcoding thresholds. Immutable; contains no image bytes (those stay on EnrollmentValidationResult).
///
/// Field tiers (see §7):
///   Tier 1 (source-level)   — always present once the image DECODES.
///   Tier 2 (detector-level) — present once >=1 face is DETECTED.
///   Tier 3 (face-level)     — present only for the single APPROVED-shape candidate (FaceCount == 1).
/// </summary>
public sealed record ValidationReport
{
    // ---- Tier 1: source-level (always measurable once the image decodes) ----

    /// <summary>Number of faces the detector returned. 0 = none detected OR detection was skipped by the
    /// pre-detection source-resolution short-circuit (disambiguate via SourceWidth/Height vs the floor).</summary>
    public required int FaceCount { get; init; }

    /// <summary>Whole-image width in pixels. Null only when the bytes never decoded (InvalidImage/CorruptImage).</summary>
    public int? SourceWidth { get; init; }

    /// <summary>Whole-image height in pixels. Null only when the bytes never decoded (InvalidImage/CorruptImage).</summary>
    public int? SourceHeight { get; init; }

    // ---- Tier 2: detector-level (present once >=1 face is detected) ----

    /// <summary>The DETECTOR's own score (DetectedFaceDto.DetectionScore) for the selected/top face.
    /// Distinct from CompositeScore (§3). Null when zero faces were detected.</summary>
    public float? DetectionConfidence { get; init; }

    // ---- Tier 3: face-level (present only when exactly one face was detected) ----

    /// <summary>Detected bounding-box width in px (absolute face-crop resolution, §3.2 face-crop floor).</summary>
    public int? FaceCropWidth { get; init; }

    /// <summary>Detected bounding-box height in px (absolute face-crop resolution, §3.2 face-crop floor).</summary>
    public int? FaceCropHeight { get; init; }

    /// <summary>FaceBoundingBoxArea / SourceImageArea, 0..1 (RELATIVE face size, distinct from resolution — §4).</summary>
    public float? FaceSizeRatio { get; init; }

    /// <summary>Variance-of-Laplacian sharpness on the aligned face crop (§3.3). Higher = sharper. Unbounded &gt;= 0.</summary>
    public float? BlurScore { get; init; }

    /// <summary>Coarse yaw/pitch/roll estimate from the 5-point landmarks (§3.4). Null when no single face.</summary>
    public PoseEstimate? Pose { get; init; }

    /// <summary>Mean luma of the aligned face crop, normalized 0..1. Advisory.</summary>
    public float? Brightness { get; init; }

    /// <summary>Std-dev of luma of the aligned face crop, normalized 0..1. Advisory.</summary>
    public float? Contrast { get; init; }

    /// <summary>Eyes-open / no-sunglasses (§3.5). RESERVED — deferred to v2; always null in v1.
    /// When implemented it follows Tier-3 population rules.</summary>
    public bool? EyesVisible { get; init; }

    /// <summary>Fraction of the face region covered by a detected obstruction (mask/hand/hair/sticker), 0..1.
    /// RESERVED — not yet designed; always null in v1 (see §5 sketch). Follows Tier-3 rules once implemented.</summary>
    public float? OcclusionCoverage { get; init; }

    /// <summary>The advisory 0..1 composite (§6, = the existing EnrollmentValidationResult.QualityScore).
    /// Null whenever any of its Tier-3 inputs is null (i.e. no single approved-shape face).</summary>
    public float? CompositeScore { get; init; }
}

/// <summary>Coarse geometric head-pose estimate derived from the existing 5-point SCRFD landmarks (§3.4).
/// Not a new ML model — a heuristic over landmark geometry. Angles in degrees.</summary>
public sealed record PoseEstimate
{
    /// <summary>Left/right head turn, degrees. 0 = frontal.</summary>
    public required float Yaw { get; init; }

    /// <summary>Up/down head tilt, degrees. 0 = frontal.</summary>
    public required float Pitch { get; init; }

    /// <summary>In-plane rotation (eye-line vs horizontal), degrees. 0 = level.</summary>
    public required float Roll { get; init; }

    /// <summary>Aggregate normalized deviation-from-frontal (0 = perfectly frontal), the single value that
    /// feeds the (1 - normalize(PoseDeviation)) term of the composite (§6).</summary>
    public float Deviation { get; init; }
}
```

**Occlusion sketch (reserved, not implemented in v1).** No occlusion detector exists in the codebase today (consistent with §3.5's finding that no sunglasses/eye-state building block exists). When designed, `OcclusionCoverage` would report the fraction (0..1) of the face bounding region estimated to be covered by an obstruction — e.g. a mask, hand, hair, or ID-card overlay — most cheaply as a coverage heuristic over the face region (skin-vs-non-skin or a small segmentation model), and, like `EyesVisible`, would be advisory input rather than a hard gate initially. It is included as a nullable field now so the model is forward-compatible and no schema/contract churn is needed when it lands.

---

## 6. Composite Score — how it is computed from the dimensions

`CompositeScore` is **not** a new number; it *is* the existing advisory `QualityScore` from `docs/AI20_ENROLLMENT_ENGINE.md` §"Composite Quality Score", now shown as an explicit function of the report's own fields:

```
CompositeScore = w1 * normalize(DetectionConfidence)     // Tier-2 detector score
               + w2 * normalize(FaceSizeRatio)           // larger, centered face = better
               + w3 * normalize(BlurScore)               // sharper (higher Laplacian variance) = better
               + w4 * (1 - normalize(Pose.Deviation))    // more frontal = better
```

- The four terms map **one-to-one** onto the four factors §"Composite Quality Score" already lists (`detection × resolution/size × sharpness × pose`).
- `w1..w4` are advisory weights sourced from an options class (never hardcoded, per §3.3's calibration warning); an illustrative starting split is `0.40 / 0.20 / 0.25 / 0.15`, to be tuned against the real per-dimension distributions the report itself collects (§11).
- Because every term is a Tier-3 face-level measurement, `CompositeScore` is populated **only** when a single face was detected — i.e. it is null in exactly the cases where `FaceCount != 1` (zero-face, multi-face, source-floor short-circuit, decode failure). This is consistent with §"Composite Quality Score" computing the score only "once a candidate passes all hard-reject gates" (or at least once a single candidate exists to measure).

---

## 7. Required vs. optional fields — the three tiers

The determining principle is **"at what point in the pipeline does the value become measurable?"**, combined with a deliberate **measurement-completeness** rule.

**Measurement-completeness rule (design decision):** once a single face has been detected, the validator computes the **entire** Tier-3 battery (blur, pose, brightness, contrast, size ratio, composite) **even if an earlier/other gate already decided to reject**. It does *not* stop measuring at the first failed gate. `FailureCategory` records which gate tripped first (by the §3 priority order), but every measurable dimension is still populated. This is what makes the report a complete calibration dataset (§11) rather than a truncated one. The single exception is the **source-resolution floor**, which §3.2 states is checked *before* any ONNX inference to avoid wasting the detector on a too-small image — so it is the one gate that legitimately short-circuits before Tier-2/Tier-3 are measured.

| Tier | Fields | When populated | Nullable? |
|---|---|---|---|
| **Tier 1 — source** | `FaceCount` | as soon as detection runs (or `0` if detection skipped) | `FaceCount` **required** (non-null int) |
| | `SourceWidth`, `SourceHeight` | as soon as the image **decodes** | null **only** when bytes never decode |
| **Tier 2 — detector** | `DetectionConfidence` | once **≥1** face is detected | null when `FaceCount == 0` |
| **Tier 3 — face** | `FaceCropWidth/Height`, `FaceSizeRatio`, `BlurScore`, `Pose`, `Brightness`, `Contrast`, `CompositeScore` | only when **exactly one** face is detected (single approved-shape candidate) | null whenever `FaceCount != 1` |
| **Tier 3 — reserved** | `EyesVisible`, `OcclusionCoverage` | deferred to v2 (§3.5 / §5) | **always null in v1** |

So the genuinely **mandatory** (always non-null on any returned result) field is `FaceCount`. `SourceWidth`/`SourceHeight` are *effectively required* — present on every attempt whose bytes decoded, i.e. every case except `InvalidImage`/`CorruptImage`. Everything else is **conditionally populated** and therefore nullable, precisely because there is nothing to measure it *on* until a face exists.

---

## 8. Null-under-which-failure-category

Columns are the outcomes a returned `EnrollmentValidationResult` can carry (engine exceptions such as `EmbeddingEngineFailed` **throw** and never produce a report, per §8 of the contracts doc, so they are not columns here). `LowResolutionRejected` is split into its two §3.2 sub-cases because they null different fields. Legend: **✓** populated · **○** populated by the measurement-completeness rule (§7) despite a *different* gate causing the rejection · **—** null · **null(v1)** reserved, always null this version.

| Field | Success (`IsValid`) | `NoFaceDetected` | `MultipleFacesDetected` | `LowResolutionRejected` (source floor) | `LowResolutionRejected` (face-crop floor) | `BlurRejected` | `InvalidImage` / `CorruptImage` |
|---|---|---|---|---|---|---|---|
| `FaceCount` | `1` | `0` | `≥2` | `0` (detection skipped) | `1` | `1` | `0` (never detected) |
| `SourceWidth` / `SourceHeight` | ✓ | ✓ | ✓ | ✓ (below floor) | ✓ | ✓ | — (decode failed) |
| `DetectionConfidence` | ✓ | — | ✓ (top candidate) | — | ✓ | ✓ | — |
| `FaceCropWidth` / `FaceCropHeight` | ✓ | — | — | — | ✓ (below floor) | ✓ | — |
| `FaceSizeRatio` | ✓ | — | — | — | ✓ | ○ | — |
| `BlurScore` | ✓ | — | — | — | ○ | ✓ (below threshold) | — |
| `Pose` | ✓ | — | — | — | ○ | ○ | — |
| `Brightness` / `Contrast` | ✓ | — | — | — | ○ | ○ | — |
| `EyesVisible` | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) |
| `OcclusionCoverage` | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) | null(v1) |
| `CompositeScore` | ✓ | — | — | — | ○ | ○ | — |

Key reads of this table:

- **Zero-face case (`NoFaceDetected`)** nulls the entire Tier-2/Tier-3 battery — there is literally no face to measure pose/blur/brightness/occlusion *on*, exactly as the task requires. Source dimensions remain, so you can still tell whether the image itself was large or small.
- **`MultipleFacesDetected`** keeps `DetectionConfidence` (the top candidate's score, useful to know whether it was two clear faces or one face + a faint false positive) but nulls all single-candidate Tier-3 fields, because §3.1 rejects *before* selecting one canonical face.
- **`LowResolutionRejected` (source floor)** is the sole pre-detection short-circuit (§3.2), so it nulls Tier-2/Tier-3 entirely; `FaceCount == 0` here means *"detection skipped,"* distinguished from a true `NoFaceDetected` by the source dimensions sitting below the floor.
- **`LowResolutionRejected` (face-crop floor)** and **`BlurRejected`** both had a single detected face, so the measurement-completeness rule (§7) fills the whole Tier-3 battery (the ○ cells) — that is the calibration payload of §11.
- **`InvalidImage`/`CorruptImage`** produce a minimal report — `FaceCount = 0`, everything else null — because the bytes never decoded into an image at all.

---

## 9. Relationship to `EnrollmentValidationResult`

**Recommendation: `ValidationReport` is a NEW `required` non-null field on `EnrollmentValidationResult` (`Report`), and the existing flat `QualityScore` / `SourceWidth` / `SourceHeight` become thin, non-breaking *forwarding accessors* that read from `Report` (rather than being removed outright).**

Proposed shape (illustrative — a superset of the §8 baseline, not a rewrite of it):

```csharp
namespace Abhyanvaya.Application.Common.Interfaces;

public sealed record EnrollmentValidationResult
{
    public required bool IsValid { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? Reason { get; init; }
    public byte[]? AlignedFaceBytes { get; init; }

    /// <summary>Structured per-dimension breakdown of THIS attempt. Present on EVERY returned result
    /// (pass or fail); see AI20_PHASE2_VALIDATION_REPORT.md. Authoritative source for the values below.</summary>
    public required ValidationReport Report { get; init; }

    // ---- Backward-compatible forwarders over Report (baseline AI20.PHASE2.0.1 §8 shape) ----

    /// <summary>Advisory composite. Forwards Report.CompositeScore. Kept for baseline-contract compatibility.</summary>
    public float? QualityScore => Report.CompositeScore;

    /// <summary>Forwards Report.SourceWidth. Kept for baseline-contract compatibility.</summary>
    public int? SourceWidth => Report.SourceWidth;

    /// <summary>Forwards Report.SourceHeight. Kept for baseline-contract compatibility.</summary>
    public int? SourceHeight => Report.SourceHeight;
}
```

**Why "new field" and not "flat only," and why forwarders rather than deletion:**

1. **`ValidationReport` is the single source of truth; duplication is eliminated, not added.** `QualityScore`, `SourceWidth`, `SourceHeight` are *computed* from `Report`, so there is exactly one place each value lives. The report is not a parallel copy of the flat fields — the flat fields become views onto the report.
2. **Backward compatibility with the AI20.PHASE2.0.1 baseline.** Downstream contracts already reference the flat names — most concretely `EnrollmentSuccessWrite` (contracts doc §11) requires `SourceWidth`, `SourceHeight`, and `QualityScore`. Keeping them as forwarders means the orchestrator and result-writer designs from PHASE2.0.1 keep compiling against the same member names, so this milestone *extends* the baseline rather than breaking it. (Both artifacts are still only proposed contracts, so there is freedom here; choosing the non-breaking superset keeps the two documents consistent instead of forcing a retroactive edit to §8/§11.)
3. **`required` non-null `Report` enforces the invariant "every validation attempt yields a full report."** Because a report is produced even on `NoFaceDetected` and `InvalidImage` (§8 table), making it non-null is honest and means no caller has to null-check the report itself — only its tiered inner fields, whose nullability already encodes "was this measurable?"
4. **It keeps image bytes off the report.** `AlignedFaceBytes` stays on `EnrollmentValidationResult` (it is the pipeline payload consumed by storage/embedding); `ValidationReport` carries only *measurements* — safe to log, persist, and aggregate for analytics without ever handling image data (consistent with the "never log image bytes" rule throughout Phase 2).

A future clean-up could mark the three forwarders `[Obsolete]` once all callers read `Report.*` directly, but that is out of scope here and not required for this milestone.

---

## 10. Future UI usage

A per-dimension report upgrades the SuperAdmin screens already sketched in `docs/AI20_ENROLLMENT_UI.md` from *"what category failed"* to *"what actually happened, and is it worth a retry."*

- **Failure Screen (`docs/AI20_ENROLLMENT_UI.md` §4).** Today the Reason column shows only the enum, e.g. `Blur Rejected`. With the report, that row can expand to the measured truth:
  - `Blur Rejected — score 42 (threshold 100): image was noticeably out of focus` → clearly not worth retrying the same photo; needs a better source scan.
  - `Blur Rejected — score 94 (threshold 100): borderline` → a near-miss; a SuperAdmin might reasonably lower the threshold or manually override rather than chase a new photo.
  - `Low Resolution — face crop 96×96 (floor 112×112)` vs `source 512×384 (floor 640×480)` → tells the admin *which* resolution problem it is (tiny face vs tiny image), which points at different fixes.
  This directly serves the §4 "let a SuperAdmin isolate a systemic source-data gap vs. a handful needing the source photo corrected" goal, but with numbers instead of guesswork.
- **Student Detail Screen (`docs/AI20_ENROLLMENT_UI.md` §5).** The per-attempt block that today shows `Quality Score: 0.81` can render the full breakdown behind it — `Confidence 0.97 · Sharpness 210 · Face size 0.34 · Pose dev 6° → Composite 0.81` — so an admin judging whether to `Regenerate` sees *why* a passed-but-marginal enrollment is marginal (e.g. good sharpness but an off-center small face), informing whether a manual re-upload would actually help.
- **Failure breakdown strip (§4).** The existing "Photo Not Found 3 · Multiple Faces 2 · Blur 1" strip can gain a severity read — e.g. "Blur 1 (near-miss)" — turning a flat count into an actionable signal.

Crucially, the report changes *nothing structurally* about these screens — it enriches columns/expanders that §4–§5 already describe, exactly as those screens are "a UI rendering of existing data."

---

## 11. Future AI explainability and calibration

Carrying every dimension (not just pass/fail) is what makes the enrollment gate **explainable** and **tunable** rather than a black box:

- **"Why did *this* photo fail?" answered fully.** Instead of one enum, the report gives a complete diagnostic vector for the exact attempt — the tripped gate *plus* every other dimension's value — so support can explain a rejection precisely and spot secondary problems (e.g. a `BlurRejected` photo that was *also* badly under-lit, visible via `Brightness`), even though only the first gate is named in `FailureCategory`.
- **Batch/college-level failure analytics.** Because a report exists for every attempt (including failures), an analytics view can correlate *which dimensions most commonly drive rejections across a whole batch or college* — e.g. *"73% of this batch's failures were `BlurRejected` with scores just below threshold → the source photos are likely low-quality scans"* vs. *"failures are spread evenly across categories → no single systemic cause."* That is impossible with only an enum + composite; it requires the retained per-dimension scores.
- **Threshold calibration backed by real distributions — the §3.3 mandate.** `docs/AI20_ENROLLMENT_ENGINE.md` §3.3 explicitly warns the blur threshold "must be calibrated against a sample of real ID photos before shipping — do not hardcode a threshold without validating first," because variance-of-Laplacian is scale- and content-sensitive. **A `ValidationReport` with real per-dimension scores *is* the calibration dataset that warning implies you must collect.** Persisting reports across a pilot batch yields the actual distribution of `BlurScore` (and `FaceSizeRatio`, `Pose.Deviation`, `DetectionConfidence`) over genuine exambranch.com-style photos, so the shipped thresholds and composite weights (§6) are set from data rather than guesswork — and can be re-tuned later against fresh distributions without code changes (they live in an options class).
- **Composite-weight tuning.** The same retained data lets the `w1..w4` composite weights (§6) be validated against outcomes (do the photos scored "Poor" actually recognize worse in classroom attendance?), closing the loop between the advisory score and real recognition accuracy.

In short: the flat result can only ever say *"it failed, category X."* The report can say *"it failed category X at value V against threshold T, here is every other dimension, and here is the distribution across the batch"* — the difference between an opaque gate and one you can explain, audit, and calibrate.

---

## Constraints Confirmed

- No production code was written or modified. Every C# block above (`ValidationReport`, `PoseEstimate`, the extended `EnrollmentValidationResult`) is a **proposed** model, not a committed file.
- No business logic, HTTP endpoint, background worker, DI registration, database update, migration, or image processing was implemented.
- No `.cs`, `.csproj`, `.tsx`, `.ts`, or any non-markdown file was created or edited anywhere in the repository.
- The only artifact produced is this single new markdown file, `docs/AI20_PHASE2_VALIDATION_REPORT.md`.
- The design reuses the existing Domain `FailureCategory` vocabulary and the existing `DetectedFaceDto` detection data without changing them, and grounds every dimension in `docs/AI20_ENROLLMENT_ENGINE.md` §3.1–3.6 and its "Composite Quality Score" section rather than inventing a parallel design.
- Relationship to the AI20.PHASE2.0.1 baseline (`docs/AI20_PHASE2_ENGINE_CONTRACTS.md` §8) is additive and backward-compatible: `ValidationReport` is a new `required` field on `EnrollmentValidationResult`, with `QualityScore`/`SourceWidth`/`SourceHeight` retained as forwarding accessors.
