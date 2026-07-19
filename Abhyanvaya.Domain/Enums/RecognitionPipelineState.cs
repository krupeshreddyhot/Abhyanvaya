namespace Abhyanvaya.Domain.Enums;

/// <summary>Runtime state of a recognition pipeline execution (AI20.PHASE2.3).</summary>
public enum RecognitionPipelineState
{
    Pending = 0,
    Embedding = 1,
    Searching = 2,
    Ranking = 3,
    Evaluating = 4,
    Recognized = 5,
    Unknown = 6,
    ManualReview = 7,
    Completed = 8,
    Failed = 9,
    Cancelled = 10,
}
