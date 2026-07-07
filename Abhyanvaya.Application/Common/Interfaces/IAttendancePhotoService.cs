using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Uploads classroom photos and enqueues AI recognition for an attendance session.
/// </summary>
public interface IAttendancePhotoService
{
    Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadToSessionAsync(
        AttendanceSession session,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task QueueProcessingAsync(Guid sessionId, string storagePath, CancellationToken cancellationToken = default);
}
