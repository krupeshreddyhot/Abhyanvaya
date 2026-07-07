# AI Attendance Module — Final Architecture Review (A3 Readiness)

**Review date:** 2026-07-01  
**Scope:** Phase A2 AI attendance (session → recognition → review → materialize)  
**Next phase:** A3 Face Embedding Generation

---

## Executive Summary

The AI Attendance module follows **Clean Architecture** with clear aggregate boundaries, tenant isolation, and a separation between **provisional AI output** (`AttendanceRecognition`) and **official college attendance** (`Attendance` + `AttendanceDetail`).

This review consolidated duplicate logic, removed redundant infrastructure, and documented the architecture for Phase A3 without changing runtime behaviour.

---

## Layer Responsibilities

| Layer | Responsibility |
|-------|----------------|
| **Domain** | Entities, enums, validation partials, `RecognitionSnapshot` value model |
| **Application** | Review workflow, summary sync, analytics, builder, finalizer, DTOs |
| **Infrastructure** | EF Core configurations, migrations, global tenant filters |
| **API** | Thin controllers delegating to application services |

**Dependency rule:** Application references Domain only. API references Application + Infrastructure via DI.

---

## DDD Aggregate Boundaries

### `AttendanceSession` (Aggregate Root)

- Owns academic context (course, group, semester, subject, date)
- Owns session lifecycle (`Draft` → `AwaitingReview` → `Approved`)
- Owns AI pipeline metadata (`RecognitionProvider`, `RecognitionModel`, image keys, timing)
- Owns denormalized summary counters (synced by `AttendanceSessionSummaryService`)
- Validation rules in `AttendanceSession.Validation.cs`

**Child entities:** `AttendanceRecognition` (cascade delete)

### `AttendanceRecognition` (Entity within Session aggregate)

- **Not** official attendance — provisional AI output only
- Teacher review mutates status via `AttendanceRecognitionReviewService`
- Append-only audit via `AttendanceRecognitionReviewHistory`
- `EmbeddingDistance` reserved for Phase A3 matching

### Official Attendance (Separate aggregate)

- `Attendance` + `AttendanceDetail` created only by `AttendanceBuilder` after full teacher review
- `RecognitionSnapshotJson` on `AttendanceDetail` preserves immutable AI context at materialization time

---

## Service Map (Post-Review)

```
AttendanceRecognitionReviewService
  ├── Review / batch / reset
  ├── Append AttendanceRecognitionReviewHistory
  └── SyncSessionSummaryAsync

AttendanceSessionFinalizer
  ├── Validate all recognitions reviewed
  ├── AttendanceBuilder.BuildAsync
  └── Mark session Approved

AttendanceBuilder
  ├── SyncSessionSummaryAsync
  ├── Roster from StudentSubjects (no Subject entity query)
  └── Create Attendance + AttendanceDetail (with RecognitionSnapshot)

AttendanceSessionSummaryService
  └── AttendanceRecognitionMetrics → session counters

AttendanceSessionAnalyticsService
  └── AttendanceRecognitionMetrics + Attendance counts → reporting DTO

AttendanceSessionQueryService
  └── Read-only session context for review UI
```

### Consolidations Applied

| Before | After |
|--------|-------|
| Duplicate status counting in Summary + Analytics + Builder | Shared `AttendanceRecognitionMetrics` |
| Duplicate `EnsureTenantAccess` in Finalizer + ReviewService | Shared `TenantAccessGuard` |
| Duplicate `SyncSessionSummaryAsync` on finalize path | Single call in `AttendanceBuilder` only |
| Controller queried `IApplicationDbContext` directly | `IAttendanceSessionQueryService` |
| Redundant index `IX_AttendanceRecognitionReviewHistory_RecognitionId` | Removed (covered by composite index) |

---

## SOLID Assessment

| Principle | Status | Notes |
|-----------|--------|-------|
| **S** Single Responsibility | ✅ | Builder materializes; Finalizer validates + approves; ReviewService handles teacher actions |
| **O** Open/Closed | ✅ | New AI providers extend session metadata without changing review workflow |
| **L** Liskov | ✅ | All services implement narrow interfaces |
| **I** Interface Segregation | ✅ | Separate interfaces for build, finalize, review, summary, analytics, query |
| **D** Dependency Inversion | ✅ | Services depend on `IApplicationDbContext`, `ICurrentUserService` abstractions |

---

## Multi-Tenancy

- `AttendanceSession` and `AttendanceRecognition` implement `ITenantScoped`
- Global query filter in `ApplicationDbContext` scopes reads by tenant
- `TenantAccessGuard` enforces explicit tenant checks in write services
- `AttendanceRecognitionReviewHistory` inherits tenant scope via recognition join

---

## Concurrency

- `RowVersion` configured on `AttendanceSession` and `AttendanceRecognition`
- **Future A3/A4 work:** Expose `RowVersion` in review DTOs and handle `DbUpdateConcurrencyException` for concurrent teacher reviews

---

## EF Configuration Checklist

