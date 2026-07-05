using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Immutable audit record for a single teacher review action on an <see cref="AttendanceRecognition"/> row.
/// </summary>
/// <remarks>
/// One history row is appended for every review command: Approve, Reject, Ignore, AssignStudent, or Reset.
/// Stores before/after status and student assignment to support compliance and dispute resolution.
/// </remarks>
public class AttendanceRecognitionReviewHistory
{
    /// <summary>Unique identifier for this audit entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Recognition row that was reviewed.</summary>
    public Guid RecognitionId { get; set; }

    /// <summary>Recognition status before the teacher action.</summary>
    public RecognitionStatus OldStatus { get; set; }

    /// <summary>Recognition status after the teacher action.</summary>
    public RecognitionStatus NewStatus { get; set; }

    /// <summary>Matched or assigned student before the teacher action.</summary>
    public int? OldStudentId { get; set; }

    /// <summary>Matched or assigned student after the teacher action.</summary>
    public int? NewStudentId { get; set; }

    /// <summary>Teacher review command that produced this change.</summary>
    public RecognitionReviewAction ReviewAction { get; set; }

    /// <summary>Notes captured with this review action.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>User who performed the review.</summary>
    public int ReviewedBy { get; set; }

    /// <summary>UTC timestamp when the review action was recorded.</summary>
    public DateTime ReviewedUtc { get; set; }

    /// <summary>Navigation to the reviewed recognition row.</summary>
    public AttendanceRecognition Recognition { get; set; } = null!;

    /// <summary>Navigation to the reviewing user.</summary>
    public User ReviewedByUser { get; set; } = null!;
}
