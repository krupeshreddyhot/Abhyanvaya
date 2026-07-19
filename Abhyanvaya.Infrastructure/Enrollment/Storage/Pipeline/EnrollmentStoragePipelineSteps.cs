using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;

internal abstract class EnrollmentStorageStepBase : IEnrollmentStorageStep
{
    public abstract string Name { get; }
    public abstract int Order { get; }
    public abstract string Category { get; }
    public virtual bool SupportsRollback => false;
    public virtual bool IsOptional => false;
    public virtual string? FeatureFlag => null;
    public abstract string Description { get; }
    public virtual string Version => "1.0";
    public virtual bool Enabled => true;

    public async Task ExecuteAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return;
        }

        await ExecuteCoreAsync(context, cancellationToken);
    }

    protected abstract Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken);
}

internal sealed class ValidateInputStep : EnrollmentStorageStepBase
{
    public override string Name => "ValidateInput";
    public override int Order => 10;
    public override string Category => StorageStepCategory.Validation;
    public override string Description => "Ensures the enrollment artifact passed validation before storage.";

    protected override Task ExecuteCoreAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken)
    {
        if (context.Request.Artifact.Report.OverallResult != ValidationOverallResult.Passed)
        {
            context.Failed = true;
            context.FailureReason = "Invalid artifact: validation report did not pass.";
        }

        return Task.CompletedTask;
    }
}

internal sealed class ResolvePolicyStep : EnrollmentStorageStepBase
{
    private readonly IEnrollmentStoragePolicy _policy;

    public ResolvePolicyStep(IEnrollmentStoragePolicy policy) => _policy = policy;

    public override string Name => "ResolvePolicy";
    public override int Order => 20;
    public override string Category => StorageStepCategory.Preparation;
    public override string Description => "Resolves tenant storage policy for artifact types and retention.";

    protected override async Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Failed)
        {
            return;
        }

        context.Policy = await _policy.ResolveAsync(new EnrollmentStoragePolicyRequest
        {
            TenantId = context.Request.TenantId,
            CollegeId = context.Request.CollegeId,
        }, cancellationToken);
    }
}

internal sealed class PrepareArtifactsStep : EnrollmentStorageStepBase
{
    private readonly IEnrollmentArtifactTypeRegistry _registry;

    public PrepareArtifactsStep(IEnrollmentArtifactTypeRegistry registry) => _registry = registry;

    public override string Name => "PrepareArtifacts";
    public override int Order => 30;
    public override string Category => StorageStepCategory.Preparation;
    public override string Description => "Materializes enabled artifact payloads from the validation artifact.";

    protected override async Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Failed || context.Policy is null)
        {
            return;
        }

        context.EnabledTypes = _registry.GetEnabled(context.Policy);
        if (context.EnabledTypes.Count == 0)
        {
            context.Failed = true;
            context.FailureReason = "No artifact types enabled by storage policy.";
            return;
        }

        foreach (var typeDefinition in context.EnabledTypes)
        {
            var payload = await typeDefinition.TryCreatePayloadAsync(context.Request.Artifact, cancellationToken);
            context.ArtifactItems.Add(new EnrollmentStoragePipelineArtifactItem
            {
                TypeDefinition = typeDefinition,
                Payload = payload,
            });
        }
    }
}

internal sealed class ChecksumStep : EnrollmentStorageStepBase
{
    private readonly IChecksumService _checksumService;
    private readonly IStorageMetricsCollector _metrics;

    public ChecksumStep(IChecksumService checksumService, IStorageMetricsCollector metrics)
    {
        _checksumService = checksumService;
        _metrics = metrics;
    }

    public override string Name => "Checksum";
    public override int Order => 40;
    public override string Category => StorageStepCategory.Checksum;
    public override string Description => "Computes SHA-256 checksums for artifact payloads.";

    protected override Task ExecuteCoreAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken)
    {
        if (context.Failed)
        {
            return Task.CompletedTask;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var item in context.ArtifactItems)
        {
            if (item.Payload is null)
            {
                continue;
            }

            item.Checksum = _checksumService.ComputeSha256Hex(item.Payload.Bytes);
        }

        sw.Stop();
        _metrics.RecordChecksumTime(sw.ElapsedMilliseconds);
        return Task.CompletedTask;
    }
}

internal sealed class CompressionStep : EnrollmentStorageStepBase
{
    public override string Name => "Compression";
    public override int Order => 45;
    public override string Category => StorageStepCategory.Compression;
    public override bool IsOptional => true;
    public override string? FeatureFlag => "EnrollmentStorage.Compression";
    public override string Description => "Optional payload compression before upload (feature-flagged).";
    public override bool Enabled => false;

