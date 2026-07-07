using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>
/// AI recognition result presented to a teacher during session review.
/// Not an official attendance record—maps from <see cref="Domain.Entities.AttendanceRecognition"/>.
/// </summary>
public sealed class AttendanceRecognitionDto
{
    public Guid Id { get; init; }

    public Guid AttendanceSessionId { get; init; }

    public int? StudentId { get; init; }

    public string? StudentName { get; init; }

    public string? StudentNumber { get; init; }

    public string? ThumbnailUrl { get; init; }

    public decimal? ConfidenceScore { get; init; }

    public decimal? EmbeddingDistance { get; init; }

    public RecognitionStatus RecognitionStatus { get; init; }

    public int BoundingBoxX { get; init; }

    public int BoundingBoxY { get; init; }

    public int BoundingBoxWidth { get; init; }

    public int BoundingBoxHeight { get; init; }

    public bool VerifiedByTeacher { get; init; }

    public bool TeacherOverride { get; init; }

    public string? ReviewNotes { get; init; }
}
