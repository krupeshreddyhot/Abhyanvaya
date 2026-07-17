# AI20.PHASE2.1.7 — Result Writer Reuse Analysis

**Milestone:** Pre-implementation review for `IEnrollmentResultWriter`.

## Existing Components Reused

| Component | Location | Reuse |
|-----------|----------|-------|
| `StudentFaceEmbedding` entity | `Domain/Entities/StudentFaceEmbedding.cs` | Persist embedding vector + metadata |
| `StudentEnrollmentItem` | `Domain/Entities/StudentEnrollmentItem.cs` | Link embedding, update status to Completed |
| `StudentEnrollmentBatch` | `Domain/Entities/StudentEnrollmentBatch.cs` | Counter transition Embedding → Completed |
| `Student` | `Domain/Entities/Student.cs` | Update `PhotoKey` / `PhotoUploadedUtc` |
| `IUnitOfWork.ExecuteInTransactionAsync` | `ApplicationDbContext.UnitOfWork.cs` | Single transaction boundary |
| `EnrollmentBatchCounterRules` | `Application/Enrollment/Progress/` | Batch counter updates |
| `EnrollmentStatusTransitionRules` | `Application/Enrollment/Progress/` | Embedding → Completed validation |
| `EmbeddingStorage.DeactivateActiveEmbeddings` pattern | `Infrastructure/Embedding/EmbeddingStorage.cs` | Deactivate prior active embeddings |
| `EnrollmentEmbeddingArtifact` | `Application/Enrollment/Embedding/` | Immutable input from embedding service |
| `ICurrentUserService` | Existing | `GeneratedBy` / audit user |
| `DomainEventBase` pattern | `Domain/Events/` | Event-ready domain events |

## Not Reused (and Why)

| Component | Reason |
|-----------|--------|
| `IEmbeddingStorage` directly | Tied to `StudentPhotoUploadedMessage` manual upload path |
| `IStudentEnrollmentItemRepository` in writer | Consolidated into `IEnrollmentPersistenceRepository` per architect spec |
| `IEnrollmentProgressReporter` | Result writer owns terminal persistence transition in same transaction |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IEnrollmentResultWriter` | Sole persistence orchestrator for enrollment embeddings |
| `IEnrollmentPersistenceRepository` | Single SQL abstraction — writer must not call multiple repositories |
| `IEnrollmentPersistencePolicy` | Centralized persistence rules (overwrite, duplicates, history) |
| `IEnrollmentDuplicateDetector` | Metadata-only idempotency without vector comparison |
| `EnrollmentEmbeddingVersionSnapshot` | Immutable reproducibility record |
| `EnrollmentPersistenceAudit` | Compliance-ready immutable audit trail |
| `EmbeddingPersisted` / `EmbeddingPersistenceFailed` | Event-ready domain events (not published externally) |
| `EnrollmentPersistenceResult` | Result pattern output with telemetry + statistics |

## Methods Reused

| Logic | Source |
|-------|--------|
| Deactivate active embeddings | `EmbeddingStorage.DeactivateActiveEmbeddingsAsync` pattern |
| Batch counter transition | `EnrollmentBatchCounterRules.ApplyTransition` |
| Status transition guard | `EnrollmentStatusTransitionRules.EnsureAllowed` |
| Quality mapping | Same thresholds as embedding service advisory score |

## Architecture Boundary

```
EnrollmentEmbeddingArtifact (immutable input)
        ↓
IEnrollmentResultWriter (workflow)
        ↓
IEnrollmentPersistenceRepository (SQL)
        ↓
StudentFaceEmbedding + VersionSnapshot + Audit + Item + Batch + Student
```

No embedding generation, validation, storage, or artifact resolution in this layer.
