using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Approves an attendance session after validation and attendance materialization.
/// </summary>
/// <remarks>
/// Finalization runs inside a single database transaction. Optimistic concurrency conflicts surface as
/// <see cref="ConcurrencyConflictException"/>.
/// </remarks>
public interface IAttendanceSessionFinalizer
{
    /// <summary>
    /// Validates review completeness, builds official attendance, and marks the session approved.
    /// </summary>
    Task<AttendanceBuildSummaryDto> FinalizeAttendanceSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
