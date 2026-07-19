# AI20.PHASE2.4 — Classroom Recognition Orchestration & Attendance Decision Framework

**Milestone:** AI20.PHASE2.4 — convert recognition results into attendance decisions.

## Objective

Orchestrate classroom attendance processing: recognition results → validation → conflict resolution → attendance decision → persistence — **without modifying** enrollment, recognition engine, workers, or vector search.

```
Classroom Image
    ↓ Face Detection
    ↓ IMultiFaceRecognitionCoordinator → IRecognitionOrchestrator (per face)
    ↓ IAttendanceValidationService
    ↓ IAttendanceConflictResolver
    ↓ IAttendanceDecisionEngine
    ↓ IAttendanceResultWriter
    ↓ AttendanceSessionResult (AwaitingReview)
```

## Architecture

```mermaid
flowchart TD
    CO[IClassroomRecognitionOrchestrator]
    SM[IAttendanceSessionManager]
    MFC[IMultiFaceRecognitionCoordinator]
    RO[IRecognitionOrchestrator]
    VAL[IAttendanceValidationService]
    CR[IAttendanceConflictResolver]
    DE[IAttendanceDecisionEngine]
    RW[IAttendanceResultWriter]
    REPO[IAttendanceRecognitionRepository]

    CO --> SM
    CO --> MFC
    MFC --> RO
    CO --> VAL
    CO --> CR
    CO --> DE
    CO --> RW
    RW --> REPO
```

## Sequence Diagram

```mermaid
sequenceDiagram
    participant O as ClassroomOrchestrator
    participant M as SessionManager
    participant D as FaceDetection
    participant C as MultiFaceCoordinator
    participant R as RecognitionOrchestrator
    participant V as Validation
    participant X as ConflictResolver
    participant E as DecisionEngine
    participant W as ResultWriter

    O->>M: BeginProcessing
    O->>D: DetectAsync
    loop Each face
        C->>R: RecognizeAsync
        R-->>C: RecognitionResult
    end
    O->>V: Validate
    O->>X: Resolve
    O->>E: Decide
    O->>W: PersistAsync
    O->>M: CompleteProcessing (AwaitingReview)
```

## Attendance Flow

| Step | Service | Output |
|------|---------|--------|
| Session load | `IAttendanceSessionManager` | `AttendanceSessionMetadata` |
| Face detection | `IFaceDetectionService` | `DetectedFaceDto[]` |
| Per-face recognition | `IMultiFaceRecognitionCoordinator` | `FaceRecognitionOutcome[]` |
| Validation | `IAttendanceValidationService` | Valid outcomes |
| Conflict resolution | `IAttendanceConflictResolver` | Resolved outcomes + conflicts |
| Decision | `IAttendanceDecisionEngine` | `AttendanceDecision[]` |
| Persistence | `IAttendanceResultWriter` | Updated recognitions + summary |

## Conflict Flow

| Conflict | Strategy |
|----------|----------|
| Duplicate student | `HighestConfidenceConflictStrategy` |
| Unknown face | `ManualReviewConflictStrategy` |
| Borderline confidence | `ManualReviewConflictStrategy` |
| Duplicate face | `ManualReviewConflictStrategy` |

## Session Lifecycle

| State | Meaning |
|-------|---------|
| `Created` | Session context initialized |
| `Detecting` | Loading image + face detection |
| `Recognizing` | Multi-face recognition dispatch |
| `Validating` | Business rule validation |
| `ResolvingConflicts` | Conflict resolution |
| `WritingAttendance` | Persisting decisions |
| `Completed` | Session moved to AwaitingReview |
| `Failed` / `Cancelled` | Terminal error states |

## Configuration

```json
{
  "ClassroomAttendance": {
    "MinimumConfidence": 55,
    "RequireTeacherApproval": true,
    "ManualReviewEnabled": true,
    "LateArrivalThreshold": "00:15:00",
    "AllowDuplicateStudents": false,
    "AllowReRecognition": true,
    "UnknownFaceThreshold": 45,
    "DefaultTopK": 10,
    "PipelineVersion": 1
  }
}
```

## Testing

| Test | Coverage |
|------|----------|
| `DecisionEngine_MarksPresent_WhenRecognizedWithConfidence` | Present decision |
| `DecisionEngine_MarksUnknown_WhenNoRecognitionDecision` | Unknown face |
| `DecisionEngine_MarksDuplicate_WhenSameStudentTwice` | Duplicate student |
| `ConflictResolver_DetectsDuplicateStudent` | Conflict detection |
| `ValidationService_RejectsEmptyOutcomes` | Validation rules |
| `MultiFaceCoordinator_DispatchesRecognitionPerFace` | Multi-face + ordering |
| `ManualReviewService_FlagsLowConfidence` | Manual review |

## Files Created

See reuse analysis. Key additions:

- Application: 11 interfaces + `ClassroomAttendanceModels.cs`
- Domain: `AttendanceSessionState`, `AttendanceSessionEvents`
- Infrastructure: Orchestrator, session manager, validation, conflict, decision, coordinator, repository, writer, analytics
- Tests: `ClassroomAttendanceFrameworkTests.cs`

## Files Modified

| File | Change |
|------|--------|
| `DependencyInjection.cs` | Classroom attendance DI registrations |

## Verification Checklist

- [x] `IRecognitionOrchestrator` unchanged
- [x] Recognition engine focused on AI inference only
- [x] Validation, conflict, decision, persistence isolated
- [x] Multi-face coordination independent of attendance policy
- [x] Business rules interface-driven (`IAttendancePolicy`)
- [x] Session context immutable
- [x] No attendance component accesses embeddings/vector search
- [x] All services stateless and independently testable
- [x] `ClassroomRecognitionPipeline` unchanged
- [x] Background workers unchanged

## Migration Note

`IClassroomRecognitionOrchestrator` is registered and ready. Wire into `ClassroomRecognitionBackgroundService` behind a feature flag in a future phase.
