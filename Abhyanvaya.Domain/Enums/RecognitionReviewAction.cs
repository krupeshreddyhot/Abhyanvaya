namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Teacher review action applied during attendance session review.
/// Represents what the teacher chose to do—not the AI outcome stored in <see cref="RecognitionStatus"/>.
/// </summary>
/// <remarks>
/// <see cref="RecognitionReviewAction"/> is the command/input from the review UI or API.
/// <see cref="RecognitionStatus"/> on <see cref="Entities.AttendanceRecognition"/> is the persisted
/// AI or post-review state. Services map review actions to status updates (e.g.
/// <see cref="Approve"/> may set <see cref="RecognitionStatus.Recognized"/> with
/// <see cref="Entities.AttendanceRecognition.VerifiedByTeacher"/>).
/// </remarks>
public enum RecognitionReviewAction
{
    /// <summary>Teacher confirms the AI match is correct.</summary>
    Approve = 1,

    /// <summary>Teacher rejects the match; face will not count toward attendance.</summary>
    Reject = 2,

    /// <summary>Teacher excludes the face from attendance (staff, visitor, duplicate, etc.).</summary>
    Ignore = 3,

    /// <summary>Teacher manually assigns or reassigns a student to this face.</summary>
    AssignStudent = 4,

    /// <summary>Teacher clears prior review decisions and restores the row for re-review.</summary>
    Reset = 5
}
