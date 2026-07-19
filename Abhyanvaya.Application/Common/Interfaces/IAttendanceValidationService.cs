using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Validates recognition output against attendance rules — no persistence (AI20.PHASE2.4).</summary>
public interface IAttendanceValidationService
{
    AttendanceValidationResult Validate(AttendanceSessionContext context);
}
