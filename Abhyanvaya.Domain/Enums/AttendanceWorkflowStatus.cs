namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// AI22.8 — enterprise attendance workflow lifecycle (additive).
/// Does not replace <see cref="AttendanceSessionStatus"/>; maps alongside it for recovery UX.
/// </summary>
public enum AttendanceWorkflowStatus
{
    Created = 0,
    ImagesUploaded = 1,
    RecognitionRunning = 2,
    RecognitionCompleted = 3,
    ReviewPending = 4,
    ReviewInProgress = 5,
    ReadyForFinalization = 6,
    AttendanceFinalized = 7,
    Cancelled = 8,
    RecognitionFailed = 9,
    UploadFailed = 10,
    Expired = 11
}
