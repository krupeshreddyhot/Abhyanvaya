namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Uploads classroom photos and enqueues AI recognition for an attendance session.
/// </summary>
public interface IClassroomPhotoService
{
    Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadClassroomPhotoAsync(
        Guid sessionId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);
}

public sealed class ClassroomPhotoUploadResult
{
    public Guid AttendanceSessionId { get; set; }

    public bool ImageUploaded { get; set; }

    public DateTime UploadUtc { get; set; }

    public string? ImageUrl { get; set; }

    public bool Queued { get; set; }

    public string ImageStorageKey { get; set; } = null!;
}
