using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>Audit record for a teacher review action on one recognition row.</summary>
public sealed class AttendanceRecognitionReviewHistoryDto
{
    public Guid Id { get; init; }

    public Guid RecognitionId { get; init; }

    public RecognitionStatus OldStatus { get; init; }

    public RecognitionStatus NewStatus { get; init; }

    public int? OldStudentId { get; init; }

    public int? NewStudentId { get; init; }

    public RecognitionReviewAction ReviewAction { get; init; }

    public string? ReviewNotes { get; init; }

    public int ReviewedBy { get; init; }

    public string? ReviewedByUsername { get; init; }

    public DateTime ReviewedUtc { get; init; }
}
