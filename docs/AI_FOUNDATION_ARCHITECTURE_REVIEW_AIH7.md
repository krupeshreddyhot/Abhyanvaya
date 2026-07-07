# AI Foundation Architecture Review (AIH7)

**Project:** Abhyanvaya — Milestone 2 AI Foundation Hardening (AIH1–AIH7)  
**Date:** 2026-07-02  
**Reviewer:** Chief Solution Architect  
**Verdict:** **APPROVED** for AI6 — InsightFace / ONNX Runtime integration

---

## Executive Summary

The embedding subsystem has been refactored from a monolithic background worker into a clean, provider-agnostic pipeline with explicit factory resolution, separated lifecycle/quality concerns, retry policy, metadata tracking, and structured diagnostics. No AI provider implementations were added. All layers compile and existing API routes remain unchanged.

---

## Architecture Assessment

### Clean Architecture & SOLID

| Component | Layer | Assessment |
|-----------|-------|------------|
| `StudentFaceEmbedding` | Domain | Pure entity; lifecycle and quality separated |
| `IEmbeddingProviderFactory` | Application | Interface segregation; factory resolves providers |
| `IEmbeddingPipeline` | Application | Single orchestration entry point |
| `IEmbeddingValidator` / `IEmbeddingNormalizer` | Application | Open/closed — swap implementations without pipeline changes |
| `IEmbeddingStorage` | Application | Persistence abstraction; worker does not touch DbContext |
| `EmbeddingProviderFactory` | Infrastructure | Concrete DI registration of `IEmbeddingGenerator` implementations |
| `EmbeddingPipeline` | Infrastructure | Coordinates provider → validate → normalize → store |
| `StudentFaceEmbeddingBackgroundService` | Infrastructure | Thin — delegates exclusively to `IEmbeddingPipeline` |
| `StudentFaceEmbeddingService` | Application | API orchestration only (enqueue, status, deactivate) |

**Dependency rule:** Domain has no infrastructure references. Application defines abstractions. Infrastructure implements them.

### Provider Abstraction (AIH1)

- Replaced `IEnumerable<IEmbeddingGenerator>` + `First()` with `IEmbeddingProviderFactory`.
- `GetProvider(name)` throws `NotSupportedException` for unknown providers.
- `GetDefaultProvider()` supports `Embedding:DefaultProvider` configuration or single-provider auto-select.
- Provider names align with `EmbeddingProviders` constants (InsightFace, FaceNet, AzureFace, OpenCV).

### Pipeline Design (AIH5)

```
Background Worker → IEmbeddingPipeline → Provider → Validator → Normalizer → Storage
```

The background worker contains no generation, validation, or persistence logic.

### Metadata (AIH2)

| Field | Purpose |
|-------|---------|
| `EmbeddingDimension` | Stored vector length (512, 768, 1024, …) |
| `PhotoVersion` | `Student.PhotoUploadedUtc.Ticks` at generation time — detects stale embeddings |

Status DTO exposes `IsPhotoVersionStale` when the active embedding predates the current photo.

### Lifecycle vs Quality (AIH3)

| Concern | Enum | Values |
|---------|------|--------|
| Lifecycle | `EmbeddingStatus` | Pending → Processing → Completed / Failed; Deactivate → Inactive |
| Quality | `EmbeddingQuality` | Unknown, Poor, Fair, Good, Excellent |

Quality is no longer used to represent job progress.

### Retry Policy (AIH4)

- `RetryCount`, `LastFailureUtc`, `LastFailureReason` (varchar 500)
- Maximum 3 attempts per pipeline run
- Success resets retry counters
- After 3 failures: `EmbeddingStatus = Failed`

### Observability (AIH6)

Structured logging via `ILogger<T>` includes:

- Provider, Model, EmbeddingDimension
- GenerationDurationMs, NormalizationDurationMs
- ValidationResult, RetryCount

In-process metrics via `IEmbeddingGenerationMetrics`:

- Successful / failed embedding counts
- Average generation time
- Average retries

Ready for OpenTelemetry export without Prometheus dependency.

---

## API & UI Review

### APIs (unchanged routes)

| Method | Route | Status |
|--------|-------|--------|
| GET | `/api/student/{id}/embeddings/status` | Extended DTO fields (backward compatible) |
| GET | `/api/student/{id}/embeddings` | Extended DTO fields |
| POST | `/api/student/{id}/embeddings/generate` | Unchanged |
| POST | `/api/student/{id}/embeddings/regenerate` | Unchanged |
| POST | `/api/student/{id}/embeddings/{embeddingId}/deactivate` | Sets `Inactive` status |

### UI

`StudentEmbeddingPanel` displays lifecycle status chip, quality chip, dimensions, photo-version staleness warning, and retry count.

---

## Dead Code Removed

- `StoreGeneratedEmbeddingAsync` / `MarkGenerationPendingAsync` removed from `IStudentFaceEmbeddingService` (moved to `IEmbeddingStorage`)
- Background worker no longer resolves `IEnumerable<IEmbeddingGenerator>` directly

---

## Performance Considerations

- Pipeline retries are in-process (no re-queue overhead)
- Validator and normalizer operate on in-memory float arrays — O(n) per vector
- Storage deactivates prior active rows in a single transaction before insert/update
- Filtered unique index on `(StudentId, IsActive)` preserved

---

## AI6 Readiness Checklist

| Requirement | Ready |
|-------------|-------|
| Register `IEmbeddingGenerator` implementation | Yes — via DI + factory |
| Provider name constant (`EmbeddingProviders.InsightFace`) | Yes |
| Pipeline handles validate + normalize + store | Yes |
| Dimension metadata populated automatically | Yes |
| Retry on transient ONNX failures | Yes (3 attempts) |
| No business logic changes required for AI6 | Yes |

### Recommended AI6 Implementation Steps

1. Create `InsightFaceEmbeddingGenerator : IEmbeddingGenerator` in Infrastructure
2. Register as `services.AddSingleton<IEmbeddingGenerator, InsightFaceEmbeddingGenerator>()`
3. Set `Embedding:DefaultProvider = "InsightFace"` in `appsettings.json`
4. Return `ExpectedDimension = 512` in `EmbeddingGenerationResult`
5. Map ONNX confidence score to `EmbeddingQuality` (Poor–Excellent)

---

## Approval

The AI Foundation architecture is **frozen and approved**. Proceed with **AI6 — InsightFace / ONNX Runtime** provider implementation.
