namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Outcome of AI face matching for a single detected face in an <see cref="Entities.AttendanceSession"/>.
/// Stored on <see cref="Entities.AttendanceRecognition"/> only—does not represent official attendance.
/// Teacher review is required before <see cref="Entities.Attendance"/> rows are created.
/// </summary>
public enum RecognitionStatus
{
    /// <summary>Face detected; match outcome not yet determined or inconclusive.</summary>
    Unknown = 0,

    /// <summary>Face matched to a student with acceptable confidence.</summary>
    Recognized = 1,

    /// <summary>Face matched to a student but below the confidence threshold.</summary>
    LowConfidence = 2,

    /// <summary>Same student matched more than once in the session image.</summary>
    Duplicate = 3,

    /// <summary>Face detected but excluded from attendance (e.g. staff, visitor, background).</summary>
    Ignored = 4,

    /// <summary>Match rejected by rules or teacher during review.</summary>
    Rejected = 5,

    /// <summary>Student assigned manually by teacher; not an AI match.</summary>
    ManuallyAssigned = 6
}
