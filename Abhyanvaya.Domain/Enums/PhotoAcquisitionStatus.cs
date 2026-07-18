namespace Abhyanvaya.Domain.Enums;

public enum PhotoAcquisitionBatchStatus
{
    Created = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum PhotoAcquisitionItemStatus
{
    Pending = 0,
    Downloading = 1,
    Validating = 2,
    QualityAssessment = 3,
    ReadyForEnrollment = 4,
    Failed = 5,
    RetryQueued = 6,
    Duplicate = 7,
    Invalid = 8,
}
