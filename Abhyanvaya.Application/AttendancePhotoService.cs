using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application;

/// <summary>
/// Validates, stores, and queues classroom photos for attendance sessions.
/// </summary>
public sealed class AttendancePhotoService : IAttendancePhotoService, IClassroomPhotoService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClassroomImageValidator _imageValidator;
    private readonly IMediaStorageService _mediaStorage;
    private readonly IClassroomPhotoQueue _queue;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AttendancePhotoService> _logger;

    public AttendancePhotoService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IClassroomImageValidator imageValidator,
        IMediaStorageService mediaStorage,
        IClassroomPhotoQueue queue,
        ICurrentUserService currentUser,
        ILogger<AttendancePhotoService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _imageValidator = imageValidator;
        _mediaStorage = mediaStorage;
        _queue = queue;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadClassroomPhotoAsync(
        Guid sessionId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null)
    {
        var session = await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == _currentUser.TenantId, cancellationToken);

        if (session == null)
        {
            return (false, "Attendance session was not found.", null);
        }

        if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.Cancelled)
        {
            return (false, "Cannot upload a classroom photo to a finalized session.", null);
        }

        ClassroomPhotoUploadResult? result = null;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var upload = await UploadToSessionAsync(
                    session,
                    imageStream,
                    fileName,
                    fileSizeBytes,
                    cancellationToken: ct,
                    captureContext: captureContext);

                if (!upload.Ok)
                {
                    throw new InvalidOperationException(upload.Error ?? "Classroom photo upload failed.");
                }

                if (session.Status == AttendanceSessionStatus.Draft)
                {
                    session.MoveToPending();
                }

                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);
                result = upload.Result;
            }, cancellationToken);

            if (result != null)
            {
                await QueueProcessingAsync(result.AttendanceSessionId, result.ImageStorageKey, cancellationToken);
                result.Queued = true;
            }

            return (true, null, result);
        }
        catch (Exception ex)
        {
            var detail = GetInnermostMessage(ex);
            _logger.LogWarning(ex, "Classroom photo upload failed. SessionId={SessionId}", sessionId);
            return (false, detail, null);
        }
    }

    private static string GetInnermostMessage(Exception ex)
    {
        while (ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        return ex.Message;
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadToSessionAsync(
        AttendanceSession session,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null)
    {
        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }

        var validation = await _imageValidator.ValidateAsync(imageStream, fileName, fileSizeBytes, cancellationToken);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage, null);
        }

        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }

        var extension = Path.GetExtension(fileName);
        var storageKey = AttendanceSessionStoragePaths.BuildClassroomImageKey(session.TenantId, session.Id, extension);
        var contentType = AttendanceSessionStoragePaths.GetContentType(extension);
        var uploadedUtc = DateTime.UtcNow;

        try
        {
            await _mediaStorage.SaveOriginalObjectAsync(storageKey, imageStream, contentType, cancellationToken);

            var metadata = new ClassroomImageMetadata
            {
                Width = validation.Width,
                Height = validation.Height,
                FileSize = fileSizeBytes,
                AcquisitionMethod = NormalizeAcquisitionMethod(captureContext?.AcquisitionMethod),
                CaptureDevice = Truncate(captureContext?.CaptureDevice, 100),
                CaptureTimestamp = captureContext?.CaptureTimestampUtc,
                Orientation = captureContext?.Orientation,
                CaptureLatitude = captureContext?.Latitude,
                CaptureLongitude = captureContext?.Longitude,
                BlurScore = captureContext?.BlurScore,
            };

            session.AttachClassroomImage(storageKey, fileName, metadata, uploadedUtc, fileSizeBytes);

            var result = new ClassroomPhotoUploadResult
            {
                AttendanceSessionId = session.Id,
                ImageUploaded = true,
                UploadUtc = uploadedUtc,
                ImageUrl = AttendanceSessionMediaPaths.BuildMediaUrl(storageKey, uploadedUtc),
                ImageStorageKey = storageKey,
                Queued = false,
            };

            _logger.LogInformation(
                "Classroom photo stored. SessionId={SessionId} TenantId={TenantId} StorageKey={StorageKey}",
                session.Id,
                session.TenantId,
                storageKey);

            return (true, null, result);
        }
        catch (Exception ex)
        {
            await _mediaStorage.DeleteObjectAsync(storageKey, cancellationToken);
            _logger.LogWarning(ex, "Classroom photo storage failed. SessionId={SessionId}", session.Id);
            return (false, ex.Message, null);
        }
    }

    private static string? NormalizeAcquisitionMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized switch
        {
            nameof(ClassroomPhotoAcquisitionMethod.Upload) => nameof(ClassroomPhotoAcquisitionMethod.Upload),
            nameof(ClassroomPhotoAcquisitionMethod.CameraCapture) => nameof(ClassroomPhotoAcquisitionMethod.CameraCapture),
            nameof(ClassroomPhotoAcquisitionMethod.CameraMultiCapture) => nameof(ClassroomPhotoAcquisitionMethod.CameraMultiCapture),
            _ => Truncate(normalized, 32),
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public async Task QueueProcessingAsync(Guid sessionId, string storagePath, CancellationToken cancellationToken = default)
    {
        await _queue.EnqueueAsync(
            new ClassroomPhotoMessage(sessionId, _currentUser.TenantId, storagePath, _currentUser.UserId, DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation(
            "Classroom recognition job enqueued. SessionId={SessionId} TenantId={TenantId} QueueDepth={QueueDepth}",
            sessionId,
            _currentUser.TenantId,
            _queue.Count);
    }
}
