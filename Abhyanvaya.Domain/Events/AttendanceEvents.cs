using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Domain.Events;

/// <summary>
/// Raised when a faculty member saves manual attendance for a subject and reporting day.
/// </summary>
public sealed record AttendanceMarkedEvent(
    int TenantId,
    int SubjectId,
    AttendanceDay AttendanceDay,
    int StudentCount,
    AttendanceMethod CaptureMethod,
    int? MarkedByUserId) : DomainEventBase;

/// <summary>
/// Raised when the AI recognition pipeline materializes official <c>Attendance</c> rows
/// (present + absent) for an <see cref="Entities.AttendanceSession"/>.
/// </summary>
public sealed record AttendanceGeneratedFromAIEvent(
    Guid AttendanceSessionId,
    int TenantId,
    int SubjectId,
    AttendanceDay AttendanceDay,
    int StudentCount,
    int PresentCount,
    int AbsentCount,
    AttendanceMethod CaptureMethod) : DomainEventBase;

/// <summary>
/// Raised when an attendance session's recognition review is finalized and its attendance
/// becomes official (session approved). Capture-method agnostic: any workflow that reaches
/// finalization through <c>AttendanceSessionFinalizer</c> raises this event.
/// </summary>
public sealed record AttendanceFinalizedEvent(
    Guid AttendanceSessionId,
    int TenantId,
    int SubjectId,
    AttendanceDay AttendanceDay,
    int StudentCount,
    int PresentCount,
    int AbsentCount,
    AttendanceMethod CaptureMethod) : DomainEventBase;

/// <summary>
/// Raised when attendance records for a subject and reporting day are locked against further edits.
/// </summary>
public sealed record AttendanceLockedEvent(
    int TenantId,
    int SubjectId,
    AttendanceDay AttendanceDay,
    int StudentCount,
    int? LockedByUserId) : DomainEventBase;

/// <summary>
/// Raised when previously locked attendance records for a subject and reporting day are unlocked.
/// </summary>
/// <remarks>
/// Defined for the domain model now so downstream consumers (audit log, notifications) have a
/// stable contract to depend on. No production code path currently unlocks attendance—there is no
/// "unlock" API endpoint today—so this event is not yet published anywhere. Wire it up from the
/// unlock endpoint/service when that capability is added; do not add an endpoint solely to raise it.
/// </remarks>
public sealed record AttendanceUnlockedEvent(
    int TenantId,
    int SubjectId,
    AttendanceDay AttendanceDay,
    int StudentCount,
    int? UnlockedByUserId) : DomainEventBase;
