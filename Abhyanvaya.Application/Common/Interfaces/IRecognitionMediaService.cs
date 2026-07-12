namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Persists AI-generated recognition face thumbnails (aligned face crops) to tenant storage and
/// hands back the deterministic storage key to be recorded on <c>AttendanceRecognition.FaceImageKey</c>.
/// </summary>
/// <remarks>
/// This is the single seam between the recognition pipeline and the storage subsystem
/// (<see cref="IMediaStorageService"/>). The AI engine (face detection/alignment/embedding) never
/// depends on this interface and remains completely unaware that thumbnails are persisted at all —
/// see <c>InsightFaceEngine</c>, which only produces <c>DetectedFaceDto.AlignedFaceBytes</c> and never
/// references storage. The orchestration layer (<c>ClassroomRecognitionPipeline</c>) is the only
/// caller of this service.
/// </remarks>
public interface IRecognitionMediaService
{
    /// <summary>
    /// Uploads one aligned face crop for a single detected face and returns the storage key that was
    /// actually written. Callers must not populate <c>AttendanceRecognition.FaceImageKey</c> until this
    /// method returns successfully — a thrown exception means no object was stored and the caller must
    /// not persist a key that points at nothing.
    /// </summary>
    /// <param name="tenantId">Tenant that owns the attendance session.</param>
    /// <param name="attendanceSessionId">Session the detected face belongs to.</param>
    /// <param name="faceNumber">1-based face index within the session image.</param>
    /// <param name="alignedFaceBytes">WebP-encoded aligned face crop bytes produced by the AI engine.</param>
    /// <param name="executionTraceId">Execution trace id for structured log correlation.</param>
    /// <returns>The deterministic relative storage key the thumbnail was written to.</returns>
    Task<string> PersistFaceThumbnailAsync(
        int tenantId,
        Guid attendanceSessionId,
        int faceNumber,
        byte[]? alignedFaceBytes,
        Guid executionTraceId,
        CancellationToken cancellationToken = default);
}
