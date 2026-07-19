namespace Abhyanvaya.Domain.Enums;

public enum ArtifactUploadState
{
    Queued = 0,
    Uploading = 1,
    Uploaded = 2,
    Verifying = 3,
    Verified = 4,
    Failed = 5,
    Retry = 6,
    Cancelled = 7,
    Archived = 8,
    Deleted = 9,
}