    protected override Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class EncryptionStep : EnrollmentStorageStepBase
{
    public override string Name => "Encryption";
    public override int Order => 47;
    public override string Category => StorageStepCategory.Encryption;
    public override bool IsOptional => true;
    public override string? FeatureFlag => "EnrollmentStorage.Encryption";
    public override string Description => "Optional payload encryption before upload (feature-flagged).";
    public override bool Enabled => false;

    protected override Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class DuplicateDetectionStep : EnrollmentStorageStepBase
{
    private readonly IEnrollmentStorageRecordRepository _repository;

    public DuplicateDetectionStep(IEnrollmentStorageRecordRepository repository) => _repository = repository;

    public override string Name => "DuplicateDetection";
    public override int Order => 50;
    public override string Category => StorageStepCategory.Validation;
    public override string Description => "Detects existing artifacts with matching checksums to avoid duplicate uploads.";

    protected override async Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Failed)
        {
            return;
        }

        var request = context.Request;
        foreach (var item in context.ArtifactItems)
        {
            if (item.Payload is null || item.Checksum is null)
            {
                continue;
            }

            var existing = await _repository.FindByChecksumAsync(
                request.TenantId,
                request.StudentId,
                item.TypeDefinition.ArtifactType,
                item.Checksum,
                cancellationToken);

            if (existing is not null)
            {
                item.IsDuplicate = true;
                item.ExistingRecord = existing;
            }
        }
    }
}

internal sealed class UploadStep : EnrollmentStorageStepBase
{
    private readonly IObjectStorageProvider _objectStorage;
    private readonly IEnrollmentStorageRecordRepository _repository;
    private readonly IStorageMetricsCollector _metrics;
    private readonly TimeProvider _clock;
    private readonly ILogger<UploadStep> _logger;

    public UploadStep(
        IObjectStorageProvider objectStorage,
        IEnrollmentStorageRecordRepository repository,
        IStorageMetricsCollector metrics,
        TimeProvider clock,
        ILogger<UploadStep> logger)
    {
        _objectStorage = objectStorage;
        _repository = repository;
        _metrics = metrics;
        _clock = clock;
        _logger = logger;
    }

    public override string Name => "Upload";
    public override int Order => 60;
    public override string Category => StorageStepCategory.Upload;
    public override bool SupportsRollback => true;
    public override string Description => "Uploads artifact payloads to object storage and builds pending records.";

    protected override async Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Failed)
        {
            return;
        }

        var request = context.Request;
        var createdUtc = context.CreatedUtc;

        foreach (var item in context.ArtifactItems)
        {
            if (item.Payload is null)
            {
                context.StoredEntries.Add(new EnrollmentStoredArtifactEntry
                {
                    ArtifactId = Guid.Empty,
                    ArtifactType = item.TypeDefinition.ArtifactType,
                    ObjectKey = string.Empty,
                    Checksum = string.Empty,
                    ArtifactVersion = 0,
                    FileSize = 0,
                    Persisted = false,
                    FailureReason = "Artifact payload unavailable.",
                });
                continue;
            }

            if (item.IsDuplicate && item.ExistingRecord is not null)
            {
                var duplicateEntry = EnrollmentStorageMappers.MapStoredEntry(item.ExistingRecord, isDuplicate: true);
                context.StoredEntries.Add(duplicateEntry);
                context.ManifestEntries.Add(EnrollmentStorageMappers.MapManifestEntry(
                    item.ExistingRecord,
                    request.ValidationProfile?.ToString()));

                if (item.TypeDefinition.IsPrimary)
                {
                    context.PrimaryRecord = item.ExistingRecord;
                }

                continue;
            }

            item.ArtifactVersion = await _repository.GetNextArtifactVersionAsync(
                request.TenantId,
                request.StudentId,
                item.TypeDefinition.ArtifactType,
                cancellationToken);

            item.ObjectKey = EnrollmentStoragePathBuilder.BuildObjectKey(new EnrollmentStoragePathContext
            {
                TenantId = request.TenantId,
                CollegeId = request.CollegeId,
                AcademicYear = request.AcademicYear,
                StudentId = request.StudentId,
                PipelineVersion = request.PipelineVersion,
                ArtifactType = item.TypeDefinition.ArtifactType,
                ArtifactVersion = item.ArtifactVersion,
                FileExtension = item.TypeDefinition.FileExtension,
            });

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await using (var uploadStream = new MemoryStream(item.Payload.Bytes, writable: false))
            {
                await _objectStorage.WriteObjectAsync(
                    item.ObjectKey,
                    uploadStream,
                    item.TypeDefinition.ContentType,
                    cancellationToken);
            }

            sw.Stop();
            _metrics.RecordUpload(sw.ElapsedMilliseconds, item.Payload.Bytes.LongLength, _objectStorage.ProviderName, success: true);
            context.UploadedKeys.Add(item.ObjectKey);

            var record = new EnrollmentStorageRecord
            {
                Id = Guid.NewGuid(),
                StorageGroupId = context.StorageGroupId,
                TenantId = request.TenantId,
                CollegeId = request.CollegeId,
                AcademicYear = request.AcademicYear,
                StudentId = request.StudentId,
                BatchId = request.BatchId,
                ItemId = request.ItemId,
                ArtifactType = item.TypeDefinition.ArtifactType,
                ObjectKey = item.ObjectKey,
                StorageProvider = _objectStorage.ProviderName,
                Checksum = item.Checksum!,
                ContentType = item.TypeDefinition.ContentType,
                FileSize = item.Payload.Bytes.LongLength,
                ImageWidth = item.Payload.ImageWidth,
                ImageHeight = item.Payload.ImageHeight,
                ArtifactVersion = item.ArtifactVersion,
                StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
                PipelineVersion = request.PipelineVersion,
                ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
                ValidationProfile = request.ValidationProfile?.ToString(),
                CorrelationId = request.Artifact.CorrelationId,
                IsPrimary = item.TypeDefinition.IsPrimary,
                CreatedUtc = createdUtc.UtcDateTime,
            };

            context.PendingRecords.Add(record);
            context.StoredEntries.Add(EnrollmentStorageMappers.MapStoredEntry(record, isDuplicate: false));
            context.ManifestEntries.Add(EnrollmentStorageMappers.MapManifestEntry(record, request.ValidationProfile?.ToString()));

            if (item.TypeDefinition.IsPrimary)
            {
                context.PrimaryRecord = record;
            }

            _logger.LogInformation(
                "Artifact stored. ArtifactType={ArtifactType} CorrelationId={CorrelationId}",
                item.TypeDefinition.ArtifactType,
                request.Artifact.CorrelationId);
        }
    }
}

