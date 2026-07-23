namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>Lightweight live status snapshot for AI photo attendance polling.</summary>
public sealed class AttendanceSessionStatusDto
{
    public Guid AttendanceSessionId { get; init; }

    /// <summary><see cref="Domain.Enums.AttendanceSessionStatus"/> numeric value.</summary>
    public int Status { get; init; }

    public AiWorkflowStep WorkflowStep { get; init; }

    public RecognitionQueueStatus RecognitionQueueStatus { get; init; }

    public int DetectedFaces { get; init; }

    public int MatchedFaces { get; init; }

    public int ReviewedFaces { get; init; }

    public decimal? RecognitionAccuracy { get; init; }

    public DateTime? StartedUtc { get; init; }

    public DateTime LastUpdatedUtc { get; init; }

    public long? ElapsedMilliseconds { get; init; }

    public decimal RecognitionProgressPercent { get; init; }

    public string? CurrentStage { get; init; }

    public string? CurrentOperation { get; init; }

    public int? EstimatedRemainingMilliseconds { get; init; }

    public string? CurrentFileName { get; init; }

    /// <summary>AI22.7A Phase 3 — optional progress hints for scoped recognition.</summary>
    public short? CurrentImageSequence { get; init; }

    public int ProcessedImageCount { get; init; }

    public int TotalImageCount { get; init; }

    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    public string? ErrorCode { get; init; }

    public string? ProcessingError { get; init; }
}
