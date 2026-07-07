namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>Session context for the teacher recognition review screen.</summary>
public sealed class AttendanceSessionReviewDto
{
    public Guid Id { get; init; }

    public int Status { get; init; }

    public DateTime AttendanceDate { get; init; }

    public string? AnnotatedImageUrl { get; init; }

    public string? OriginalImageUrl { get; init; }

    public int? ImageWidth { get; init; }

    public int? ImageHeight { get; init; }
}
