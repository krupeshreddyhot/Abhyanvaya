using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Manages attendance session lifecycle and progress tracking (AI20.PHASE2.4).</summary>
public interface IAttendanceSessionManager
{
    Task<AttendanceSession> LoadSessionAsync(Guid sessionId, int tenantId, CancellationToken cancellationToken = default);

    Task BeginProcessingAsync(AttendanceSession session, CancellationToken cancellationToken = default);

    Task CompleteProcessingAsync(AttendanceSession session, AttendanceSessionStatistics statistics, CancellationToken cancellationToken = default);

    Task FailProcessingAsync(AttendanceSession session, string error, CancellationToken cancellationToken = default);

    AttendanceSessionMetadata CreateMetadata(AttendanceSession session, string? imageStorageKey);
}
