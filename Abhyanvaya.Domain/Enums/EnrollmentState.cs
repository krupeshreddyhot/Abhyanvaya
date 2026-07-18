namespace Abhyanvaya.Domain.Enums;

/// <summary>AI21.PHASE2 face enrollment pipeline state.</summary>
public enum EnrollmentState
{
    Queued = 0,
    DownloadingCompleted = 1,
    Processing = 2,
    DetectingFace = 3,
    AligningFace = 4,
    GeneratingEmbedding = 5,
    QualityValidation = 6,
    DuplicateChecking = 7,
    ArtifactBuilding = 8,
    Completed = 9,
    Failed = 10,
    Retry = 11,
    Cancelled = 12,
}
