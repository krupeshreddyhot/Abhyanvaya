namespace Abhyanvaya.Domain.Enums;

/// <summary>Runtime orchestration state for classroom attendance processing (AI20.PHASE2.4).</summary>
public enum AttendanceSessionState
{
    Created = 0,
    Detecting = 1,
    Recognizing = 2,
    Validating = 3,
    ResolvingConflicts = 4,
    WritingAttendance = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,
}
