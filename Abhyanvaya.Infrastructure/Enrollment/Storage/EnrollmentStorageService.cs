using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

public sealed class EnrollmentStorageService : IEnrollmentStorageService
{
    private readonly IEnrollmentStoragePolicy _policy;
    private readonly IEnrollmentArtifactTypeRegistry _artifactTypeRegistry;
    private readonly IObjectStorageProvider _objectStorage;
    private readonly IChecksumService _checksumService;
    private readonly IEnrollmentStorageRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentStorageService> _logger;

    public EnrollmentStorageService(
        IEnrollmentStoragePolicy policy,
        IEnrollmentArtifactTypeRegistry artifactTypeRegistry,
        IObjectStorageProvider objectStorage,
        IChecksumService checksumService,
        IEnrollmentStorageRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<EnrollmentStorageService> logger)
    {
        _policy = policy;
        _artifactTypeRegistry = artifactTypeRegistry;
        _objectStorage = objectStorage;
        _checksumService = checksumService;
        _recordRepository = recordRepository;
        _unitOfWork = unitOfWork;
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

        if (request.Artifact.Report.OverallResult != ValidationOverallResult.Passed)
        {
            stopwatch.Stop();
            return Failure(
                stopwatch.Elapsed,
                "Invalid artifact: validation report did not pass.");
        }

        var policy = await _policy.ResolveAsync(new EnrollmentStoragePolicyRequest
        {
            TenantId = request.TenantId,
            CollegeId = request.CollegeId,
        }, cancellationToken);

        var enabledTypes = _artifactTypeRegistry.GetEnabled(policy);
        if (enabledTypes.Count == 0)
        {
            stopwatch.Stop();
            return Failure(stopwatch.Elapsed, "No artifact types enabled by storage policy.");
        }

        var storageGroupId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var createdUtc = _clock.GetUtcNow();
        var uploadedKeys = new List<string>();
        var storedEntries = new List<EnrollmentStoredArtifactEntry>();
        var pendingRecords = new List<EnrollmentStorageRecord>();
        var manifestEntries = new List<EnrollmentStorageManifestEntry>();
        EnrollmentStorageRecord? primaryRecord = null;

        try
        {
            foreach (var typeDefinition in enabledTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var payload = await typeDefinition.TryCreatePayloadAsync(request.Artifact, cancellationToken);
                if (payload is null)
                {
                    storedEntries.Add(new EnrollmentStoredArtifactEntry
                    {
                        ArtifactId = Guid.Empty,
                        ArtifactType = typeDefinition.ArtifactType,
                        ObjectKey = string.Empty,
                        Checksum = string.Empty,
                        ArtifactVersion = 0,
                        FileSize = 0,
                        Persisted = false,
                        FailureReason = "Artifact payload unavailable.",
                    });
                    continue;
                }

                var checksum = _checksumService.ComputeSha256Hex(payload.Bytes);
                _logger.LogInformation(
                    "Checksum generated. ArtifactType={ArtifactType} Checksum={Checksum} CorrelationId={CorrelationId}",
                    typeDefinition.ArtifactType,
                    checksum,
                    correlationId);

                var existing = await _recordRepository.FindByChecksumAsync(
                    request.TenantId,
                    request.StudentId,
                    typeDefinition.ArtifactType,
                    checksum,
                    cancellationToken);

                if (existing is not null)
                {
                    _logger.LogInformation(
                        "Duplicate artifact detected. ArtifactType={ArtifactType} StorageRecordId={StorageRecordId} CorrelationId={CorrelationId}",
                        typeDefinition.ArtifactType,
                        existing.Id,
                        correlationId);

                    var duplicateEntry = MapStoredEntry(existing, isDuplicate: true);
                    storedEntries.Add(duplicateEntry);
                    manifestEntries.Add(MapManifestEntry(existing, request.ValidationProfile?.ToString()));

                    if (typeDefinition.IsPrimary)
                    {
                        primaryRecord = existing;
                    }

                    continue;
                }

                var artifactVersion = await _recordRepository.GetNextArtifactVersionAsync(
                    request.TenantId,
                    request.StudentId,
                    typeDefinition.ArtifactType,
                    cancellationToken);

                var objectKey = EnrollmentStoragePathBuilder.BuildObjectKey(new EnrollmentStoragePathContext
                {
                    TenantId = request.TenantId,
                    CollegeId = request.CollegeId,
                    AcademicYear = request.AcademicYear,
                    StudentId = request.StudentId,
                    PipelineVersion = request.PipelineVersion,
                    ArtifactType = typeDefinition.ArtifactType,
                    ArtifactVersion = artifactVersion,
                    FileExtension = typeDefinition.FileExtension,
                });

                await using (var uploadStream = new MemoryStream(payload.Bytes, writable: false))
                {
                    await _objectStorage.WriteObjectAsync(
                        objectKey,
                        uploadStream,
                        typeDefinition.ContentType,
                        cancellationToken);
                }

                uploadedKeys.Add(objectKey);

                var recordId = Guid.NewGuid();
                var record = new EnrollmentStorageRecord
                {
                    Id = recordId,
                    StorageGroupId = storageGroupId,
                    TenantId = request.TenantId,
                    CollegeId = request.CollegeId,
                    AcademicYear = request.AcademicYear,
                    StudentId = request.StudentId,
                    BatchId = request.BatchId,
                    ItemId = request.ItemId,
                    ArtifactType = typeDefinition.ArtifactType,
                    ObjectKey = objectKey,
                    StorageProvider = _objectStorage.ProviderName,
                    Checksum = checksum,
                    ContentType = typeDefinition.ContentType,
                    FileSize = payload.Bytes.LongLength,
                    ImageWidth = payload.ImageWidth,
                    ImageHeight = payload.ImageHeight,
                    ArtifactVersion = artifactVersion,
                    StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
                    PipelineVersion = request.PipelineVersion,
                    ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
                    ValidationProfile = request.ValidationProfile?.ToString(),
                    CorrelationId = correlationId,
                    IsPrimary = typeDefinition.IsPrimary,
                    CreatedUtc = createdUtc.UtcDateTime,
                };

                pendingRecords.Add(record);
                storedEntries.Add(MapStoredEntry(record, isDuplicate: false));
                manifestEntries.Add(MapManifestEntry(record, request.ValidationProfile?.ToString()));

                if (typeDefinition.IsPrimary)
                {
                    primaryRecord = record;
                }

                _logger.LogInformation(
                    "Artifact stored. ArtifactType={ArtifactType} ObjectKey={ObjectKey} CorrelationId={CorrelationId}",
                    typeDefinition.ArtifactType,
                    objectKey,
                    correlationId);
            }

            if (pendingRecords.Count > 0)
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await _recordRepository.AddRangeAsync(pendingRecords, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, cancellationToken);

                _logger.LogInformation(
                    "Metadata persisted. RecordCount={RecordCount} CorrelationId={CorrelationId}",
                    pendingRecords.Count,
                    correlationId);
            }

            if (primaryRecord is null)
            {
                stopwatch.Stop();
                await RollbackUploadedObjectsAsync(uploadedKeys, cancellationToken);
                return Failure(stopwatch.Elapsed, "Primary aligned face artifact could not be stored.");
            }

            stopwatch.Stop();

            var manifest = new EnrollmentStorageManifest
            {
                ManifestId = manifestId,
                StorageGroupId = storageGroupId,
                Entries = manifestEntries,
                CreatedUtc = createdUtc,
                PipelineVersion = request.PipelineVersion,
                ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
                ValidationProfile = request.ValidationProfile?.ToString(),
                CorrelationId = correlationId,
            };

            _logger.LogInformation(
                "Enrollment storage completed. StorageRecordId={StorageRecordId} DurationMs={DurationMs} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
                primaryRecord.Id,
                stopwatch.ElapsedMilliseconds,
                correlationId,
                request.PipelineVersion);

            return new EnrollmentStorageResult
            {
                Success = true,
                StorageRecordId = primaryRecord.Id,
                StorageProvider = primaryRecord.StorageProvider,
                StoragePath = primaryRecord.ObjectKey,
                StorageVersion = primaryRecord.StorageVersion,
                Checksum = primaryRecord.Checksum,
                FileSize = primaryRecord.FileSize,
                ImageWidth = primaryRecord.ImageWidth,
                ImageHeight = primaryRecord.ImageHeight,
                ContentType = primaryRecord.ContentType,
                Duration = stopwatch.Elapsed,
                PhotoKey = EnrollmentStoragePathBuilder.BuildCanonicalPhotoKey(request.TenantId, request.StudentId),
                Manifest = manifest,
                Artifacts = storedEntries,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            await RollbackUploadedObjectsAsync(uploadedKeys, cancellationToken);

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
        var entries = groupRecords.Select(r => MapStoredEntry(r, isDuplicate: false)).ToList();
        var manifestEntries = groupRecords.Select(r => MapManifestEntry(r, r.ValidationProfile)).ToList();

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
                PipelineVersion = record.PipelineVersion,
                ValidationVersion = record.ValidationVersion,
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

    private async Task RollbackUploadedObjectsAsync(IReadOnlyList<string> uploadedKeys, CancellationToken cancellationToken)
    {
        foreach (var key in uploadedKeys)
        {
            try
            {
                await _objectStorage.DeleteObjectAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete orphaned object during rollback. ObjectKey={ObjectKey}",
                    key);
            }
        }
    }

    private static EnrollmentStoredArtifactEntry MapStoredEntry(EnrollmentStorageRecord record, bool isDuplicate) =>
        new()
        {
            ArtifactId = record.Id,
            ArtifactType = record.ArtifactType,
            ObjectKey = record.ObjectKey,
            Checksum = record.Checksum,
            ArtifactVersion = record.ArtifactVersion,
            FileSize = record.FileSize,
            ContentType = record.ContentType,
            ImageWidth = record.ImageWidth,
            ImageHeight = record.ImageHeight,
            Persisted = true,
            IsDuplicate = isDuplicate,
        };

    private static EnrollmentStorageManifestEntry MapManifestEntry(
        EnrollmentStorageRecord record,
        string? validationProfile) =>
        new()
        {
            ArtifactId = record.Id,
            ArtifactType = record.ArtifactType,
            StorageProvider = record.StorageProvider,
            ObjectKey = record.ObjectKey,
            Checksum = record.Checksum,
            Version = record.ArtifactVersion,
            CreatedUtc = record.CreatedUtc,
            PipelineVersion = record.PipelineVersion,
            ValidationProfile = validationProfile,
            ContentType = record.ContentType,
            ImageMetadata = new EnrollmentStorageImageMetadata
            {
                Width = record.ImageWidth,
                Height = record.ImageHeight,
                FileSize = record.FileSize,
            },
        };

    private static EnrollmentStorageResult Failure(TimeSpan duration, string reason) =>
        new()
        {
            Success = false,
            Duration = duration,
            FailureReason = reason,
            StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
        };
}
