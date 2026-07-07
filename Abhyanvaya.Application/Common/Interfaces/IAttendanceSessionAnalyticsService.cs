using Abhyanvaya.Application.DTOs.Attendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Computes read-only session analytics for reporting dashboards.
/// </summary>
public interface IAttendanceSessionAnalyticsService
{
    /// <summary>
    /// Returns recognition and attendance metrics for the specified session.
    /// </summary>
    Task<AttendanceSessionAnalyticsDto> GetSessionAnalyticsAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
