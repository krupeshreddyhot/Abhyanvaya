using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Read-only queries for attendance session review context.
/// </summary>
public interface IAttendanceSessionQueryService
{
    Task<AttendanceSessionReviewDto?> GetSessionForReviewAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<AttendanceSessionStatusDto?> GetSessionStatusAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<FinalizationStatusDto?> GetFinalizationStatusAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<AttendanceSessionReportDto?> GetSessionReportAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntryDto>> GetSessionAuditEntriesAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
