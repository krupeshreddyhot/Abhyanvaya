using Abhyanvaya.Application.DTOs.Attendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Materializes official attendance rows from teacher-reviewed AI recognitions.
/// </summary>
public interface IAttendanceBuilder
{
    /// <summary>
    /// Creates <see cref="Domain.Entities.Attendance"/> and <see cref="Domain.Entities.AttendanceDetail"/> rows
    /// for a fully reviewed session and stages them on the current unit of work.
    /// </summary>
    /// <remarks>
    /// Does not persist changes. <see cref="IAttendanceSessionFinalizer"/> commits staged rows atomically.
    /// </remarks>
    Task<AttendanceBuildSummaryDto> BuildAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
