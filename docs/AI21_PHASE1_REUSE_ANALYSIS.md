# AI21.PHASE1 — Reuse Analysis

## Objective

Add an enterprise photo acquisition framework that downloads student photos from external SIS/ERP sources and prepares them for the existing AI20 enrollment pipeline — without modifying Recognition, Attendance, Governance, or Operations.

## Reusable Components

| Component | Location | Reuse |
|---|---|---|
| `IStudentPhotoProvider` | Application | Source adapter for `IStudentPhotoDownloader` |
| `IStudentPhotoProviderFactory` | Infrastructure/Enrollment | Provider resolution in `StudentPhotoSourceAdapter` |
| `ExamBranchPhotoProvider` | Infrastructure | First production source |
| `ExternalPhotoImportPolicies` | Infrastructure/Resilience | HTTP retry for provider fetch (unchanged) |
| `IEnrollmentJobQueue` | Application | Wake signal when photos are ready for enrollment |
| `Student`, `College` | Domain | Student master fields for acquisition requests |
| ImageSharp quality math | Infrastructure/Enrollment/Validation | Similar blur/brightness patterns (no face AI) |

## New AI21.PHASE1 Components

| Component | Reason |
|---|---|
| `IStudentPhotoSource` | Resolve provider + source reference from student master |
| `IStudentPhotoDownloader` | Acquisition-layer download with timeout |
| `IPhotoDownloadCoordinator` | Parallel batch orchestration |
| `IPhotoValidationService` | Pre-enrollment format/corruption/resolution/duplicate checks |
| `IPhotoQualityAssessmentService` | Non-AI quality metrics (blur, brightness, contrast) |
| `IPhotoRetryPolicy` | Acquisition retry separate from enrollment retry |
| `IPhotoManifestGenerator` | Download manifest for audit and handoff |
| `IPhotoDownloadRepository` | Durable batch/item persistence |
| `IPhotoDownloadQueue` | Download/retry/enrollment queue channels |
| `IPhotoAcquisitionReportService` | Quality and failure reporting |

## Explicit Non-Goals

- No embedding generation
- No Cloudflare R2 upload
- No recognition
- No changes to AI20 frozen layers

## Future Roadmap

1. Additional providers (OU, CSV, Google Drive, Azure Blob)
2. File-system staging instead of DB byte storage for very large batches
3. API endpoints and admin UI for acquisition batches
4. Direct handoff adapter into `EnrollmentBatchService` using manifest entries

## Design Principles

- Stateless services with concurrent-safe collections
- Parallel downloads via `SemaphoreSlim`
- Validation excludes corrupt/duplicate/oversized images
- Quality assessment uses image statistics only (face/occlusion placeholders)
- Enrollment wake signal only — enrollment pipeline unchanged
