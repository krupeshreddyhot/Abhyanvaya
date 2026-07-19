using Abhyanvaya.Application.PhotoAcquisition;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IStudentPhotoSource
{
    Task<PhotoSourceResolution> ResolveAsync(
        PhotoAcquisitionStudentMaster student,
        CancellationToken cancellationToken = default);
}

public interface IStudentPhotoDownloader
{
    Task<PhotoDownloadResult> DownloadAsync(
        PhotoSourceResolution source,
        CancellationToken cancellationToken = default);
}

public interface IPhotoDownloadCoordinator
{
    Task<PhotoAcquisitionBatchResult> RunBatchAsync(
        PhotoAcquisitionBatchRequest request,
        CancellationToken cancellationToken = default);

    Task ProcessPendingRetriesAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IPhotoValidationService
{
    PhotoValidationResult Validate(
        byte[] photoBytes,
        string? contentType,
        IReadOnlySet<string>? existingHashes = null);
}

public interface IPhotoQualityAssessmentService
{
    PhotoQualityReport Assess(byte[] photoBytes);
}

public interface IPhotoRetryPolicy
{
    bool ShouldRetry(int attemptCount, PhotoDownloadResult result);
    TimeSpan GetDelay(int attemptCount);
}

public interface IPhotoManifestGenerator
{
    PhotoDownloadManifest Generate(StudentPhotoAcquisitionBatch batch, IEnumerable<StudentPhotoAcquisitionItem> items);
}

public interface IPhotoDownloadRepository
{
    Task<StudentPhotoAcquisitionBatch> CreateBatchAsync(PhotoAcquisitionBatchRequest request, CancellationToken cancellationToken = default);
    Task<StudentPhotoAcquisitionBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetBatchItemsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(StudentPhotoAcquisitionItem item, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(StudentPhotoAcquisitionBatch batch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetRetryReadyItemsAsync(Guid batchId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetEnrollmentReadyItemsAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IPhotoDownloadQueue
{
    ValueTask EnqueueDownloadAsync(Guid itemId, CancellationToken cancellationToken = default);
    ValueTask EnqueueRetryAsync(Guid itemId, CancellationToken cancellationToken = default);
    ValueTask EnqueueEnrollmentAsync(Guid itemId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadDownloadQueueAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadEnrollmentQueueAsync(CancellationToken cancellationToken = default);
    int DownloadQueueDepth { get; }
    int EnrollmentQueueDepth { get; }
}

public interface IPhotoAcquisitionReportService
{
    Task<PhotoAcquisitionReport> GenerateReportAsync(Guid batchId, CancellationToken cancellationToken = default);
}
