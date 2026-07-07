using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>
/// Single recognition row review submitted by a teacher.
/// <see cref="Action"/> is the teacher command; it is distinct from AI <see cref="RecognitionStatus"/>.
/// </summary>
public sealed class AttendanceRecognitionReviewRequest
{
    public Guid RecognitionId { get; set; }

    /// <summary>Teacher review command (approve, reject, ignore, assign, reset).</summary>
    public RecognitionReviewAction Action { get; set; }

    /// <summary>Required when <see cref="Action"/> is <see cref="RecognitionReviewAction.AssignStudent"/>.</summary>
    public int? StudentId { get; set; }

    public string? ReviewNotes { get; set; }
}
