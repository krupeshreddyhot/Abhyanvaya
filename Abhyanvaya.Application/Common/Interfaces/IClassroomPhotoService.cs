using Abhyanvaya.Application.DTOs.Attendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Uploads classroom photos and enqueues AI recognition for an attendance session.
/// AI22.7A Phase 2/3 — multi-image collection with selective recognition restart.
/// </summary>
public interface IClassroomPhotoService
{
    Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadClassroomPhotoAsync(
        Guid sessionId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null);

    /// <summary>Adds one image and queues recognition for pending images only (skips already Processed).</summary>
    Task<(bool Ok, string? Error, ClassroomPhotoCollectionUploadResult? Result)> AddSessionImageAsync(
        Guid sessionId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null);

    Task<(bool Ok, string? Error, IReadOnlyList<AttendanceSessionImageDto>? Images)> ListSessionImagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> DeleteSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces one image and queues recognition for that image only (AI22.7A Phase 3).
    /// Successfully Processed sibling images are not restarted.
    /// </summary>
    Task<(bool Ok, string? Error, ClassroomPhotoCollectionUploadResult? Result)> ReplaceSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null);

    Task<(bool Ok, string? Error, IReadOnlyList<AttendanceSessionImageDto>? Images)> ReorderSessionImagesAsync(
        Guid sessionId,
        IReadOnlyList<Guid> orderedImageIds,
        CancellationToken cancellationToken = default);

    /// <summary>Re-queues recognition for pending/failed images only (skips Processed).</summary>
    Task<(bool Ok, string? Error)> RequeueSessionRecognitionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>AI22.7A Phase 3 — re-queues recognition for a single session image.</summary>
    Task<(bool Ok, string? Error)> RequeueSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        CancellationToken cancellationToken = default);
}

/// <summary>Legacy single-upload response (backward compatible with Phase 1 clients).</summary>
public sealed class ClassroomPhotoUploadResult
{
    public Guid AttendanceSessionId { get; set; }

    public bool ImageUploaded { get; set; }

    public DateTime UploadUtc { get; set; }

    public string? ImageUrl { get; set; }

    public bool Queued { get; set; }

    public string ImageStorageKey { get; set; } = null!;
}
