using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Conflict resolution strategy plugin (AI20.PHASE2.4).</summary>
public interface IAttendanceConflictStrategy
{
    string StrategyName { get; }

    bool CanHandle(AttendanceConflictType conflictType);

    FaceRecognitionOutcome? Resolve(
        AttendanceConflict conflict,
        IReadOnlyList<FaceRecognitionOutcome> outcomes,
        IAttendancePolicy policy);
}
