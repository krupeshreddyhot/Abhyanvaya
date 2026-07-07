namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>Aggregate recognition counts for a session review dashboard.</summary>
public sealed class RecognitionStatisticsDto
{
    public int DetectedFaces { get; init; }

    public int Matched { get; init; }

    public int Unmatched { get; init; }

    public int LowConfidence { get; init; }

    public int ManualOverrides { get; init; }

    public int Rejected { get; init; }

    public int Approved { get; init; }

    public int PendingReview { get; init; }

    public decimal? AverageConfidence { get; init; }
}
