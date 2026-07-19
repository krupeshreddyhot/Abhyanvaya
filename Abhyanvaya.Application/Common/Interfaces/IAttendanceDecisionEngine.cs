using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Produces attendance decisions from validated recognition outcomes (AI20.PHASE2.4).</summary>
public interface IAttendanceDecisionEngine
{
    IReadOnlyList<AttendanceDecision> Decide(AttendanceSessionContext context);
}
