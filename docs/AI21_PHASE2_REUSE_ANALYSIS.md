# AI21.PHASE2 — Reuse Analysis

## Reused Components (Frozen — Not Modified)

| Component | Path | Role in PHASE2 |
|---|---|---|
| `IEnrollmentFaceAnalysisService` | Application | Face detection + alignment via `EnrollmentFaceAnalysisBridge` |
| `IEmbeddingEngine` | Application | Embedding generation from aligned face |
| `IEmbeddingValidator` / `IEmbeddingNormalizer` | Application | Embedding dimension + normalization checks |
| `IEnrollmentDuplicateDetector` | Application | Metadata duplicate enrollment guard |
| `IPhotoDownloadRepository` | Application | Consumes `ReadyForEnrollment` acquisition items |
| `IAITracingService` / `IAITelemetryService` | Application | Enterprise operations integration |
| `IEnrollmentJobQueue` | Application | Existing wake signal (not modified) |
| AI20 Worker Framework | Infrastructure | Unchanged — PHASE2 runs parallel enrollment pipeline |

## New Components (AI21.PHASE2)

| Component | Reason |
|---|---|
| `IEnrollmentCoordinator` | Consumes acquisition batch; never performs AI |
| `IEnrollmentBatchProcessor` | Single/batch/parallel processing with resume support |
| `IFaceDetectionEngine` / `IFaceAlignmentEngine` | Thin adapters over `IEnrollmentFaceAnalysisService` — no duplicate ONNX |
| `IEnrollmentQualityEngine` | Policy-driven quality gates (face count, embedding, score) |
| `IEnrollmentArtifactBuilder` | Immutable storage-agnostic artifacts |
| `IEnrollmentProgressTracker` | `EnrollmentState` lifecycle + progress % |
| `IEnrollmentFailureHandler` | Structured failure/retry handling |
| `IEnrollmentDuplicateDetectorService` | Student number, artifact hash, metadata duplicates |
| `IEnrollmentRepository` | Face enrollment batch/job persistence |
| `IEnrollmentManifestGenerator` | Success/failure/duplicate/retry manifests |
| `IEnrollmentReportService` | Batch/failure/quality/duplicate reports |
| `IArtifactUploadQueue` | Defers upload to AI21.PHASE3 — no R2 |
| `IFaceEnrollmentRecoveryService` | Resume interrupted batches |

## Explicit Non-Goals

- No Recognition Engine changes
- No Attendance / Governance / Operations modifications
- No Worker Framework changes
- No Cloudflare R2 upload
- No duplicate face detection / embedding implementations

## Future Roadmap (PHASE3+)

1. `IArtifactUploadQueue` consumer for storage upload
2. API endpoints to trigger coordinator from acquisition batches
3. Bridge artifacts into AI20 `IEnrollmentOrchestrator` for unified persistence path
4. Vector similarity duplicate detection (design-only in AI20 docs)

## Design Principles

- Consumes only `PhotoAcquisitionItemStatus.ReadyForEnrollment`
- All AI delegated to AI20 engines
- Policy-driven quality via `IEnrollmentPolicy` / `EnrollmentPolicyOptions`
- Immutable `EnrollmentContext` and `EnrollmentArtifact`
- Stateless, thread-safe services with parallel batch support
