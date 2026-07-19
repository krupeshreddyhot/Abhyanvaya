# AI20.PHASE2.1.5 — Storage Reuse Analysis

**Phase:** AI20.PHASE2.1.5  
**Purpose:** Pre-implementation architecture review — identify reusable storage components and justify new classes.

---

## Existing Components

| Existing Component | Location | Reuse Decision | Reason |
|---|---|---|---|
| `IStorageProvider` | `Abhyanvaya.API/Media/IStorageProvider.cs:4-38` | **Reuse via adapter** | Single-object write/read/delete abstraction; S3/R2 and local already implemented. Application layer must not reference API types directly. |
| `S3StorageProvider` | `Abhyanvaya.API/Media/S3StorageProvider.cs:30-74` | **Reuse unchanged** | Cloudflare R2 via S3-compatible `PutObjectRequest`; R2 signing flags at lines 53-54. |
| `LocalStorageProvider` | `Abhyanvaya.API/Media/LocalStorageProvider.cs:41-53` | **Reuse unchanged** | Dev/local disk writes. |
| `IStorageProviderFactory` | `Abhyanvaya.API/Media/IStorageProviderFactory.cs:4-9` | **Reuse via adapter** | Resolves active provider name for metadata. |
| `IMediaStorageService` (Application) | `Abhyanvaya.Application/Common/Interfaces/IMediaStorageService.cs:6-15` | **Reuse pattern** | Recognition/enrollment precedent — byte write seam used by `RecognitionMediaService`. |
| `ApplicationMediaStorageService` | `Abhyanvaya.API/Media/ApplicationMediaStorageService.cs:17-38` | **Reuse pattern** | Adapter over `IStorageProvider`; enrollment storage uses parallel `IObjectStorageProvider` adapter. |
| `MediaStorageService.SaveVariantsAsync` | `Abhyanvaya.API/Media/MediaStorageService.cs:38-59` | **Reuse later** | N-object loop pattern for thumbnail variants; not invoked in 2.1.5 (aligned face + report only). |
| `StudentMediaPaths` | `Abhyanvaya.Application/StudentMediaPaths.cs:7-8` | **Reuse for canonical base** | `students/{tenantId}/{studentId}` key convention preserved for downstream embedding compatibility. |
| `RecognitionMediaService` | `Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs:52-120` | **Reuse pattern** | Template for deterministic key + delegate upload + structured logging; no duplicate provider logic. |
| `IUnitOfWork` / `ExecuteInTransactionAsync` | `Abhyanvaya.Application/Common/Interfaces/IUnitOfWork.cs:13-15` | **Reuse** | Transaction boundary for metadata persistence + rollback. |
| `StudentEnrollmentItem` checksum column | `Abhyanvaya.Domain/Entities/StudentEnrollmentItem.cs:49-50` | **Reuse field** | SHA-256 hex for idempotency; populated by result writer (future), not storage service directly. |
| Inline `SHA256.HashData` | `EnrollmentConfigurationSnapshotCapture.cs:245-249` | **Reuse pattern** | No dedicated checksum service existed; new `IChecksumService` centralizes photo-byte hashing without duplicating call sites. |
| `EnrollmentValidationArtifact` | `EnrollmentValidationFrameworkModels.cs:49-61` | **Reuse input** | Storage input artifact from validation phase 2.1.4A. |

---

## New Components (Justification)

| New Component | Justification |
|---|---|
| `IObjectStorageProvider` | Application-layer seam required by phase spec; existing name is `IStorageProvider` in API. Adapter wraps factory without leaking Cloudflare/AWS types into Application/Infrastructure enrollment code. |
| `IChecksumService` | No existing photo-byte checksum utility; inline SHA256 only used for config snapshots. Centralizes integrity/duplicate detection for enrollment artifacts. |
| `IEnrollmentStorageService` | Designed in `AI20_PHASE2_ENGINE_CONTRACTS.md` §9 but never implemented. Sole owner of enrollment artifact persistence. |
| `IEnrollmentArtifactTypeRegistry` | Phase spec requires extensible artifact types without switch statements; no existing registry. |
| `IEnrollmentStoragePolicy` | Phase spec requires tenant/college retention/provider resolution; no existing enrollment storage policy. |
| `EnrollmentStorageRecord` entity | Append-only versioning metadata (ArtifactVersion, StorageVersion, manifest linkage) not present on `StudentEnrollmentItem`; required for immutable artifact history without overloading item row. |
| `EnrollmentStorageService` | Orchestrates policy → registry → checksum → object upload → metadata transaction; no existing class performs this for enrollment artifacts. |
| `ObjectStorageProviderAdapter` | Bridges Application `IObjectStorageProvider` to API `IStorageProviderFactory`; cannot place in Infrastructure (no API reference). |

---

## Duplication Avoided

- No new S3/R2/local provider implementations.
- No validation, embedding, batch, or progress logic in storage service.
- No direct Cloudflare API references outside existing `S3StorageProvider`.
- Object upload delegates to existing `WriteObjectAsync` stack.
