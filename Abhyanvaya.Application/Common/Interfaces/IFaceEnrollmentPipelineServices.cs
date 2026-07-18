using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IFaceDetectionEngine
{
    Task<FaceDetectionResult> DetectAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

public interface IFaceAlignmentEngine
{
    Task<FaceAlignmentResult> AlignAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

public interface IEnrollmentCoordinator
{
    Task<EnrollmentBatchResult> RunAcquisitionBatchAsync(EnrollmentBatchRequest request, CancellationToken cancellationToken = default);
    Task CancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentBatchProcessor
{
    Task<EnrollmentManifestEntry> ProcessSingleAsync(
        FaceEnrollmentJob job,
        byte[] photoBytes,
        EnrollmentContext context,
        CancellationToken cancellationToken = default);

    Task<EnrollmentBatchResult> ProcessBatchAsync(
        FaceEnrollmentBatch batch,
        IReadOnlyDictionary<Guid, byte[]> photoBytesByItemId,
        CancellationToken cancellationToken = default);
}

public interface IEnrollmentQualityEngine
{
    EnrollmentQualityResult ValidateFaceCount(FaceDetectionResult detection, IEnrollmentPolicy policy);
    EnrollmentQualityResult ValidateEmbedding(float[] embedding, IEmbeddingEngine engine, IEnrollmentPolicy policy);
    EnrollmentQualityResult ValidateComposite(decimal qualityScore, IEnrollmentPolicy policy);
}

public interface IEnrollmentArtifactBuilder
{
    EnrollmentArtifact Build(
        EnrollmentContext context,
        string photoReference,
        byte[] alignedFaceBytes,
        float[] embedding,
        string embeddingVersion,
        decimal qualityScore);
}

public interface IEnrollmentProgressTracker
{
    Task UpdateStateAsync(Guid enrollmentId, EnrollmentState state, CancellationToken cancellationToken = default);
    Task<EnrollmentProgressSnapshot> GetProgressAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    decimal CalculateProgressPercent(EnrollmentState state);
}

public interface IEnrollmentFailureHandler
{
    Task HandleAsync(FaceEnrollmentJob job, EnrollmentState failedAt, string reason, bool isRetryable, CancellationToken cancellationToken = default);
}

public interface IEnrollmentDuplicateDetectorService
{
    Task<EnrollmentDuplicateResult> DetectAsync(
        EnrollmentContext context,
        string studentNumber,
        string contentHash,
        float[] embedding,
        CancellationToken cancellationToken = default);
}

public interface IEnrollmentRepository
{
    Task<FaceEnrollmentBatch> CreateBatchAsync(Guid acquisitionBatchId, int tenantId, IReadOnlyList<StudentPhotoAcquisitionItem> items, CancellationToken cancellationToken = default);
    Task<FaceEnrollmentBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<FaceEnrollmentJob?> GetJobAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task UpdateJobAsync(FaceEnrollmentJob job, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(FaceEnrollmentBatch batch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FaceEnrollmentJob>> GetIncompleteJobsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FaceEnrollmentJob>> GetJobsByBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentManifestGenerator
{
    EnrollmentManifest Generate(FaceEnrollmentBatch batch, IEnumerable<FaceEnrollmentJob> jobs);
}

public interface IEnrollmentReportService
{
    Task<EnrollmentReport> GenerateBatchReportAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IArtifactUploadQueue
{
    ValueTask EnqueueAsync(EnrollmentArtifact artifact, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EnrollmentArtifact> ReadAllAsync(CancellationToken cancellationToken = default);
    int QueueDepth { get; }
}

public interface IFaceEnrollmentRecoveryService
{
    Task<EnrollmentBatchResult> ResumeBatchAsync(Guid batchId, IReadOnlyDictionary<Guid, byte[]> photoBytesByItemId, CancellationToken cancellationToken = default);
}
