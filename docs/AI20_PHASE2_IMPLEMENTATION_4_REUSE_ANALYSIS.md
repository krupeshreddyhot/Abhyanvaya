# AI20.PHASE2.1.4 — Enrollment Validation Service Reuse Analysis

**Milestone:** AI20.PHASE2.1.4  
**Status:** Pre-implementation architectural review (required gate before rule implementation)  
**Scope:** Determine reuse vs. new code for every InsightFace / ImageSharp capability the validation service needs.

---

## Executive Summary

| Decision | Outcome |
|---|---|
| Face detection (ONNX SCRFD) | **Reuse** — `InsightFaceEngine.DetectFaces` (private, lines 190–254) via new public `AnalyzeForEnrollmentValidationAsync` |
| Face alignment (ArcFace 5-point) | **Reuse** — `InsightFaceImageMath.AlignFace` (lines 71–87) |
| Bounding box conversion | **Reuse** — `InsightFaceImageMath.ToBoundingBox` (lines 167–174) |
| NMS / candidate filtering | **Reuse** — `InsightFaceImageMath.ApplyNms` (lines 149–165), invoked inside `DetectFaces` |
| Image decode (RGB) | **Reuse pattern** — `Image.Load<Rgb24>` as in `InsightFaceEngine.DetectAsync` line 56 |
| Image identify (format/corrupt) | **Reuse pattern** — `ClassroomImageValidator.ValidateImageIntegrityAndResolutionAsync` lines 55–92 |
| Supported extensions | **Reuse constants pattern** — `ClassroomImageValidator` lines 16–17, 46–50 |
| Embedding extraction | **Do NOT call** — `ExtractEmbedding` (lines 256–335) is out of scope for validation |
| Full `DetectAsync` pipeline | **Do NOT call** — lines 45–174 generate embeddings + thumbnails for every face (recognition path) |
| Blur detection | **New** — no implementation exists (`ClassroomImageValidator.ValidateBlurAsync` is a stub, lines 100–101) |
| Pose estimation | **New heuristic** — landmarks from SCRFD reused; no existing pose helper |
| Brightness / contrast | **New** — `ClassroomImageValidator.ValidateBrightnessAsync` is a stub (lines 97–98) |
| Liveness / mask / spoof / etc. | **Extension points only** — no building blocks in codebase |

**Naming note:** The task spec references `IInsightFaceEngine`. The codebase exposes a concrete `InsightFaceEngine` class (`Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs`) wrapped by `IFaceDetectionService` (`InsightFaceDetectionService.cs:26–29`). There is **no** `IInsightFaceEngine` interface. Validation introduces `IEnrollmentFaceAnalysisService` (Application) implemented by `InsightFaceEnrollmentFaceAnalysisService` (Infrastructure) delegating to the new engine method — preserving Clean Architecture test seams without duplicating ONNX logic.

---

## Reuse Matrix

