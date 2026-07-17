using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Persists attendance decisions and audit metadata — no AI logic (AI20.PHASE2.4).</summary>
public interface IAttendanceResultWriter
{
    Task<AttendancePersistenceResult> PersistAsync(
        AttendancePersistenceRequest request,
        CancellationToken cancellationToken = default);
}
