namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>Session-level recognition review summary for teacher dashboards.</summary>
public sealed class RecognitionSummaryDto
{
    public Guid AttendanceSessionId { get; init; }

    public RecognitionStatisticsDto Statistics { get; init; } = new();

    public bool CanFinalize { get; init; }

    public IReadOnlyList<string> FinalizeBlockers { get; init; } = Array.Empty<string>();
}
