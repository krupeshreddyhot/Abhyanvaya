using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition;

/// <summary>
/// AI18.REVIEW.2 — dedicated recognition-thumbnail persistence layer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single responsibility.</b> This service does exactly three things: build the deterministic
/// storage key for a recognition face thumbnail, upload the bytes through the existing
/// <see cref="IMediaStorageService"/> abstraction (the same one <c>AttendancePhotoService</c> and
/// <c>StudentPhotoService</c> already use for original photo uploads), and return the key. It contains
/// no recognition logic, no matching logic, and no database access — <c>ClassroomRecognitionPipeline</c>
/// remains solely responsible for creating and saving <c>AttendanceRecognition</c> rows.
/// </para>
/// <para>
/// <b>No duplicate upload code.</b> The actual byte-level write is delegated entirely to
/// <see cref="IMediaStorageService.SaveOriginalObjectAsync"/> → <c>IStorageProviderFactory</c> →
/// <c>IStorageProvider.WriteObjectAsync</c> (local disk or S3/R2, unchanged). This class never talks
/// to <c>IStorageProvider</c> directly.
/// </para>
/// <para>
/// <b>Key format preserved.</b> <see cref="BuildFaceImageKey"/> reproduces the exact key format
/// previously computed inline in <c>ClassroomRecognitionPipeline.BuildFaceImageKey</c>
/// (<c>recognitions/{tenantId}/{attendanceSessionId}/faces/{faceNumber:D5}.webp</c>), so
/// <c>AttendanceSessionMediaPaths.BuildMediaUrl</c> and the <c>/media</c> static-file route continue to
/// resolve identically — no change to media URL generation.
/// </para>
/// <para>
/// <b>Transactional guarantee.</b> <see cref="PersistFaceThumbnailAsync"/> either returns a key that
/// names an object that was just successfully written, or throws. It never returns a key for a failed
/// or partial upload, so the caller can never end up with a dangling <c>FaceImageKey</c>.
/// </para>
/// </remarks>
public sealed class RecognitionMediaService : IRecognitionMediaService
{
    private const string ThumbnailContentType = "image/webp";

    private readonly IMediaStorageService _mediaStorage;
    private readonly ILogger<RecognitionMediaService> _logger;

    public RecognitionMediaService(IMediaStorageService mediaStorage, ILogger<RecognitionMediaService> logger)
    {
        _mediaStorage = mediaStorage;
        _logger = logger;
    }

    public async Task<string> PersistFaceThumbnailAsync(
        int tenantId,
        Guid attendanceSessionId,
        int faceNumber,
        byte[]? alignedFaceBytes,
        Guid executionTraceId,
        CancellationToken cancellationToken = default,
        short imageSequence = 1)
    {
        if (alignedFaceBytes is null || alignedFaceBytes.Length == 0)
        {
            // No bytes were produced by the AI engine for this face — refuse to fabricate a
            // FaceImageKey that would point at an object that was never (and will never be) written.
            throw new DomainException(
                $"Recognition thumbnail bytes are missing for face {faceNumber} in session " +
                $"{attendanceSessionId}; refusing to assign a FaceImageKey with no uploaded object.");
        }

        var storageKey = BuildFaceImageKey(tenantId, attendanceSessionId, faceNumber, imageSequence);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Recognition Thumbnail Upload Started. ExecutionTraceId={ExecutionTraceId} AttendanceSessionId={AttendanceSessionId} FaceNumber={FaceNumber} StorageKey={StorageKey} Bytes={Bytes}",
            executionTraceId,
            attendanceSessionId,
            faceNumber,
            storageKey,
            alignedFaceBytes.Length);

        try
        {
            using var content = new MemoryStream(alignedFaceBytes, writable: false);
            await _mediaStorage.SaveOriginalObjectAsync(storageKey, content, ThumbnailContentType, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Recognition Thumbnail Upload Completed. ExecutionTraceId={ExecutionTraceId} AttendanceSessionId={AttendanceSessionId} FaceNumber={FaceNumber} StorageKey={StorageKey} DurationMs={DurationMs} Bytes={Bytes}",
                executionTraceId,
                attendanceSessionId,
                faceNumber,
                storageKey,
                stopwatch.ElapsedMilliseconds,
                alignedFaceBytes.Length);

            return storageKey;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not an upload failure — let it propagate untouched so normal
            // cancellation handling upstream is unaffected.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Recognition Thumbnail Upload Failed. ExecutionTraceId={ExecutionTraceId} AttendanceSessionId={AttendanceSessionId} FaceNumber={FaceNumber} StorageKey={StorageKey} DurationMs={DurationMs} Bytes={Bytes}",
                executionTraceId,
                attendanceSessionId,
                faceNumber,
                storageKey,
                stopwatch.ElapsedMilliseconds,
                alignedFaceBytes.Length);

            throw new DomainException(
                $"Failed to persist recognition thumbnail for face {faceNumber} in session {attendanceSessionId}.",
                ex);
        }
    }

    /// <summary>
    /// Deterministic recognition thumbnail key — identical format to the key previously computed
    /// inline in <c>ClassroomRecognitionPipeline.BuildFaceImageKey</c> (AI18.REVIEW.1 evidence),
    /// preserved exactly so <c>AttendanceSessionMediaPaths.BuildMediaUrl</c> continues to resolve it.
    /// </summary>
    private static string BuildFaceImageKey(
        int tenantId,
        Guid attendanceSessionId,
        int faceNumber,
        short imageSequence = 1)
    {
        // Sequence 1 keeps the legacy key format for single-image sessions.
        if (imageSequence <= 1)
        {
            return $"recognitions/{tenantId}/{attendanceSessionId}/faces/{faceNumber:D5}.webp";
        }

        return $"recognitions/{tenantId}/{attendanceSessionId}/images/{imageSequence:D2}/faces/{faceNumber:D5}.webp";
    }
}
