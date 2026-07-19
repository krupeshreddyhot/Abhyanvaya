# AI20.PHASE2 — Enrollment Validation Rule Matrix

**Milestone:** AI20.PHASE2.1.4  
**Status:** Single source of truth — complete before implementation  
**Authority:** `docs/AI20_ENROLLMENT_ENGINE.md` §3, `docs/AI20_PHASE2_VALIDATION_REPORT.md`, `ValidationRulesSnapshot` defaults

---

## Rule Priority Order (First Hard Failure Wins)

When multiple rules fail, `FailureCategory` reflects the **first** failed required rule in this order. Measurement-completeness (§7 of validation report doc) still populates all measurable Tier-3 fields when `FaceCount == 1`.

| Order | Rule ID | Gate type |
|---|---|---|
| 1 | `ImageFormat` | Pre-decode |
| 2 | `CorruptImage` | Pre-decode |
| 3 | `MinimumSourceResolution` | Pre-ONNX |
| 4 | `MaximumSourceResolution` | Pre-ONNX |
| 5 | `ExactlyOneFace` | Post-detection |
| 6 | `FaceConfidence` | Post-detection |
| 7 | `MinimumFaceCropResolution` | Post-detection (FaceCount==1) |
| 8 | `FaceSizeCoverage` | Post-detection (FaceCount==1) |
| 9 | `BlurScore` | Post-detection (FaceCount==1) |
| 10 | `Pose` | Post-detection (FaceCount==1) |
| 11 | `Brightness` | Post-detection (FaceCount==1) |
| 12 | `Contrast` | Post-detection (FaceCount==1) |

---

## Required Rules (v1)

| Rule | Purpose | Required | Severity | Failure Code (`FailureCategory`) | Diagnostic Code | User Message | Telemetry Key | Future AI Extension |
|---|---|---|---|---|---|---|---|---|
| **ImageFormat** | Reject unsupported file extensions before decode | Yes | Hard | `InvalidImage` | `VAL_UNSUPPORTED_FORMAT` | Supported formats: JPG, JPEG, PNG, WebP. | `rules.imageFormat` | — |
| **CorruptImage** | Detect unreadable/truncated bytes | Yes | Hard | `CorruptImage` | `VAL_CORRUPT_IMAGE` | The image file appears to be corrupt or unreadable. | `rules.corruptImage` | — |
| **MinimumSourceResolution** | Whole-image floor before ONNX (save CPU) | Yes | Hard | `LowResolutionRejected` | `VAL_SOURCE_RES_TOO_LOW` | Source image must be at least {MinW}×{MinH} pixels. | `rules.minSourceResolution` | — |
| **MaximumSourceResolution** | Reject unreasonably large sources (memory guard) | Yes | Hard | `LowResolutionRejected` | `VAL_SOURCE_RES_TOO_HIGH` | Source image must not exceed {MaxW}×{MaxH} pixels. | `rules.maxSourceResolution` | — |
| **ExactlyOneFace** | Enrollment requires one canonical subject | Yes | Hard | `NoFaceDetected` / `MultipleFacesDetected` | `VAL_NO_FACE` / `VAL_MULTIPLE_FACES` | No face detected. / Multiple faces detected; enrollment requires exactly one. | `rules.exactlyOneFace` | — |
| **FaceConfidence** | Detector score must meet threshold | Yes | Hard | `NoFaceDetected` | `VAL_LOW_FACE_CONFIDENCE` | Face detection confidence below minimum ({threshold}). | `rules.faceConfidence` | — |
| **MinimumFaceCropResolution** | Bbox must be ≥112×112 (network input) | Yes | Hard | `LowResolutionRejected` | `VAL_FACE_CROP_TOO_SMALL` | Face crop must be at least {MinW}×{MinH} pixels. | `rules.minFaceCropResolution` | — |
| **FaceSizeCoverage** | Face must occupy minimum % of frame | Yes | Hard | `LowResolutionRejected` | `VAL_FACE_TOO_SMALL_IN_FRAME` | Face occupies {ratio:P1} of the image; minimum is {min:P1}. | `rules.faceSizeCoverage` | — |
| **BlurScore** | Variance-of-Laplacian on aligned crop | Yes | Hard | `BlurRejected` | `VAL_BLUR_REJECTED` | Image is too blurry (score {score:F0}, minimum {threshold:F0}). | `rules.blurScore` | — |
| **Pose** | Yaw/pitch/roll within generous ID-photo limits | Yes | Hard | `BlurRejected`* | `VAL_POSE_REJECTED` | Head pose exceeds allowed limits (yaw {yaw:F0}°, pitch {pitch:F0}°, roll {roll:F0}°). | `rules.pose` | — |
| **Brightness** | Mean luma of aligned face in acceptable band | Yes | Hard | `BlurRejected`* | `VAL_BRIGHTNESS_REJECTED` | Face brightness {value:F2} outside acceptable range [{min:F2}, {max:F2}]. | `rules.brightness` | — |
| **Contrast** | Std-dev luma of aligned face above floor | Yes | Hard | `BlurRejected`* | `VAL_CONTRAST_REJECTED` | Face contrast {value:F2} below minimum {min:F2}. | `rules.contrast` | — |

