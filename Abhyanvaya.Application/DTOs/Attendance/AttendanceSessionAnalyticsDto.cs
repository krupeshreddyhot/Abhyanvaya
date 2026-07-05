namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Computed analytics for a single attendance session.
/// Prefers denormalized <see cref="Domain.Entities.AttendanceSession"/> summary fields where available.
/// </summary>
public sealed class AttendanceSessionAnalyticsDto
{
    public Guid AttendanceSessionId { get; init; }

    public int RecognizedCount { get; init; }

    public int UnknownCount { get; init; }

    public int RejectedCount { get; init; }

    public int IgnoredCount { get; init; }

    public int DuplicateCount { get; init; }

    public int ManualAssignmentCount { get; init; }

    public int LowConfidenceCount { get; init; }

    /// <summary>
    /// Percentage of detected faces that were identified (recognized or manually assigned), 0–100 scale.
    /// </summary>
    public decimal? RecognitionAccuracy { get; init; }

    /// <summary>Mean match confidence (0–100 scale) from session summary.</summary>
    public decimal? AverageConfidence { get; init; }

    /// <summary>Number of faces where a teacher corrected the AI assignment or status.</summary>
    public int TeacherCorrections { get; init; }

    /// <summary>Sum of per-face recognition times in milliseconds.</summary>
    public int? RecognitionDurationMilliseconds { get; init; }

    /// <summary>Wall-clock session processing duration in milliseconds.</summary>
    public int? ProcessingDurationMilliseconds { get; init; }

    public int PresentStudents { get; init; }

    public int AbsentStudents { get; init; }
}
