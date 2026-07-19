# AI20.ENROLLMENT.5 — AI Enrollment Engine

**Type:** Design only. No production code was written or modified to produce this document.

---

## 1. Review of `InsightFaceEngine`

**File:** `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs`

The engine already exposes two distinct paths:

| Method | Used by | Behavior |
|---|---|---|
| `DetectAsync` (via `IFaceDetectionService`) | Classroom recognition (`ClassroomRecognitionPipeline`) | Detects **all** faces above `DetectionThreshold` (0.5) in a classroom photo — `maxFaces = request.MaxFaces ?? int.MaxValue`, no upper bound by default |
| `GenerateSingleFaceEmbedding(byte[] imageBytes, ...)` | Manual student photo upload (`InsightFaceEmbeddingGenerator` → `EmbeddingPipeline`) | Detects faces, then **picks the single highest-`Score` candidate** and embeds it — does **not** check that exactly one face was found |

```176:188:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
public float[] GenerateSingleFaceEmbedding(byte[] imageBytes, CancellationToken cancellationToken = default)
{
    using var image = Image.Load<Rgb24>(imageBytes);
    var candidates = DetectFaces(image);
    if (candidates.Count == 0)
    {
        throw new InvalidOperationException("No face detected in the student photo.");
    }

    var best = candidates.OrderByDescending(c => c.Score).First();
    using var aligned = InsightFaceImageMath.AlignFace(image, best.Landmarks, _options.RecognitionInputSize);
    return ExtractEmbedding(aligned);
}
```

Both paths share the same singleton `InsightFaceOnnxModelHost` (`det_10g.onnx` detection session, `w600k_r50.onnx` recognition session, both cached and reused, unmodified by this milestone).

---

## 2. How Enrollment Differs From Attendance Recognition