\* **Note:** Domain `FailureCategory` has no dedicated pose/brightness/contrast members. Per frozen enum, quality rejections map to `BlurRejected` for pose/brightness/contrast until a future enum extension. Diagnostic codes disambiguate.

---

## Optional / Future Rules (Extension Points — Disabled v1)

| Rule | Purpose | Required | Severity | Failure Code | Diagnostic Code | User Message | Telemetry | Future AI Extension |
|---|---|---|---|---|---|---|---|---|
| **Liveness** | Live subject vs print/screen | No (v2) | — | — | — | — | `rules.liveness` = Skipped | Dedicated liveness ONNX model |
| **MaskDetection** | Face mask obstruction | No (v2) | — | — | — | — | `rules.maskDetection` = Skipped | Segmentation / classifier model |
| **EyeOpenness** | Eyes open gate | No (v2) | — | — | — | — | `rules.eyeOpenness` = Skipped | Eye-state model or fine landmarks |
| **SpoofDetection** | Presentation attack | No (v2) | — | — | — | — | `rules.spoofDetection` = Skipped | Anti-spoof ONNX |
| **Occlusion** | Hand/hair/sticker coverage | No (v2) | — | — | — | — | `rules.occlusion` = Skipped | Segmentation heuristic / model |
| **Sunglasses** | Eye region obstruction | No (v2) | — | — | — | — | `rules.sunglasses` = Skipped | Classifier over eye patches |
| **Smile** | Expression neutrality | No (v2) | — | — | — | — | `rules.smile` = Skipped | Expression classifier |
| **Expression** | Neutral expression gate | No (v2) | — | — | — | — | `rules.expression` = Skipped | Expression classifier |

All future rules return `ValidationRuleOutcome.Skipped` with `IsEnabled = false`. No AI models are loaded or invoked in v1.

---

## Rule Outcome Values

Every rule records one of:

| Outcome | Meaning |
|---|---|
| `Pass` | Rule evaluated; requirement met |
| `Fail` | Rule evaluated; requirement not met (may set `FailureCategory` if first hard failure) |
| `Skipped` | Rule disabled (future extension points) or prerequisites missing |
| `NotApplicable` | Prerequisites not met (e.g. blur rule when `FaceCount != 1`) |

---

## Default Thresholds (Configurable via `EnrollmentValidationOptions`)

| Parameter | Default | Source |
|---|---|---|
| Min source width × height | 640 × 480 | `ClassroomImageValidator` lines 13–14; `ValidationRulesSnapshot` |
| Max source width × height | 8192 × 8192 | Memory guard (new) |
| Min face crop width × height | 112 × 112 | `InsightFaceOptions.RecognitionInputSize`; `ValidationRulesSnapshot` |
| Min face coverage ratio | 0.05 (5%) | `AI20_ENROLLMENT_ENGINE.md` composite / relative size |
| Detection confidence floor | 0.50 | `InsightFaceOptions.DetectionThreshold` |
| Blur method | VarianceOfLaplacian | `ValidationRulesSnapshot.BlurMethod` |
| Blur threshold | 100.0 | `ValidationRulesSnapshot.BlurThreshold` (calibration placeholder) |
| Max abs yaw / pitch / roll | 25° / 25° / 25° | `ValidationRulesSnapshot` |
| Brightness range (mean luma) | 0.20 – 0.85 | Enrollment-specific (configurable) |
| Min contrast (std-dev luma) | 0.08 | Enrollment-specific (configurable) |
| Composite weights | detection 0.40, faceArea 0.20, sharpness 0.25, pose 0.15 | `AI20_PHASE2_VALIDATION_REPORT.md` §6 |

Composite score is **informational only** — never determines pass/fail.

---

## Supported Image Formats

| Format | Extension | Magic / Identify |
|---|---|---|
| JPEG | `.jpg`, `.jpeg` | ImageSharp `IdentifyAsync` |
| PNG | `.png` | ImageSharp `IdentifyAsync` |
| WebP | `.webp` | ImageSharp `IdentifyAsync` |

---

## Constraints

- Rules determine pass/fail; composite score does not.
- Expected failures return `EnrollmentValidationResult`; they do not throw.
- ONNX / infrastructure faults throw (orchestrator maps to `EmbeddingEngineFailed`).
- No image bytes in logs, telemetry, or `ValidationReport`.
