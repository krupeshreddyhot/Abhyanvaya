using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>
/// Recognition row presented on the teacher review screen.
/// </summary>
public sealed class AttendanceRecognitionReviewDto
{
    public Guid RecognitionId { get; init; }

    public Guid AttendanceSessionId { get; init; }

    public int FaceNumber { get; init; }

    /// <summary>AI22.7A Phase 4 — classroom image sequence this face belongs to (1-based).</summary>
    public short ImageSequence { get; init; } = 1;

    public int? StudentId { get; init; }

    public string? StudentNumber { get; init; }

    public string? StudentName { get; init; }

    public decimal? Confidence { get; init; }

    public int BoundingBoxX { get; init; }

    public int BoundingBoxY { get; init; }

    public int BoundingBoxWidth { get; init; }

    public int BoundingBoxHeight { get; init; }

    public string? FaceThumbnailUrl { get; init; }

    public string? StudentPhotoUrl { get; init; }

    public RecognitionStatus Status { get; init; }

    public bool IsMatched { get; init; }

    public int? SuggestedStudentId { get; init; }

    public string? SuggestedStudentName { get; init; }

    public string? SuggestedStudentNumber { get; init; }

    public int? ManualOverrideStudentId { get; init; }

    public string? ManualOverrideStudentName { get; init; }

    public string? ManualOverrideStudentNumber { get; init; }

    public bool VerifiedByTeacher { get; init; }

    public bool TeacherOverride { get; init; }

    public string? ReviewNotes { get; init; }
}