| Dimension | Classroom Attendance Recognition | AI Enrollment |
|---|---|---|
| **Input image** | A crowd photo — many faces expected, variable lighting/angle, teacher-controlled | A single reference photo per student, sourced from an external ID-photo system (exambranch.com-style), expected to be a controlled headshot |
| **Face count expectation** | Many (0 to dozens) — the whole point is finding *all* recognizable faces | **Exactly one.** Zero or multiple faces both indicate a bad source photo and must be rejected, not best-effort-handled |
| **Cost of a bad result** | Low-medium — a low-confidence/unmatched face becomes a manual review item in the Recognition Review UI; a human always double-checks before attendance is finalized | **High.** The embedding produced here becomes the **canonical reference vector** compared against for every future classroom recognition of that student. A bad enrollment embedding (wrong crop, blurry, extreme angle) silently degrades that student's attendance accuracy for as long as it stays `IsActive` — there is no per-classroom-photo human review to catch it downstream |
| **Existing quality gate** | None beyond `DetectionThreshold` (0.5) — acceptable, because a human reviews every result | Also **none today** (`GenerateSingleFaceEmbedding` has no exactly-one-face check, no blur/pose/resolution gate) — **not acceptable** for a value that becomes the permanent ground truth, so this milestone must add one |
| **Failure handling** | Silent "Unknown"/"LowConfidence" status, reviewed by a teacher | Must be an explicit, categorized rejection (`docs/AI20_ENROLLMENT_DATABASE.md`'s `FailureCategory`) that a SuperAdmin can act on (fix the source photo, retry) |

**Conclusion:** the enrollment engine reuses the exact same ONNX sessions and embedding math, but wraps them in a **strictly stricter, reject-by-default validation layer** that does not exist for classroom recognition today, because the two use cases have fundamentally different risk profiles for a low-quality result.

---

## 3. Validation Design

A new `IEnrollmentFaceValidator` runs **after** detection and **before** embedding. It receives all detected candidates (not just the best one) and the decoded image, and returns either a single approved candidate or a categorized rejection.

### 3.1 Exactly One Face

```
candidates = DetectFaces(image)   // existing InsightFaceEngine.DetectFaces, unmodified

if candidates.Count == 0:
    reject(FailureCategory.NoFaceDetected)
elif candidates.Count > 1:
    reject(FailureCategory.MultipleFacesDetected)
else:
    proceed with candidates[0]
```

This is the single most important, highest-priority new check — it directly closes the gap explicitly confirmed absent in `GenerateSingleFaceEmbedding` today (best-score-wins, no count assertion). Unlike classroom recognition (where "no cap" is correct), enrollment must be a hard `Count == 1` gate. A photo with two people in it (a common failure mode for ID-photo databases — e.g., a photographer visible in the background) must be rejected, not silently embedded from whichever face scored higher.

*(Design note: a `MaxFaces`/threshold parameter already exists on the detection request per the classroom path — the enrollment validator does not need engine changes to enforce this, it only needs to inspect `candidates.Count` on the result the existing `DetectFaces` already returns, i.e., this is a wrapper-level check, not a new engine capability.)*

### 3.2 Minimum Resolution

Two related checks, both file/pixel-level and cheap to compute before running any ONNX inference:

- **Source image resolution** — reject images below a minimum (recommend reusing the existing classroom-photo precedent of **640×480** as a floor for the *whole downloaded image*, per `ClassroomImageValidator`'s implemented file-level checks — this is the one quality dimension classroom validation *does* enforce today).
- **Face crop resolution** — separately, the *detected face's bounding box* must be at least a minimum size (recommend **112×112**, matching `RecognitionInputSize` exactly — a face bounding box smaller than the network's own input size means the network is upsampling a crop that never had enough real detail, which measurably degrades embedding quality). Bounding box dimensions are already available on the detection result (`BoundingBoxWidth`/`BoundingBoxHeight` on `DetectedFaceDto`, `Abhyanvaya.Application/DTOs/Recognition/DetectedFaceDto.cs:12-15`) — no new detection capability needed, only a threshold comparison.

### 3.3 No Blur

**Status today: not implemented anywhere in the codebase.** `ClassroomImageValidator.ValidateBlurAsync` is an explicit stub:

```100:103:Abhyanvaya.Infrastructure/Validation/ClassroomImageValidator.cs
public Task<ClassroomImageValidationResult> ValidateBlurAsync(...) =>
    Task.FromResult(ClassroomImageValidationResult.Failure("Blur detection is not implemented yet."));
```

**This milestone must add a real implementation** (for enrollment; whether to also wire it into classroom validation is out of scope here). Recommended approach — variance-of-Laplacian sharpness score, a standard, cheap, well-understood technique:

1. Convert the **aligned face crop** (not the whole photo — focusing on the face region avoids background blur/bokeh incorrectly failing an in-focus face) to grayscale.
2. Convolve with a discrete Laplacian kernel (`ImageSharp` supports custom convolution kernels, or a simple manual 3×3 pass over the pixel buffer — either is a small, self-contained addition, no new package dependency required).
3. Compute the variance of the resulting response. Low variance ⇒ few sharp edges ⇒ blurry image.
4. Compare against an empirically-tuned threshold (must be calibrated against a sample of real ID photos before shipping — **do not hardcode a threshold without validating against real exambranch.com-style photos first**, since variance-of-Laplacian is scale- and content-sensitive).
5. Below threshold → `reject(FailureCategory.BlurRejected)`.

### 3.4 Pose (Yaw/Pitch/Roll)

Not implemented today, and the 5-point landmarks SCRFD already outputs (used today only for ArcFace alignment, `InsightFaceImageMath.AlignFace`) are sufficient for a **coarse** pose estimate without a new model:

- Roll: angle between the two eye landmarks relative to horizontal.
- Yaw (left/right turn): relative horizontal distance between eye-to-nose-tip vs. nose-tip-to-eye on each side (asymmetry indicates a turned head).
- Pitch (up/down tilt): relative vertical distance between eye-line and nose/mouth landmarks.

Recommend a **generous tolerance** initially (e.g., reject only clearly extreme poses — roll > 25°, or strong yaw/pitch asymmetry) since ID-photo sources are usually already front-facing by convention; this is a coarse geometric heuristic on existing landmark output, not a new ML model.

### 3.5 Eyes Open / No Sunglasses

**Recommended: defer to a v2 phase, explicitly flagged as such, not silently dropped.**

Neither check has an existing building block in this codebase (no eye-state classifier, no occlusion/sunglasses detector), and both are meaningfully harder than the geometric/statistical checks above:
- Eyes-open detection typically needs either a small dedicated eye-state model or an eye-aspect-ratio heuristic requiring finer per-eye landmarks than SCRFD's 5-point output provides (SCRFD gives 2 eye-center points, not eyelid contours).
- Sunglasses detection typically needs a dedicated classifier or a color/gradient heuristic over the eye region that is failure-prone in isolation (dark skin tone, shadows, and low-quality JPEGs all produce false positives).

Building either well would require either a new small ONNX model (out of scope — "no new production code" for this milestone, and a new model choice is itself a significant decision deserving its own review) or a heuristic weak enough that it risks rejecting *good* photos as often as bad ones — actively harmful for a bulk administrative process a SuperAdmin is trying to run over potentially thousands of students with minimal manual intervention. **Recommendation: ship exactly-one-face + resolution + blur + coarse-pose for v1, log occurrence of `EmbeddingQuality` distribution over time, and revisit eyes-open/sunglasses only if real-world data shows it's a material accuracy problem.**

### 3.6 No Multiple Faces

Covered by §3.1 (exactly-one-face is a single check that rejects both zero *and* more-than-one).

### Composite Quality Score

Per `docs/AI20_ENROLLMENT_DATABASE.md`'s `StudentEnrollmentJob.QualityScore` (`real?`, 0.0–1.0), computed as a weighted composite once a candidate passes all hard-reject gates:

```
QualityScore = w1 * normalize(DetectionScore)
             + w2 * normalize(FaceBoundingBoxArea / ImageArea)   // larger, centered face = better
             + w3 * normalize(LaplacianVarianceScore)            // sharper = better
             + w4 * (1 - normalize(PoseDeviation))               // more frontal = better
```

This score is **advisory** (drives the UI's quality column/sort and `StudentFaceEmbedding.EmbeddingQuality` bucket mapping — e.g. `>0.85 → Excellent`, `>0.65 → Good`, `>0.4 → Fair`, else `Poor`) and does not itself cause rejection — the hard gates in §3.1–3.4 already handled outright rejection; this score exists to let a SuperAdmin proactively find and re-enroll "technically passed but marginal" students (AI20.3's "Bulk Regenerate" against a quality threshold).

---

## 4. Embedding Generation

**Reused unchanged:** the same 112×112 ArcFace alignment (`InsightFaceImageMath.AlignFace`) and the same cached `w600k_r50.onnx` recognition session (`InsightFaceOnnxModelHost.GetRecognitionSession()`), producing the same 512-dimension `float[]` output, L2-normalized the same way (`EmbeddingNormalizer`, `MagnitudeTolerance = 0.01f`).

**New wrapper method** (name: `GenerateEnrollmentEmbedding`, sibling to the existing `GenerateSingleFaceEmbedding`, not a modification of it — the existing method keeps its current best-score-wins behavior for whatever currently calls it, avoiding any regression to manual photo upload):

```
GenerateEnrollmentEmbedding(imageBytes):
    image = Load(imageBytes)
    candidates = DetectFaces(image)              // existing, unmodified
    validationResult = EnrollmentFaceValidator.Validate(image, candidates)   // §3
    if not validationResult.Approved:
        throw EnrollmentValidationException(validationResult.FailureCategory, validationResult.Reason)
    aligned = AlignFace(image, validationResult.ApprovedCandidate.Landmarks, RecognitionInputSize)  // existing
    embedding = ExtractEmbedding(aligned)          // existing
    return (embedding, validationResult.QualityScore)
```

This keeps `InsightFaceEngine`'s existing detection/alignment/extraction primitives completely untouched — only a new orchestration method is added, plus the new standalone `IEnrollmentFaceValidator`.

---

## 5. Store Embedding

**Fully reused, unmodified:** `EmbeddingStorage.StoreCompletedAsync` (`Abhyanvaya.Infrastructure/Embedding/EmbeddingStorage.cs`) already implements exactly what's needed — writes `EmbeddingVector`, `EmbeddingDimension`, sets `IsActive = true` (superseding any prior active row per the existing unique filtered index), and records `EmbeddingModel`/`EmbeddingVersion`/`GeneratedUtc`/`GeneratedBy`. The enrollment pipeline calls this service exactly the way `EmbeddingPipeline` (manual upload path) already does — no new embedding-persistence code is needed, only a new caller.

The enrollment-specific addition is capturing the returned/created `StudentFaceEmbedding.Id` back onto `StudentEnrollmentJob.StudentFaceEmbeddingId` (per `docs/AI20_ENROLLMENT_DATABASE.md` §3.2) for traceability — a thin bookkeeping step in the enrollment orchestrator, not a change to `EmbeddingStorage` itself.

---

## 6. Update Enrollment Status

`StudentEnrollmentJob.Status` (per `docs/AI20_ENROLLMENT_DATABASE.md`) is updated at each pipeline stage boundary by the **enrollment orchestrator** (`docs/AI20_ENROLLMENT_BACKGROUND.md`'s `EnrollmentPipeline`), which is a **new, sibling** component to `ClassroomRecognitionPipeline` — it does not reuse or modify `ClassroomRecognitionPipeline` in any way, since the two jobs have entirely different inputs (external URL vs. already-uploaded classroom photo), different DB rows to update, and different downstream side effects (embedding storage vs. `AttendanceRecognition` rows + matching).

Deliberate separation from `StudentFaceEmbedding.EmbeddingStatus`: that column continues to track only "is the *current active embedding record* pending/processing/completed/failed" exactly as it does today for manual uploads; `StudentEnrollmentJob.Status` tracks the *administrative job's* full lifecycle including the download stage that has no equivalent concept in `StudentFaceEmbedding` at all. Both are updated during a successful enrollment run (the orchestrator calls into the existing `EmbeddingStorage`/embedding-status-setting code exactly as the manual path does, **in addition to** updating its own `StudentEnrollmentJob.Status`) — there is no conflict, because they track different things at different granularities.

---

## 7. Retry / Failure Handling

| Failure point | Category (`docs/AI20_ENROLLMENT_DATABASE.md`'s `FailureCategory`) | Retry classification |
|---|---|---|
| HTTP 404 from source | `PhotoNotFound` | Terminal (`Failed`) — retrying without a source-side fix cannot succeed |
| HTTP 403 from source | `AccessDenied` | Terminal (`Failed`) — likely a configuration/credentials issue requiring operator action, not a transient blip |
| HTTP 5xx / timeout / connection reset | — | Transient (`RetryRequired`), automatic retry with backoff (`docs/AI20_PHOTO_IMPORT.md`) |
| Downloaded bytes fail image decode | `InvalidImage` / `CorruptImage` | Terminal (`Failed`) — the source file itself is bad |
| `NoFaceDetected` | `NoFaceDetected` | Terminal (`Failed`) — retrying the same photo detects the same zero faces |
| `MultipleFacesDetected` | `MultipleFacesDetected` | Terminal (`Failed`) |
| Blur / low-resolution rejection | `BlurRejected` / `LowResolutionRejected` | Terminal (`Failed`) |
| R2/storage upload exception | `StorageUploadFailed` | Transient (`RetryRequired`) — typically a network/provider blip |
| ONNX/engine exception (out of memory, session fault) | `EmbeddingEngineFailed` | Transient (`RetryRequired`), capped retry count — mirrors the existing `EmbeddingStorage.MaxRetryCount = 3` precedent for the manual-upload embedding path |

**All "terminal `Failed`" categories remain manually retriable** by a SuperAdmin via the UI (AI20.3's per-student and bulk "Retry" actions) — "terminal" here means *the automatic pipeline will not retry it on its own*, not that the job row is permanently frozen. A manual retry re-enters the job at `Pending` (per the state machine in `docs/AI20_ENROLLMENT_ARCHITECTURE.md` §6), which is appropriate because the SuperAdmin action typically follows a real-world fix (uploaded a corrected photo at the source, corrected the URL template, etc.) — the system cannot know that fix happened, so it does not auto-retry, but it also should not block a human-initiated retry.

Automatic retries (the `RetryRequired` transient path) follow the capped-count, backoff-based strategy detailed in `docs/AI20_ENROLLMENT_BACKGROUND.md`; once `RetryCount` exceeds the configured maximum, a transient failure is escalated to terminal `Failed` rather than retrying forever.

---

## Constraints Confirmed

No changes were made to `InsightFaceEngine`, `InsightFaceOnnxModelHost`, `InsightFaceEmbeddingGenerator`, `EmbeddingStorage`, or any other existing recognition/embedding code to produce this document. All new components described (`IEnrollmentFaceValidator`, `GenerateEnrollmentEmbedding`) are proposed additions only.
