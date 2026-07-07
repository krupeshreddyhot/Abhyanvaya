# AI Attendance — Phase A3 Architecture Freeze (Prompts S1–S9)

**Date:** 2026-07-01  
**Status:** Final architecture freeze before AI implementation (Phase A3)

---

## Prompt Completion Summary

| Prompt | Deliverable | Status |
|--------|-------------|--------|
| **S1** | `SessionNumber` non-null, default 1, EF + migration | ✅ |
| **S2** | State machine methods on `AttendanceSession`, `DomainException` | ✅ |
| **S3** | Enhanced `RecognitionSnapshotJson` fields | ✅ |
| **S4** | `ImageSequence` on `AttendanceRecognition` | ✅ |
| **S5** | `RecognitionPipelineVersion` on `AttendanceSession` | ✅ |
| **S6** | `FaceImageKey` on `AttendanceRecognition` | ✅ |
| **S7** | `AttendanceSessionAnalyticsDto` + service (session summary first) | ✅ |
| **S8** | Image metadata fields on `AttendanceSession` | ✅ |
| **S9** | Architecture validation (this document) | ✅ |

---

## Aggregate Boundaries

```
AttendanceSession (root)
├── AttendanceRecognition[]     — provisional AI output
├── Attendance[]                  — official rows after approve
└── summary counters              — denormalized for dashboards

AttendanceRecognitionReviewHistory — append-only audit (via recognition)
AttendanceDetail                   — immutable snapshot at materialization
```

---

## State Machine (S2)

```
Draft → Pending → Processing → AwaitingReview → Approved → Completed
Cancel() allowed from any state except Completed
Completed is terminal
```

Services use `Approve()`, `Cancel()`, etc. — never assign `Status` directly.

`AttendanceSessionFinalizer` calls `session.Approve(...)`.

---

## Schema Prepared for A3

| Field | Entity | A3 use |
|-------|--------|--------|
| `RecognitionPipelineVersion` | Session | Track embedding/match pipeline version |
| `FaceImageKey` | Recognition | Cropped face storage path |
| `ImageSequence` | Recognition | Multi-photo classroom sessions |
| `EmbeddingDistance` | Recognition | Vector match distance |
| `RecognitionSnapshotJson` | AttendanceDetail | Immutable attendance evidence |
| `CaptureDevice/Timestamp/Hash/Orientation` | Session | Mobile camera metadata |

---

## SOLID / Clean Architecture

- **Domain:** Entities, enums, `DomainException`, state machine, factory
- **Application:** Services, DTOs, `AttendanceRecognitionMetrics`, `TenantAccessGuard`
- **Infrastructure:** EF configurations, migrations
- **API:** Thin controllers, no direct DbContext in session GET

---

## Tenant Isolation

- `ITenantScoped` on Session + Recognition
- Global query filters + `TenantAccessGuard` on writes

---

## Concurrency

- `RowVersion` on Session + Recognition (API conflict handling deferred post-A3)

---

## Dependency Injection (Application)

```
IAttendanceSessionQueryService
IAttendanceRecognitionReviewService
IAttendanceSessionSummaryService
IAttendanceSessionAnalyticsService
IAttendanceBuilder
IAttendanceSessionFinalizer
```

No unused registrations.

---

## Verdict

**Architecture is frozen and ready for Phase A3 Face Embedding Generation.**

Next implementation steps:
1. `StudentFaceEmbedding` entity + vector storage
2. Embedding generation worker from verified student photos
3. Session AI pipeline: detect faces → match → write recognitions → `AwaitingReview`
4. Populate `RecognitionPipelineVersion`, `FaceImageKey`, `DetectedFaces`, timing fields

---

## Apply Migration

```powershell
dotnet ef database update --project Abhyanvaya.Infrastructure/Abhyanvaya.Infrastructure.csproj --startup-project Abhyanvaya.API/Abhyanvaya.API.csproj
```
