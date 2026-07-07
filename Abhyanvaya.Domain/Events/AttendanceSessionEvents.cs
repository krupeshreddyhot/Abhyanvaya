using Abhyanvaya.Domain.Common;

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
