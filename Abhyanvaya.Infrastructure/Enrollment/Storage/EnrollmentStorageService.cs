using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

public sealed class EnrollmentStorageService : IEnrollmentStorageService
{
    private readonly IEnrollmentStoragePipelineExecutor _pipelineExecutor;
    private readonly TimeProvider _clock;
    private readonly IEnrollmentStorageRecordRepository _recordRepository;
    private readonly ILogger<EnrollmentStorageService> _logger;

    public EnrollmentStorageService(
        IEnrollmentStoragePipelineExecutor pipelineExecutor,
        IEnrollmentStorageRecordRepository recordRepository,
        TimeProvider clock,
        ILogger<EnrollmentStorageService> logger)
    {
        _pipelineExecutor = pipelineExecutor;
        _recordRepository = recordRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentStorageResult> StoreAsync(
        EnrollmentStorageRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = request.Artifact.CorrelationId;

        _logger.LogInformation(
            "Enrollment storage started. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
            request.StudentId,
            request.BatchId,
            correlationId,
            request.PipelineVersion);

        try
        {
            var context = new EnrollmentStoragePipelineContext
            {
                Request = request,
                StorageGroupId = Guid.NewGuid(),
                ManifestId = Guid.NewGuid(),
                CreatedUtc = _clock.GetUtcNow(),
            };

            context = await _pipelineExecutor.ExecuteAsync(context, cancellationToken);
            stopwatch.Stop();

            if (context.Failed || context.PrimaryRecord is null || context.Manifest is null)
            {
                _logger.LogWarning(
                    "Storage failed. DurationMs={DurationMs} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} Reason={Reason}",
                    stopwatch.ElapsedMilliseconds,
                    correlationId,
                    request.PipelineVersion,
                    context.FailureReason);

                return Failure(stopwatch.Elapsed, context.FailureReason ?? "Storage pipeline failed.");
            }

            var primary = context.PrimaryRecord;
            _logger.LogInformation(
                "Enrollment storage completed. StorageRecordId={StorageRecordId} DurationMs={DurationMs} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
                primary.Id,
                stopwatch.ElapsedMilliseconds,
                correlationId,
                request.PipelineVersion);

            return new EnrollmentStorageResult
            {
                Success = true,
                StorageRecordId = primary.Id,
                StorageProvider = primary.StorageProvider,
                StoragePath = primary.ObjectKey,
                StorageVersion = primary.StorageVersion,
                Checksum = primary.Checksum,
                FileSize = primary.FileSize,
                ImageWidth = primary.ImageWidth,
                ImageHeight = primary.ImageHeight,
                ContentType = primary.ContentType,
                Duration = stopwatch.Elapsed,
                PhotoKey = EnrollmentStoragePathBuilder.BuildCanonicalPhotoKey(request.TenantId, request.StudentId),
                Manifest = context.Manifest,
                Artifacts = context.StoredEntries,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Storage failed. DurationMs={DurationMs} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
                stopwatch.ElapsedMilliseconds,
                correlationId,
                request.PipelineVersion);

            return Failure(stopwatch.Elapsed, ex.Message);
        }
    }

    public async Task<EnrollmentStorageResult?> RetrieveAsync(
        Guid storageRecordId,
        CancellationToken cancellationToken = default)
    {
        var record = await _recordRepository.GetByIdAsync(storageRecordId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var groupRecords = await _recordRepository.GetByStorageGroupIdAsync(record.StorageGroupId, cancellationToken);
        var entries = groupRecords.Select(r => EnrollmentStorageMappers.MapStoredEntry(r, isDuplicate: false)).ToList();
        var manifestEntries = groupRecords.Select(r => EnrollmentStorageMappers.MapManifestEntry(r, r.ValidationProfile)).ToList();
        var maxVersion = manifestEntries.Count == 0 ? 0 : manifestEntries.Max(e => e.Version);

        return new EnrollmentStorageResult
        {
            Success = true,
            StorageRecordId = record.Id,
            StorageProvider = record.StorageProvider,
            StoragePath = record.ObjectKey,
            StorageVersion = record.StorageVersion,
            Checksum = record.Checksum,
            FileSize = record.FileSize,
            ImageWidth = record.ImageWidth,
            ImageHeight = record.ImageHeight,
            ContentType = record.ContentType,
            Duration = TimeSpan.Zero,
            PhotoKey = EnrollmentStoragePathBuilder.BuildCanonicalPhotoKey(record.TenantId, record.StudentId),
            Manifest = new EnrollmentStorageManifest
            {
                ManifestId = record.StorageGroupId,
                StorageGroupId = record.StorageGroupId,
                Entries = manifestEntries,
                CreatedUtc = record.CreatedUtc,
                ManifestVersion = EnrollmentStorageVersions.CurrentManifestVersion,
                SchemaVersion = EnrollmentStorageVersions.ManifestSchemaVersion,
                PipelineVersion = record.PipelineVersion,
                ValidationVersion = record.ValidationVersion,
                StorageVersion = record.StorageVersion,
                ArtifactVersion = maxVersion,
                ValidationProfile = record.ValidationProfile,
                CorrelationId = record.CorrelationId,
            },
            Artifacts = entries,
        };
    }

    public Task<EnrollmentStorageResult> DeleteAsync(
        Guid storageRecordId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failure(TimeSpan.Zero, "Delete is not implemented in AI20.PHASE2.1.5."));

    private static EnrollmentStorageResult Failure(TimeSpan duration, string reason) =>
        new()
        {
            Success = false,
            Duration = duration,
            FailureReason = reason,
            StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
        };
}