internal sealed class MetadataStep : EnrollmentStorageStepBase
{
    private readonly IEnrollmentStorageRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MetadataStep> _logger;

    public MetadataStep(
        IEnrollmentStorageRecordRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<MetadataStep> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public override string Name => "Metadata";
    public override int Order => 70;
    public override string Category => StorageStepCategory.Metadata;
    public override bool SupportsRollback => true;
    public override string Description => "Persists storage record metadata within a transactional unit of work.";

    protected override async Task ExecuteCoreAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Failed || context.PendingRecords.Count == 0)
        {
            return;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddRangeAsync(context.PendingRecords, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Metadata persisted. RecordCount={RecordCount} CorrelationId={CorrelationId}",
            context.PendingRecords.Count,
            context.Request.Artifact.CorrelationId);
    }
}

internal sealed class ManifestStep : EnrollmentStorageStepBase
{
    public override string Name => "Manifest";
    public override int Order => 80;
    public override string Category => StorageStepCategory.Manifest;
    public override string Description => "Builds the versioned enrollment storage manifest for the storage group.";

    protected override Task ExecuteCoreAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken)
    {
        if (context.Failed)
        {
            return Task.CompletedTask;
        }

        if (context.PrimaryRecord is null)
        {
            context.Failed = true;
            context.FailureReason = "Primary aligned face artifact could not be stored.";
            return Task.CompletedTask;
        }

        var maxVersion = context.ManifestEntries.Count == 0
            ? 0
            : context.ManifestEntries.Max(e => e.Version);

        context.Manifest = EnrollmentStorageMappers.BuildManifest(context, maxVersion);
        return Task.CompletedTask;
    }
}

public sealed class RollbackStep
{
    public const int Order = 999;

    public static StorageStepMetadata Metadata { get; } = new(
        Name: "Rollback",
        Category: StorageStepCategory.Rollback,
        Version: "1.0",
        Order: Order,
        SupportsRollback: false,
        Optional: false,
        FeatureFlag: null,
        Description: "Removes uploaded objects when the pipeline fails or throws.");

    private readonly IObjectStorageProvider _objectStorage;
    private readonly ILogger<RollbackStep> _logger;

    public RollbackStep(IObjectStorageProvider objectStorage, ILogger<RollbackStep> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task ExecuteAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var key in context.UploadedKeys)
        {
            try
            {
                await _objectStorage.DeleteObjectAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphaned object during rollback.");
            }
        }
    }
}
