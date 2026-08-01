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
/// Validates, stores, and queues classroom photos for attendance sessions (single + multi-image AI22.7A Phase 2).
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
        var (ok, error, collection) = await AddSessionImageAsync(
            sessionId,
            imageStream,
            fileName,
            fileSizeBytes,
            cancellationToken,
            captureContext);

        if (!ok || collection == null)
        {
            return (false, error, null);
        }

        return (true, null, new ClassroomPhotoUploadResult
        {
            AttendanceSessionId = collection.AttendanceSessionId,
            ImageUploaded = true,
            UploadUtc = collection.Image.UploadedUtc ?? DateTime.UtcNow,
            ImageUrl = collection.Image.ImageUrl,
            ImageStorageKey = collection.Image.ImageStorageKey,
            Queued = collection.Queued,
        });
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoCollectionUploadResult? Result)> AddSessionImageAsync(
        Guid sessionId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.", null);
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot upload a classroom photo to a finalized session.", null);
        }

        ClassroomPhotoCollectionUploadResult? result = null;

        try
        {
            await EnsureLegacyImageBackfillAsync(session, cancellationToken);

            var existingCount = await _context.AttendanceSessionImages
                .CountAsync(i => i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId, cancellationToken);

            if (existingCount >= ClassroomPhotoCollectionLimits.MaxImagesPerSession)
            {
                return (false, $"A session may contain at most {ClassroomPhotoCollectionLimits.MaxImagesPerSession} classroom images.", null);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var nextSequence = (short)(existingCount + 1);
                var stored = await StoreImageAsync(
                    session,
                    imageStream,
                    fileName,
                    fileSizeBytes,
                    nextSequence,
                    captureContext,
                    ct);

                if (!stored.Ok || stored.Image == null)
                {
                    throw new InvalidOperationException(stored.Error ?? "Classroom photo upload failed.");
                }

                await _context.AddAsync(stored.Image);
                SyncPrimarySessionImage(session, stored.Image);

                if (session.Status == AttendanceSessionStatus.Draft)
                {
                    session.MoveToPending();
                }

                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);

                result = new ClassroomPhotoCollectionUploadResult
                {
                    AttendanceSessionId = session.Id,
                    Image = MapImageDto(stored.Image),
                    Queued = false,
                    ImageCount = existingCount + 1,
                };
            }, cancellationToken);

            if (result != null)
            {
                await QueueProcessingAsync(
                    session.Id,
                    result.Image.ImageStorageKey,
                    cancellationToken,
                    ClassroomRecognitionScope.PendingOnly);
                result.Queued = true;
                result.RecognitionScope = nameof(ClassroomRecognitionScope.PendingOnly);
            }

            return (true, null, result);
        }
        catch (Exception ex)
        {
            var detail = GetInnermostMessage(ex);
            _logger.LogWarning(ex, "Classroom photo collection upload failed. SessionId={SessionId}", sessionId);
            return (false, detail, null);
        }
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<AttendanceSessionImageDto>? Images)> ListSessionImagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.", null);
        }

        await EnsureLegacyImageBackfillAsync(session, cancellationToken);
        if (session.ImageMetadata.HasUploadedImage)
        {
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        }

        var images = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId)
            .OrderBy(i => i.ImageSequence)
            .ToListAsync(cancellationToken);

        var faceCounts = await _context.AttendanceRecognitions
            .Where(r => r.AttendanceSessionId == sessionId && r.TenantId == _currentUser.TenantId)
            .GroupBy(r => r.ImageSequence)
            .Select(g => new { Sequence = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countBySequence = faceCounts.ToDictionary(x => x.Sequence, x => x.Count);

        return (true, null, images.Select(i => MapImageDto(i, countBySequence.GetValueOrDefault(i.ImageSequence))).ToList());
    }

    public async Task<(bool Ok, string? Error)> DeleteSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.");
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot modify images on a finalized session.");
        }

        var image = await _context.AttendanceSessionImages
            .FirstOrDefaultAsync(
                i => i.Id == imageId && i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (image == null)
        {
            return (false, "Session image was not found.");
        }

        var storageKey = image.ImageKey;
        _context.Remove(image);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        try
        {
            await _mediaStorage.DeleteObjectAsync(storageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete classroom image object {StorageKey}", storageKey);
        }

        await RenumberSequencesAsync(sessionId, cancellationToken);
        await RefreshPrimaryFromCollectionAsync(session, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var remaining = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == sessionId)
            .OrderBy(i => i.ImageSequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (remaining != null)
        {
            // Sequences were renumbered — full rebuild keeps ImageSequence aligned with recognitions.
            await QueueProcessingAsync(
                sessionId,
                remaining.ImageKey,
                cancellationToken,
                ClassroomRecognitionScope.FullSession);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoCollectionUploadResult? Result)> ReplaceSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.", null);
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot modify images on a finalized session.", null);
        }

        var existing = await _context.AttendanceSessionImages
            .FirstOrDefaultAsync(
                i => i.Id == imageId && i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (existing == null)
        {
            return (false, "Session image was not found.", null);
        }

        ClassroomPhotoCollectionUploadResult? result = null;
        var previousKey = existing.ImageKey;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var stored = await StoreImageAsync(
                    session,
                    imageStream,
                    fileName,
                    fileSizeBytes,
                    existing.ImageSequence,
                    captureContext,
                    ct);

                if (!stored.Ok || stored.Image == null)
                {
                    throw new InvalidOperationException(stored.Error ?? "Classroom photo replace failed.");
                }

                existing.ImageKey = stored.Image.ImageKey;
                existing.OriginalFileName = stored.Image.OriginalFileName;
                existing.Width = stored.Image.Width;
                existing.Height = stored.Image.Height;
                existing.FileSize = stored.Image.FileSize;
                existing.UploadedUtc = stored.Image.UploadedUtc;
                existing.CaptureTimestamp = stored.Image.CaptureTimestamp;
                existing.CaptureDevice = stored.Image.CaptureDevice;
                existing.AcquisitionMethod = stored.Image.AcquisitionMethod;
                existing.Orientation = stored.Image.Orientation;
                existing.CaptureLatitude = stored.Image.CaptureLatitude;
                existing.CaptureLongitude = stored.Image.CaptureLongitude;
                existing.BlurScore = stored.Image.BlurScore;
                existing.Status = AttendanceSessionImageStatus.Uploaded;
                existing.ProcessingError = null;

                SyncPrimarySessionImage(session, existing);
                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);

                result = new ClassroomPhotoCollectionUploadResult
                {
                    AttendanceSessionId = session.Id,
                    Image = MapImageDto(existing),
                    Queued = false,
                    ImageCount = await _context.AttendanceSessionImages.CountAsync(
                        i => i.AttendanceSessionId == sessionId,
                        ct),
                };
            }, cancellationToken);

            if (!string.Equals(previousKey, result?.Image.ImageStorageKey, StringComparison.Ordinal))
            {
                try
                {
                    await _mediaStorage.DeleteObjectAsync(previousKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete replaced classroom image {StorageKey}", previousKey);
                }
            }

            if (result != null)
            {
                // Phase 3: restart recognition for the replaced image only.
                await QueueProcessingAsync(
                    session.Id,
                    result.Image.ImageStorageKey,
                    cancellationToken,
                    ClassroomRecognitionScope.SingleImage,
                    result.Image.Id);
                result.Queued = true;
                result.RecognitionScope = nameof(ClassroomRecognitionScope.SingleImage);
            }

            return (true, null, result);
        }
        catch (Exception ex)
        {
            return (false, GetInnermostMessage(ex), null);
        }
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<AttendanceSessionImageDto>? Images)> ReorderSessionImagesAsync(
        Guid sessionId,
        IReadOnlyList<Guid> orderedImageIds,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.", null);
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot modify images on a finalized session.", null);
        }

        var images = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId)
            .ToListAsync(cancellationToken);

        if (images.Count == 0)
        {
            return (true, null, []);
        }

        if (orderedImageIds.Count != images.Count ||
            orderedImageIds.Distinct().Count() != images.Count ||
            orderedImageIds.Any(id => images.All(i => i.Id != id)))
        {
            return (false, "Reorder list must include every session image exactly once.", null);
        }

        for (var i = 0; i < orderedImageIds.Count; i++)
        {
            var image = images.First(x => x.Id == orderedImageIds[i]);
            image.ImageSequence = (short)(i + 1);
        }

        await RefreshPrimaryFromCollectionAsync(session, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var ordered = images.OrderBy(i => i.ImageSequence).Select(i => MapImageDto(i)).ToList();
        var firstKey = ordered.FirstOrDefault()?.ImageStorageKey;
        if (!string.IsNullOrWhiteSpace(firstKey))
        {
            // Reorder changes ImageSequence — full rebuild keeps recognition rows aligned.
            await QueueProcessingAsync(
                sessionId,
                firstKey,
                cancellationToken,
                ClassroomRecognitionScope.FullSession);
        }

        return (true, null, ordered);
    }

    public async Task<(bool Ok, string? Error)> RequeueSessionRecognitionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.");
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot requeue recognition on a finalized session.");
        }

        var images = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId)
            .OrderBy(i => i.ImageSequence)
            .ToListAsync(cancellationToken);

        if (images.Count == 0)
        {
            return (false, "No classroom images are available to recognize.");
        }

        // Reset failed/stuck images so PendingOnly scope picks them up again.
        foreach (var image in images.Where(i =>
                     i.Status is AttendanceSessionImageStatus.Failed
                         or AttendanceSessionImageStatus.Processing))
        {
            image.Status = AttendanceSessionImageStatus.Uploaded;
            image.ProcessingError = null;
        }

        // Move Failed → Pending so status polling shows "Queued" immediately after retry
        // (worker may still be picking up the in-memory job).
        if (session.Status == AttendanceSessionStatus.Failed)
        {
            session.MoveToPending();
            session.ProcessingError = null;
            session.CompletedUtc = null;
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        // After reset above, prefer the first Uploaded row for the queue message storage key.
        var first = images.FirstOrDefault(i => i.Status == AttendanceSessionImageStatus.Uploaded) ?? images[0];

        await QueueProcessingAsync(
            sessionId,
            first.ImageKey,
            cancellationToken,
            ClassroomRecognitionScope.PendingOnly);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RequeueSessionImageAsync(
        Guid sessionId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadMutableSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, "Attendance session was not found.");
        }

        if (IsFinalized(session))
        {
            return (false, "Cannot requeue recognition on a finalized session.");
        }

        var image = await _context.AttendanceSessionImages
            .FirstOrDefaultAsync(
                i => i.Id == imageId && i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (image == null)
        {
            return (false, "Session image was not found.");
        }

        image.Status = AttendanceSessionImageStatus.Uploaded;
        image.ProcessingError = null;

        if (session.Status == AttendanceSessionStatus.Failed)
        {
            session.MoveToPending();
            session.ProcessingError = null;
            session.CompletedUtc = null;
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        await QueueProcessingAsync(
            sessionId,
            image.ImageKey,
            cancellationToken,
            ClassroomRecognitionScope.SingleImage,
            image.Id);

        return (true, null);
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> UploadToSessionAsync(
        AttendanceSession session,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default,
        ClassroomPhotoCaptureContextDto? captureContext = null)
    {
        // Legacy single-image path used by AttendanceSessionCreator — also creates SessionImage row.
        await EnsureLegacyImageBackfillAsync(session, cancellationToken);

        var count = await _context.AttendanceSessionImages
            .CountAsync(i => i.AttendanceSessionId == session.Id, cancellationToken);

        if (count >= ClassroomPhotoCollectionLimits.MaxImagesPerSession)
        {
            return (false, $"A session may contain at most {ClassroomPhotoCollectionLimits.MaxImagesPerSession} classroom images.", null);
        }

        var stored = await StoreImageAsync(
            session,
            imageStream,
            fileName,
            fileSizeBytes,
            (short)(count + 1),
            captureContext,
            cancellationToken);

        if (!stored.Ok || stored.Image == null)
        {
            return (false, stored.Error, null);
        }

        await _context.AddAsync(stored.Image);
        SyncPrimarySessionImage(session, stored.Image);

        return (true, null, new ClassroomPhotoUploadResult
        {
            AttendanceSessionId = session.Id,
            ImageUploaded = true,
            UploadUtc = stored.Image.UploadedUtc ?? DateTime.UtcNow,
            ImageUrl = AttendanceSessionMediaPaths.BuildMediaUrl(
                stored.Image.ImageKey,
                stored.Image.UploadedUtc ?? DateTime.UtcNow),
            ImageStorageKey = stored.Image.ImageKey,
            Queued = false,
        });
    }

    public async Task QueueProcessingAsync(
        Guid sessionId,
        string storagePath,
        CancellationToken cancellationToken = default,
        ClassroomRecognitionScope scope = ClassroomRecognitionScope.FullSession,
        Guid? targetImageId = null)
    {
        await _queue.EnqueueAsync(
            new ClassroomPhotoMessage(
                sessionId,
                _currentUser.TenantId,
                storagePath,
                _currentUser.UserId,
                DateTime.UtcNow,
                scope,
                targetImageId),
            cancellationToken);

        _logger.LogInformation(
            "Classroom recognition job enqueued. SessionId={SessionId} TenantId={TenantId} Scope={Scope} TargetImageId={TargetImageId} QueueDepth={QueueDepth}",
            sessionId,
            _currentUser.TenantId,
            scope,
            targetImageId,
            _queue.Count);
    }

    private async Task<(bool Ok, string? Error, AttendanceSessionImage? Image)> StoreImageAsync(
        AttendanceSession session,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        short imageSequence,
        ClassroomPhotoCaptureContextDto? captureContext,
        CancellationToken cancellationToken)
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
        var storageKey = AttendanceSessionStoragePaths.BuildClassroomImageKey(
            session.TenantId,
            session.Id,
            imageSequence,
            extension);
        var contentType = AttendanceSessionStoragePaths.GetContentType(extension);
        var uploadedUtc = DateTime.UtcNow;

        try
        {
            await _mediaStorage.SaveOriginalObjectAsync(storageKey, imageStream, contentType, cancellationToken);

            var image = new AttendanceSessionImage
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                AttendanceSessionId = session.Id,
                ImageSequence = imageSequence,
                ImageKey = storageKey,
                OriginalFileName = fileName,
                Width = validation.Width,
                Height = validation.Height,
                FileSize = fileSizeBytes,
                UploadedUtc = uploadedUtc,
                CaptureTimestamp = captureContext?.CaptureTimestampUtc,
                CaptureDevice = Truncate(captureContext?.CaptureDevice, 100),
                AcquisitionMethod = NormalizeAcquisitionMethod(captureContext?.AcquisitionMethod),
                Orientation = captureContext?.Orientation,
                CaptureLatitude = captureContext?.Latitude,
                CaptureLongitude = captureContext?.Longitude,
                BlurScore = captureContext?.BlurScore,
                Status = AttendanceSessionImageStatus.Uploaded,
                CreatedUtc = uploadedUtc,
            };

            return (true, null, image);
        }
        catch (Exception ex)
        {
            await _mediaStorage.DeleteObjectAsync(storageKey, cancellationToken);
            return (false, ex.Message, null);
        }
    }

    private async Task EnsureLegacyImageBackfillAsync(AttendanceSession session, CancellationToken cancellationToken)
    {
        var hasRows = await _context.AttendanceSessionImages
            .AnyAsync(i => i.AttendanceSessionId == session.Id, cancellationToken);

        if (hasRows || !session.ImageMetadata.HasUploadedImage || string.IsNullOrWhiteSpace(session.ImageMetadata.ImageKey))
        {
            return;
        }

        var legacy = new AttendanceSessionImage
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            AttendanceSessionId = session.Id,
            ImageSequence = 1,
            ImageKey = session.ImageMetadata.ImageKey!,
            ImageHash = session.ImageMetadata.ImageHash,
            OriginalFileName = session.OriginalFileName,
            Width = session.ImageMetadata.Width,
            Height = session.ImageMetadata.Height,
            Orientation = session.ImageMetadata.Orientation,
            FileSize = session.ImageMetadata.FileSize,
            UploadedUtc = session.ImageMetadata.UploadedUtc,
            CaptureTimestamp = session.ImageMetadata.CaptureTimestamp,
            CaptureDevice = session.ImageMetadata.CaptureDevice,
            AcquisitionMethod = session.ImageMetadata.AcquisitionMethod,
            CaptureLatitude = session.ImageMetadata.CaptureLatitude,
            CaptureLongitude = session.ImageMetadata.CaptureLongitude,
            BlurScore = session.ImageMetadata.BlurScore,
            ThumbnailImageKey = session.ThumbnailImageKey,
            AnnotatedImageKey = session.AnnotatedImageKey,
            Status = AttendanceSessionImageStatus.Uploaded,
            CreatedUtc = session.ImageMetadata.UploadedUtc ?? DateTime.UtcNow,
        };

        await _context.AddAsync(legacy);
        _logger.LogInformation(
            "Backfilled AttendanceSessionImage from legacy session metadata. SessionId={SessionId}",
            session.Id);
    }

    private async Task RenumberSequencesAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var images = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == sessionId && i.TenantId == _currentUser.TenantId)
            .OrderBy(i => i.ImageSequence)
            .ThenBy(i => i.CreatedUtc)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < images.Count; i++)
        {
            images[i].ImageSequence = (short)(i + 1);
        }
    }

    private async Task RefreshPrimaryFromCollectionAsync(AttendanceSession session, CancellationToken cancellationToken)
    {
        var primary = await _context.AttendanceSessionImages
            .Where(i => i.AttendanceSessionId == session.Id && i.TenantId == session.TenantId)
            .OrderBy(i => i.ImageSequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (primary == null)
        {
            session.ClearClassroomImage();
            return;
        }

        SyncPrimarySessionImage(session, primary);
    }

    private static void SyncPrimarySessionImage(AttendanceSession session, AttendanceSessionImage image)
    {
        var metadata = new ClassroomImageMetadata
        {
            ImageKey = image.ImageKey,
            ImageHash = image.ImageHash,
            Width = image.Width,
            Height = image.Height,
            Orientation = image.Orientation,
            CaptureTimestamp = image.CaptureTimestamp,
            CaptureDevice = image.CaptureDevice,
            UploadedUtc = image.UploadedUtc,
            FileSize = image.FileSize,
            AcquisitionMethod = image.AcquisitionMethod,
            CaptureLatitude = image.CaptureLatitude,
            CaptureLongitude = image.CaptureLongitude,
            BlurScore = image.BlurScore,
        };

        session.AttachClassroomImage(
            image.ImageKey,
            image.OriginalFileName ?? "classroom.jpg",
            metadata,
            image.UploadedUtc ?? DateTime.UtcNow,
            image.FileSize ?? 0);
    }

    private async Task<AttendanceSession?> LoadMutableSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == _currentUser.TenantId, cancellationToken);

    private static bool IsFinalized(AttendanceSession session) =>
        session.Status is AttendanceSessionStatus.Approved
            or AttendanceSessionStatus.Completed
            or AttendanceSessionStatus.Cancelled;

    private static AttendanceSessionImageDto MapImageDto(
        AttendanceSessionImage image,
        int detectedFaceCount = 0) =>
        new()
        {
            Id = image.Id,
            ImageSequence = image.ImageSequence,
            ImageUrl = AttendanceSessionMediaPaths.BuildMediaUrl(
                image.ImageKey,
                image.UploadedUtc ?? DateTime.UtcNow),
            OriginalFileName = image.OriginalFileName,
            Width = image.Width,
            Height = image.Height,
            FileSize = image.FileSize,
            UploadedUtc = image.UploadedUtc,
            CaptureTimestamp = image.CaptureTimestamp,
            CaptureDevice = image.CaptureDevice,
            CaptureLatitude = image.CaptureLatitude,
            CaptureLongitude = image.CaptureLongitude,
            Orientation = image.Orientation,
            AcquisitionMethod = image.AcquisitionMethod,
            BlurScore = image.BlurScore,
            Status = image.Status,
            ProcessingError = image.ProcessingError,
            ImageStorageKey = image.ImageKey,
            DetectedFaceCount = detectedFaceCount,
            BatchStatus = MapBatchStatus(image.Status),
        };

    private static string MapBatchStatus(AttendanceSessionImageStatus status) =>
        status switch
        {
            AttendanceSessionImageStatus.Processing => "Processing",
            AttendanceSessionImageStatus.Processed => "Processed",
            AttendanceSessionImageStatus.Failed => "Failed",
            _ => "Waiting",
        };

    private static string GetInnermostMessage(Exception ex)
    {
        while (ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        return ex.Message;
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
}
