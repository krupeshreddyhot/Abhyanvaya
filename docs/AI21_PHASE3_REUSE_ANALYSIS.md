# AI21.PHASE3 – Reuse Analysis

## Reused Components

| Component | Source | Usage |
|-----------|--------|-------|
| `IArtifactUploadQueue` | AI21.PHASE2 | Sole entry point for storage platform |
| `EnrollmentArtifact` | AI21.PHASE2 | Immutable artifact references |
| `IAITelemetryService` | AI20.PHASE2.6 | Upload duration metrics |
| `IAITracingService` | AI20.PHASE2.6 | Distributed upload tracing |
| `IApplicationDbContext` | Infrastructure | Registry + manifest persistence |
| `BackgroundService` pattern | Worker platform | `ArtifactUploadBackgroundService` |
| Channel queue pattern | AI21.PHASE2 | Same unbounded channel consumer model |

## New Components

| Component | Reason |
|-----------|--------|
| `IArtifactUploadCoordinator` | Orchestrates queue consumption without provider-specific logic |
| `IArtifactStorageProvider` | Storage abstraction; isolates Cloudflare R2 |
| `IR2StorageProvider` | Typed R2 contract; single provider implementation |
| `IArtifactUploadService` | Upload retries, compression, streaming, multipart |
| `IArtifactVerificationService` | Mandatory post-upload verification |
| `IArtifactManifestRepository` | Persist storage manifests separately from enrollment manifests |
| `IArtifactVersionManager` | Immutable version assignment across artifact types |
| `IArtifactLifecycleManager` | Retention/archive/delete isolated from upload |
| `IArtifactIntegrityService` | SHA256 checksum computation and validation |
| `IArtifactReportService` | Upload, verification, storage, lifecycle reports |
| `IArtifactRegistryRepository` | Immutable storage metadata registry |
| `IArtifactVerificationPolicy` | Configurable verification rules |
| `IArtifactRetryPolicy` | Independent exponential backoff retry |
| `IArtifactRetentionPolicy` | Configurable tenant retention rules |
| `ArtifactUploadRequest` | Queue handoff carrying payloads without storage logic in enrollment |
| `ArtifactRegistryEntry` / `ArtifactStorageManifest` | DB entities for metadata only |

## Queue Seam Change

`IArtifactUploadQueue` now carries `ArtifactUploadRequest` (artifact + binary payloads + trace context) instead of references-only `EnrollmentArtifact`. This is the minimal PHASE2→PHASE3 handoff required to upload aligned faces and embeddings without duplicating AI processing or embedding storage logic inside the enrollment pipeline.

Enrollment processing steps (detect, align, embed, quality, duplicate, artifact build) are unchanged.

## Future Provider Roadmap

| Provider | Interface | Status |
|----------|-----------|--------|
| Cloudflare R2 | `IR2StorageProvider` | Implemented |
| AWS S3 | `IArtifactStorageProvider` | Future |
| Azure Blob | `IArtifactStorageProvider` | Future |
| Google Cloud Storage | `IArtifactStorageProvider` | Future |
| MinIO | `IArtifactStorageProvider` | Future |

Register additional providers in DI; coordinator resolves `IArtifactStorageProvider` only.

## Boundaries Preserved

- Recognition engine – unchanged
- Attendance – unchanged
- AI Governance – unchanged
- Enterprise Operations – reused, not duplicated
- Worker framework – unchanged (new hosted service follows existing pattern)
- Enrollment pipeline AI logic – unchanged
- No direct R2 calls from enrollment layer
