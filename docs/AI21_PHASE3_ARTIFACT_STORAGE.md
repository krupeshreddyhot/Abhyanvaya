# AI21.PHASE3 – Enterprise Artifact Management Platform

## Overview

AI21.PHASE3 consumes enrollment artifacts from AI21.PHASE2 via `IArtifactUploadQueue`, stores them securely in Cloudflare R2 (through `IArtifactStorageProvider`), verifies every upload, persists immutable registry metadata, and manages artifact lifecycle.

Enrollment pipeline logic is unchanged except for the queue handoff seam, which now passes binary payloads alongside immutable artifact references.

## Architecture

```
IArtifactUploadQueue (PHASE2 producer)
        ↓
ArtifactUploadBackgroundService
        ↓
IArtifactUploadCoordinator
        ↓
IArtifactUploadService → IArtifactStorageProvider → IR2StorageProvider
        ↓
IArtifactVerificationService (policy-driven)
        ↓
IArtifactRegistryRepository + IArtifactManifestRepository
        ↓
IArtifactLifecycleManager (retention / archive / delete)
        ↓
IArtifactReportService
```

## Upload Flow

1. PHASE2 enqueues `ArtifactUploadRequest` (artifact references + aligned face + embedding + original photo bytes).
2. Background worker dequeues and coordinator builds upload items:
   - Original photo
   - Aligned face
   - Embedding binary
   - Metadata JSON
   - Quality report JSON
3. Each item is uploaded with retry (`IArtifactRetryPolicy`), optional compression, and multipart support for large files.
4. Verification runs before marking verified (`IArtifactVerificationPolicy`).
5. Registry metadata and manifest are persisted (no binary data in DB).
6. Domain events emitted (`ArtifactUploaded`, `ArtifactVerified`, etc.).

## Storage Providers

| Interface | Implementation | Notes |
|-----------|----------------|-------|
| `IArtifactStorageProvider` | Abstraction | Provider-agnostic upload/verify/delete |
| `IR2StorageProvider` | `R2StorageProvider` | **Only** Cloudflare R2 implementation |

Future providers (AWS S3, Azure Blob, GCS, MinIO) implement `IArtifactStorageProvider` without changing coordinator logic.

## Configuration

```json
"ArtifactStorage": {
  "Provider": "r2",
  "Bucket": "abhyanvaya-artifacts",
  "KeyPrefix": "artifacts",
  "MultipartThresholdBytes": 5242880,
  "EnableCompression": false,
  "MaxParallelUploads": 16
},
"ArtifactStorage:R2": {
  "Endpoint": "https://<account>.r2.cloudflarestorage.com",
  "AccessKeyId": "...",
  "SecretAccessKey": "...",
  "ForcePathStyle": true
}
```

## Verification

Mandatory post-upload verification:

- SHA256 checksum
- Content length
- Metadata completeness
- Version presence
- Remote object existence

Configured via `ArtifactVerificationPolicy` section.

## Versioning

`IArtifactVersionManager` assigns immutable versions for:

- Artifact version
- Embedding version
- Recognition version
- Enrollment version
- Manifest version
- Retention version

## Lifecycle

`IArtifactLifecycleManager` applies `IArtifactRetentionPolicy`:

- Keep forever
- Archive after N days
- Delete after N days
- Version cleanup

Active (`Verified`) artifacts are never deleted.

## Thread Safety

All services are stateless and safe for concurrent uploads. Coordinator supports parallel background processing and large batches.

## Observability

Reuses AI20.PHASE2.6 Enterprise Operations:

- `IAITelemetryService` – upload duration metrics
- `IAITracingService` – upload spans
- Structured logging (never logs images, embeddings, or PII)

## Completion Checklist

Before marking upload complete:

- [x] Upload successful
- [x] Checksum matches
- [x] Metadata stored in registry
- [x] Version assigned
- [x] Manifest updated
- [x] Verification passed
- [x] Artifact event published
- [x] Ready for production storage
