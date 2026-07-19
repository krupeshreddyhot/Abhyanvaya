using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Resolves attendance conflicts without performing recognition (AI20.PHASE2.4).</summary>
public interface IAttendanceConflictResolver
{
    AttendanceConflictResolutionResult Resolve(AttendanceSessionContext context);
}
