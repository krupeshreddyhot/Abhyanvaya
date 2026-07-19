using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Attendance recognition data access — all SQL behind this repository (AI20.PHASE2.4).</summary>
public interface IAttendanceRecognitionRepository
{
    Task ReplaceSessionRecognitionsAsync(
        Guid sessionId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<int> ApplyAttendanceDecisionsAsync(
        IReadOnlyList<AttendanceDecision> decisions,
        CancellationToken cancellationToken = default);

    Task UpdateSessionCountersAsync(
        AttendanceSession session,
        AttendanceSessionStatistics statistics,
        CancellationToken cancellationToken = default);
}
