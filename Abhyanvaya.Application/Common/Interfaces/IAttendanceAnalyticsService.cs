using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Future attendance analytics and reporting (AI20.PHASE2.4).</summary>
public interface IAttendanceAnalyticsService
{
    Task<AttendanceAnalyticsSnapshot> BuildSnapshotAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
