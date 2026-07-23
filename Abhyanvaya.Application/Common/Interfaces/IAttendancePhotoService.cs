using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

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
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null);

    Task QueueProcessingAsync(
        Guid sessionId,
        string storagePath,
        CancellationToken cancellationToken = default,
        ClassroomRecognitionScope scope = ClassroomRecognitionScope.FullSession,
        Guid? targetImageId = null);
}
