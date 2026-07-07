namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Lifecycle state of an <see cref="Entities.AttendanceSession"/> from creation through approval or cancellation.
/// Applies to every attendance-taking event regardless of <see cref="AttendanceMethod"/>.
/// </summary>
public enum AttendanceSessionStatus
{
    /// <summary>Session created but not yet submitted for processing or marking.</summary>
    Draft = 0,

    /// <summary>Submitted and waiting to be picked up for processing or faculty action.</summary>
    Pending = 1,

    /// <summary>Automated processing (e.g. face recognition) is in progress.</summary>
    Processing = 2,

    /// <summary>Processing finished; results require human review before finalization.</summary>
    AwaitingReview = 3,

    /// <summary>Review complete; linked <see cref="Entities.Attendance"/> rows are authoritative.</summary>
    Approved = 4,

    /// <summary>Processing or validation failed; see <see cref="Entities.AttendanceSession.ProcessingError"/>.</summary>
    Failed = 5,

    /// <summary>Session abandoned or voided; no attendance rows should be finalized.</summary>
    Cancelled = 6,

    /// <summary>Terminal state after approved attendance has been fully closed.</summary>
    Completed = 7
}