| Existing Component | File | Class / Method | Lines | Reuse Decision | Reason |
|---|---|---|---|---|---|
| ONNX face detection session | `InsightFaceEngine.cs` | `DetectFaces(Image<Rgb24>)` | 190–254 | **Reuse (via new public wrapper)** | Private method encapsulates SCRFD inference, NMS, landmark parsing — duplicating would fork AI logic |
| Detection input tensor | `InsightFaceImageMath.cs` | `BuildDetectionInput` | 28–69 | **Reuse** | Called only from `DetectFaces` line 194 |
| NMS | `InsightFaceImageMath.cs` | `ApplyNms` | 149–165 | **Reuse** | Called from `DetectFaces` line 221 |
| Output parsing | `InsightFaceEngine.cs` | `ParseDetectionOutputs` | 349–464 | **Reuse** | Called from `DetectFaces` line 220 |
| ONNX model host | `InsightFaceOnnxModelHost` | singleton sessions | — | **Reuse** | Same cached `det_10g.onnx` session as recognition |
| Detection threshold | `InsightFaceOptions.cs` | `DetectionThreshold` | 14 | **Reuse** | Default 0.5 — face-confidence gate |
| Recognition input size | `InsightFaceOptions.cs` | `RecognitionInputSize` | 20 | **Reuse** | 112 — alignment output + face-crop floor |
| 5-point alignment | `InsightFaceImageMath.cs` | `AlignFace` | 71–87 | **Reuse** | Produces aligned crop for blur metrics + downstream `AlignedFaceBytes` |
| Similarity transform | `InsightFaceImageMath.cs` | `EstimateSimilarityTransform` | 189–216 | **Reuse (indirect)** | Private; used only by `AlignFace` |
| Bilinear sampling | `InsightFaceImageMath.cs` | `SampleBilinear` | 218–250 | **Reuse (indirect)** | Private; used only by `AlignFace` |
| Bounding box DTO mapping | `InsightFaceImageMath.cs` | `ToBoundingBox` | 167–174 | **Reuse** | Maps `FaceCandidate` → pixel bbox |
| WebP aligned crop encode | `InsightFaceEngine.cs` | `DetectAsync` loop | 118–123 | **Reuse pattern** | Same `SaveAsWebpAsync` for single-face validation output |
| Image decode RGB | `InsightFaceEngine.cs` | `DetectAsync` / `GenerateSingleFaceEmbedding` | 56, 178 | **Reuse pattern** | `Image.Load<Rgb24>(bytes)` |
| Image identify (no full decode) | `ClassroomImageValidator.cs` | `ValidateImageIntegrityAndResolutionAsync` | 66–77 | **Reuse pattern** | `Image.IdentifyAsync` for corrupt/format probe |
| Supported extensions | `ClassroomImageValidator.cs` | `SupportedExtensions` | 16–17 | **Reuse pattern** | `.jpg`, `.jpeg`, `.png`, `.webp` |
| Max file size check | `ClassroomImageValidator.cs` | `ValidateSupportedFormat` | 36–44 | **Adapt for enrollment** | Enrollment uses metadata `ByteSize`; same 15 MB ceiling |
| `IFaceDetectionService` | `InsightFaceDetectionService.cs` | `DetectAsync` | 26–29 | **Do NOT call** | Delegates to full pipeline including embedding (lines 100–107 of engine) |
| `GenerateSingleFaceEmbedding` | `InsightFaceEngine.cs` | method | 176–188 | **Do NOT call** | Picks best face, no exactly-one check, always embeds |
| Embedding extraction | `InsightFaceEngine.cs` | `ExtractEmbedding` | 256–335 | **Do NOT call** | Validation performs no embedding |
| L2 normalize | `InsightFaceImageMath.cs` | `L2Normalize` | 126–147 | **Do NOT call** | Embedding-only |
| Recognition input tensor | `InsightFaceImageMath.cs` | `BuildRecognitionInput` | 89–124 | **Do NOT call** | Embedding-only |
| Blur | `ClassroomImageValidator.cs` | `ValidateBlurAsync` | 100–101 | **New implementation** | Explicit stub — "not implemented yet" |
| Brightness | `ClassroomImageValidator.cs` | `ValidateBrightnessAsync` | 97–98 | **New implementation** | Explicit stub |
| Face count | `ClassroomImageValidator.cs` | `ValidateFaceCountAsync` | 106–107 | **New (via engine)** | Stub; enrollment uses ONNX detection count |
| Pose | — | — | — | **New heuristic** | Landmarks available from `FaceCandidate.Landmarks` (engine line 450) |
| Diagnostics / forensics | `InsightFaceEngine.cs` | `_diagnostics`, `_forensics` | throughout `DetectAsync` | **Do NOT wire** | Recognition-only instrumentation; validation uses structured logging only |

---

## New Components (Justified)

| New Component | Location | Justification |
|---|---|---|
| `IEnrollmentValidationService` | `Abhyanvaya.Application/Common/Interfaces/` | Frozen contract §8 — pure evaluation seam |
| `IEnrollmentFaceAnalysisService` | `Abhyanvaya.Application/Common/Interfaces/` | Testable facade over engine detection+alignment without embedding |
| `AnalyzeForEnrollmentValidationAsync` | `InsightFaceEngine.cs` | Public wrapper calling private `DetectFaces` + `AlignFace` without `ExtractEmbedding` — **only** engine touch; no change to `DetectAsync` / `GenerateSingleFaceEmbedding` behavior |
| `InsightFaceEnrollmentFaceAnalysisService` | `Infrastructure/InsightFace/` | Implements Application interface; maps engine output to DTOs |
| `EnrollmentValidationService` | `Infrastructure/Enrollment/Validation/` | Rule orchestration, report assembly, telemetry — no AI duplication |
| `EnrollmentFaceQualityAnalyzer` | `Infrastructure/Enrollment/Validation/` | Variance-of-Laplacian blur, luma brightness/contrast, landmark pose — **not** in InsightFace today |
| `EnrollmentImageIntegrityChecker` | `Infrastructure/Enrollment/Validation/` | Format/corrupt/resolution pre-checks — mirrors `ClassroomImageValidator` patterns, enrollment-specific thresholds |
| `EnrollmentValidationOptions` | `Infrastructure/Enrollment/Validation/` | Configurable thresholds (matches `ValidationRulesSnapshot` defaults) |
| Future rule placeholders | `Infrastructure/Enrollment/Validation/Rules/` | `IEnrollmentValidationRule` implementations returning `Skipped` — extension points only |

---

## Conflict Check — Duplicate AI Logic

**No conflict.** All ONNX inference remains inside `InsightFaceEngine.DetectFaces` (private). The validation service never reimplements SCRFD tensor building, output parsing, or NMS. Image-quality metrics (Laplacian, luma statistics, geometric pose) operate on pixels **after** detection — they are enrollment-specific gates documented in `AI20_ENROLLMENT_ENGINE.md` §3.3–3.4 as new work, distinct from the InsightFace ONNX pipeline.

---

## Constraints Verified

- Recognition pipeline (`ClassroomRecognitionPipeline.cs:117`) — **not modified**
- Attendance pipeline — **not modified**
- `ExtractEmbedding` / embedding generator — **not modified** (validation never calls them)
- Storage, R2, repositories, progress reporter, batch service — **not referenced**
- No schema changes, no UI changes