| Entity | Table | Key indexes | Concurrency |
|--------|-------|-------------|-------------|
| AttendanceSession | `AttendanceSession` | Tenant+Date+Status, Tenant+Subject+Date, Tenant+Context+SessionNumber | RowVersion |
| AttendanceRecognition | `AttendanceRecognition` | Session+FaceNumber (unique), Tenant+Session+Status | RowVersion |
| AttendanceRecognitionReviewHistory | `AttendanceRecognitionReviewHistory` | RecognitionId+ReviewedUtc, ReviewedBy | None (append-only) |
| AttendanceDetail | `AttendanceDetail` | AttendanceId (unique), AttendanceRecognitionId (unique filtered) | — |

---

## API Surface

| Method | Route | Service |
|--------|-------|---------|
| GET | `/api/attendance-sessions/{id}` | `IAttendanceSessionQueryService` |
| GET | `/api/attendance-sessions/{id}/analytics` | `IAttendanceSessionAnalyticsService` |
| POST | `/api/attendance-sessions/{id}/finalize` | `IAttendanceSessionFinalizer` |
| GET | `/api/attendance-sessions/{id}/recognitions` | `IAttendanceRecognitionReviewService` |
| POST | `/api/attendance-recognition/review` | ReviewService |
| POST | `/api/attendance-recognition/review-batch` | ReviewService |
| DELETE | `/api/attendance-recognition/{id}/reset` | ReviewService |
| GET | `/api/attendance-recognition/{id}/review-history` | ReviewService |
| GET | `/api/attendance-sessions/{id}/recognition-review-history` | ReviewService |

---

## Phase A3 — Face Embedding Generation (Readiness)

### Retain unchanged

| Component | A3 role |
|-----------|---------|
| `Student.PhotoKey` / `PhotoVerified` | Source images for embedding generation |
| `AttendanceRecognition.EmbeddingDistance` | Store match distance from vector comparison |
| `AttendanceSession.RecognitionProvider/Model` | Track embedding model version |
| `AttendanceSessionSummaryService` | Sync counters after AI pipeline writes recognitions |
| Full A2 review → finalize stack | Consumes A3 match output unchanged |

### Add in A3 (not yet implemented)

| Component | Purpose |
|-----------|---------|
| `StudentFaceEmbedding` entity (proposed) | Store vector, model version, `StudentId`, tenant |
| Embedding generation worker/service | Process verified student photos → embeddings |
| Session AI pipeline API | Create session, detect faces, write recognitions, set `DetectedFaces`, transition to `AwaitingReview` |
| Populate `DetectedFaces`, `ProcessingMilliseconds`, `StartedUtc`/`CompletedUtc` | Session timing and face counts from pipeline |

### Known deferred items (non-blocking for A3)

- Concurrency conflict UX for concurrent teacher reviews
- Analytics and review-history UI dashboards
- Legacy fields `RecognizedFaces`/`UnknownFaces` (superseded by `*Count` fields; pipeline may populate or deprecate)
- Additional index tuning after production query profiling

---

## Workflow Diagram

```
[A3 Pipeline]  Student photos → embeddings → face detect → AttendanceRecognition rows
                              ↓
[A2 Review]    Teacher Review (RecognitionReviewService)
                              ↓
[A2 Finalize]  AttendanceSessionFinalizer → AttendanceBuilder → Attendance + AttendanceDetail
                              ↓
               Session.Status = Approved (immutable official attendance)
```

---

## Files Modified in This Review

### Created
- `Abhyanvaya.Application/Internal/AttendanceRecognitionMetrics.cs`
- `Abhyanvaya.Application/Internal/TenantAccessGuard.cs`
- `Abhyanvaya.Application/AttendanceSessionQueryService.cs`
- `Abhyanvaya.Application/Common/Interfaces/IAttendanceSessionQueryService.cs`
- `docs/AI_ATTENDANCE_ARCHITECTURE_REVIEW_A3_READINESS.md`

### Modified
- `Abhyanvaya.Application/AttendanceSessionSummaryService.cs`
- `Abhyanvaya.Application/AttendanceSessionAnalyticsService.cs`
- `Abhyanvaya.Application/AttendanceSessionFinalizer.cs`
- `Abhyanvaya.Application/AttendanceBuilder.cs`
- `Abhyanvaya.Application/AttendanceRecognitionReviewService.cs`
- `Abhyanvaya.Application/DependencyInjection.cs`
- `Abhyanvaya.Application/Common/Interfaces/*.cs` (XML docs)
- `Abhyanvaya.Application/DTOs/Attendance/AttendanceBuildSummaryDto.cs`
- `Abhyanvaya.API/Controllers/AttendanceSessionController.cs`
- `Abhyanvaya.Infrastructure/Persistence/Configurations/AttendanceRecognitionReviewHistoryConfiguration.cs`
- Migration: `RemoveRedundantReviewHistoryRecognitionIdIndex`

---

## Verdict

**Architecture is ready for Phase A3.** The module maintains clean boundaries, consolidated shared logic, and explicit extension points for embedding storage and AI pipeline integration without refactoring the A2 review workflow.
