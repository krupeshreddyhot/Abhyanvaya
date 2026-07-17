# AI20.PHASE2.1.6 — Embedding Architecture Reuse Analysis

**Milestone:** Pre-implementation review before `IEnrollmentEmbeddingService`.

## Existing Components

| Component | Location | Capability |
|-----------|----------|------------|
| `InsightFaceEngine` | `Infrastructure/InsightFace/InsightFaceEngine.cs` | SCRFD detection, 5-point alignment, ArcFace ONNX embedding, L2 normalization |
| `InsightFaceImageMath` | `Infrastructure/InsightFace/InsightFaceImageMath.cs` | Detection/recognition tensor build, `AlignFace`, `L2Normalize` |
| `InsightFaceOnnxModelHost` | `Infrastructure/InsightFace/InsightFaceOnnxModelHost.cs` | Lazy singleton ONNX sessions |
| `InsightFaceOptions` | `Infrastructure/InsightFace/InsightFaceOptions.cs` | Model files, 512-dim, 112 input, pipeline version |
| `IEmbeddingGenerator` | `Application/Common/Interfaces/IEmbeddingGenerator.cs` | Manual upload path — re-detects from `PhotoKey` |
| `IEmbeddingNormalizer` | `Application/Common/Interfaces/IEmbeddingNormalizer.cs` | L2 normalize + magnitude tolerance |
| `IEmbeddingValidator` | `Application/Common/Interfaces/IEmbeddingValidator.cs` | Dimension / NaN / Infinity checks |
| `IEmbeddingGenerationMetrics` | `Application/Common/Interfaces/IEmbeddingGenerationMetrics.cs` | Generation telemetry |
| `IEnrollmentArtifactResolver` | `Application/Common/Interfaces/IEnrollmentArtifactResolver.cs` | Reads `AlignedFace` from manifest |

## Reuse Decision Matrix

| Need | Reuse | Reason |
|------|-------|--------|
| ONNX inference | ✅ `InsightFaceEngine.ExtractEmbedding` (private) | Production-tuned; no duplicate ONNX |
| Pre-aligned embed | ✅ New public `GenerateEmbeddingFromAlignedFace(Stream)` | Thin wrapper over `ExtractEmbedding` — skips re-detection |
| L2 normalization | ✅ `IEmbeddingNormalizer` / `InsightFaceImageMath.L2Normalize` | Deterministic; idempotent second pass |
| Vector validation | ✅ Extended `IEmbeddingValidator` | Same validator used by manual upload pipeline |
| Artifact input | ✅ `IEnrollmentArtifactResolver` | Sole read path; checksum verified |
| Metrics | ✅ `IEmbeddingGenerationMetrics` | Existing counters/timings |
| Manual upload generator | ❌ `InsightFaceEmbeddingGenerator` | Re-downloads original photo and re-detects — wrong for enrollment |
| `GenerateSingleFaceEmbedding` | ❌ Direct use | Picks best face among multiples — enrollment already validated exactly-one-face |
| `IEmbeddingPipeline` | ❌ Direct use | Bound to `StudentPhotoUploadedMessage` queue model |
| Persistence | ❌ In embedding service | Result writer / `IEmbeddingStorage` own persistence |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IEnrollmentEmbeddingService` | Orchestrates enrollment-specific flow: manifest → resolver → engine → artifact. No existing interface covers manifest-based input. |
| `IEmbeddingEngine` | Enrollment must not depend on `InsightFaceEngine` directly. Abstracts future ArcFace/AdaFace/FaceNet providers. Distinct from `IEmbeddingGenerator` (PhotoKey-based manual upload). |
| `InsightFaceEmbeddingEngine` | Adapter wrapping `InsightFaceEngine` for `IEmbeddingEngine` — one line of indirection, enables provider swap via DI. |
| `EnrollmentEmbeddingService` | Thin orchestrator — no AI logic, delegates validation/normalization/quality. |
| `IEmbeddingQualityAnalyzer` | Advisory diagnostics only — not covered by validator (which gates failures). |
| `EmbeddingQualityAnalyzer` | Implements quality score + diagnostic messages for future analytics. |
| `EnrollmentEmbeddingArtifact` / `EnrollmentEmbeddingResult` | Immutable enrollment output contract with provenance fields from manifest + engine. |

## Methods Reused (Not Duplicated)

| Method | File | Used by |
|--------|------|---------|
| `ExtractEmbedding(Image<Rgb24>)` | `InsightFaceEngine.cs` | `GenerateEmbeddingFromAlignedFace` |
| `InsightFaceImageMath.BuildRecognitionInput` | `InsightFaceImageMath.cs` | Via `ExtractEmbedding` |
| `InsightFaceImageMath.L2Normalize` | `InsightFaceImageMath.cs` | Via `ExtractEmbedding` |
| `EmbeddingNormalizer.Normalize` | `EmbeddingNormalizer.cs` | `EnrollmentEmbeddingService` |
| `EmbeddingValidator.Validate/ValidateNormalized/ComputeStatistics` | `EmbeddingValidator.cs` | `EnrollmentEmbeddingService` |
| `EnrollmentArtifactResolver.ResolveAsync` | `EnrollmentArtifactResolver.cs` | `EnrollmentEmbeddingService` |

## Architecture Boundary

```
EnrollmentStorageManifest
        ↓
IEnrollmentArtifactResolver  (reuse — unchanged)
        ↓
EnrollmentEmbeddingService   (new orchestrator)
        ↓
IEmbeddingEngine             (new abstraction)
        ↓
InsightFaceEmbeddingEngine → InsightFaceEngine.GenerateEmbeddingFromAlignedFace
        ↓
IEmbeddingNormalizer + IEmbeddingValidator + IEmbeddingQualityAnalyzer
        ↓
EnrollmentEmbeddingResult    (pure output — no persistence)
```

## Verification

- No duplicate ONNX preprocessing or normalization algorithms
- No changes to validation, storage, artifact resolver, or storage pipeline
- Manual upload embedding path unchanged
