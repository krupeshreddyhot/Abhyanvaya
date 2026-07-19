using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Events;

/// <summary>Raised when an attendance session is approved and official attendance is materialized.</summary>
public sealed record AttendanceApprovedEvent(
    Guid AttendanceSessionId,
    int TenantId,
    DateTime ApprovedUtc) : DomainEventBase;

/// <summary>Raised when an approved attendance session reaches its terminal completed state.</summary>
public sealed record AttendanceCompletedEvent(
    Guid AttendanceSessionId,
    int TenantId,
    DateTime CompletedUtc) : DomainEventBase;

/// <summary>Raised when an attendance session is cancelled.</summary>
public sealed record AttendanceCancelledEvent(
    Guid AttendanceSessionId,
    int TenantId,
    DateTime CancelledUtc) : DomainEventBase;

// AI20.PHASE2.4 orchestration events (event-ready; not externally published yet).

public sealed record AttendanceSessionStarted(
    Guid SessionId,
    int TenantId,
    Guid CorrelationId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record SessionRecognitionCompleted(
    Guid SessionId,
    Guid CorrelationId,
    int FaceCount,
    TimeSpan Duration,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ValidationCompleted(
    Guid SessionId,
    Guid CorrelationId,
    int ValidCount,
    int InvalidCount,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ConflictResolved(
    Guid SessionId,
    Guid CorrelationId,
    int ConflictCount,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record AttendanceWritten(
    Guid SessionId,
    Guid CorrelationId,
    int DecisionCount,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ClassroomOrchestrationCompleted(
    Guid SessionId,
    Guid CorrelationId,
    AttendanceSessionState FinalState,
    TimeSpan TotalDuration,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ManualReviewRequired(
    Guid SessionId,
    Guid CorrelationId,
    int FaceIndex,
    string Reason,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record UnknownFaceDetected(
    Guid SessionId,
    Guid CorrelationId,
    int FaceIndex,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
