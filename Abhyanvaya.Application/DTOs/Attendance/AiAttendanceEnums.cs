namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>UI workflow step aligned with frontend <c>AIWorkflowStep</c>.</summary>
public enum AiWorkflowStep
{
    Upload = 0,
    Detect = 1,
    Match = 2,
    Review = 3,
    Finalize = 4,
}

/// <summary>Recognition queue phase for live dashboard visualization.</summary>
public enum RecognitionQueueStatus
{
    Waiting = 0,
    Queued = 1,
    WorkerPicked = 2,
    Detecting = 3,
    Matching = 4,
    Saving = 5,
    AwaitingReview = 6,
    Completed = 7,
    Failed = 8,
    Cancelled = 9,
}
