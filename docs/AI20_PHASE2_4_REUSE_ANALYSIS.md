# AI20.PHASE2.4 — Classroom Attendance Reuse Analysis

**Milestone:** Pre-implementation review for classroom recognition orchestration and attendance decision framework.

## Existing Components Reused

| Component | Location | Reuse |
|-----------|----------|-------|
| `IRecognitionOrchestrator` | PHASE2.3 | Per-face AI inference — unchanged |
| `IFaceDetectionService` | InsightFace | Face detection only — no vector comparison in attendance layer |
| `IRecognitionMediaService` | Recognition | Thumbnail persistence before recognition row |
| `IAttendanceSessionSummaryService` | Application | Denormalized session counters after persistence |
| `AttendanceSession` state machine | Domain | `MoveToProcessing` / `AwaitingReview` / `Failed` |
| `AttendanceRecognition` entity | Domain | Recognition + review rows |
| `AttendanceBuilder` / `AttendanceSessionFinalizer` | Application | Official attendance at teacher approval — unchanged |
| `IClassroomPhotoQueue` | Infrastructure | Queue completion signaling |
| `IMediaObjectReader` | Infrastructure | Classroom image load |
| Enrollment orchestrator pattern | PHASE2.1.8 | Session orchestrator delegates to subsystems |

## Not Reused (and Why)

| Component | Reason |
|-----------|--------|
| `ClassroomRecognitionPipeline` | Frozen legacy path — new orchestrator is parallel |
| `IFaceMatcher` | Replaced by `IRecognitionOrchestrator` in new path |
| Direct EF in orchestrator | SQL moved to `IAttendanceRecognitionRepository` |
| Vector search / embedding access | Attendance layer never touches AI |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IClassroomRecognitionOrchestrator` | Session-level attendance workflow coordinator |
| `IMultiFaceRecognitionCoordinator` | Multi-face dispatch without attendance decisions |
| `IAttendanceSessionManager` | Session lifecycle and progress tracking |
| `IAttendanceValidationService` | Business validation separate from AI |
| `IAttendanceConflictResolver` | Conflict resolution without recognition |
| `IAttendanceDecisionEngine` | Present/Absent/Late/Unknown decisions |
| `IAttendanceResultWriter` | Attendance decision persistence |
| `IAttendanceRecognitionRepository` | All SQL for recognition + decisions |
| `IAttendancePolicy` | Configurable business rules |
| `IAttendanceConflictStrategy` | Pluggable conflict plugins |
| `IManualReviewService` | Manual review routing (no UI) |
| `IAttendanceAnalyticsService` | Future reporting architecture |
| `AttendanceSessionContext` | Immutable decision input |
| `AttendanceSessionState` | Typed orchestration runtime state |

## Future Attendance Workflow

```
ClassroomPhotoMessage
  → IClassroomRecognitionOrchestrator
  → IMultiFaceRecognitionCoordinator → IRecognitionOrchestrator (×N faces)
  → IAttendanceValidationService
  → IAttendanceConflictResolver
  → IAttendanceDecisionEngine
  → IAttendanceResultWriter
  → AwaitingReview
  → (existing) IAttendanceRecognitionReviewService
  → (existing) IAttendanceSessionFinalizer → Attendance rows
```

## Architecture Boundary

```
Business Layer (PHASE2.4)          AI Layer (PHASE2.3 — frozen)
─────────────────────────          ─────────────────────────────
ClassroomRecognitionOrchestrator → IRecognitionOrchestrator
MultiFaceCoordinator             → (per face)
Validation / Conflict / Decision → (never vectors)
ResultWriter / Repository        → (never embeddings)
```

No enrollment modifications. No worker modifications. No recognition engine modifications.
