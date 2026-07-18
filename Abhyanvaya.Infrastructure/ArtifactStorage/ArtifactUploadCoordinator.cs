using System.Diagnostics;
using System.Text.Json;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public sealed class ArtifactUploadCoordinator : IArtifactUploadCoordinator
{
    private readonly IArtifactUploadQueue _uploadQueue;
    private readonly IArtifactUploadService _uploadService;
    private readonly IArtifactVerificationService _verificationService;
    private readonly IArtifactIntegrityService _integrityService;
    private readonly IArtifactVersionManager _versionManager;
    private readonly IArtifactRegistryRepository _registryRepository;
    private readonly IArtifactManifestRepository _manifestRepository;
    private readonly IArtifactStorageProvider _storageProvider;
    private readonly IAITelemetryService _telemetryService;
    private readonly IAITracingService _tracingService;
    private readonly ArtifactStorageOptions _options;
    private readonly ILogger<ArtifactUploadCoordinator> _logger;

    public ArtifactUploadCoordinator(
        IArtifactUploadQueue uploadQueue,
        IArtifactUploadService uploadService,
        IArtifactVerificationService verificationService,
        IArtifactIntegrityService integrityService,
        IArtifactVersionManager versionManager,
        IArtifactRegistryRepository registryRepository,
        IArtifactManifestRepository manifestRepository,
        IArtifactStorageProvider storageProvider,
        IAITelemetryService telemetryService,
        IAITracingService tracingService,
        IOptions<ArtifactStorageOptions> options,
        ILogger<ArtifactUploadCoordinator> logger)
    {
        _uploadQueue = uploadQueue;
        _uploadService = uploadService;
        _verificationService = verificationService;
        _integrityService = integrityService;
        _versionManager = versionManager;
        _registryRepository = registryRepository;
        _manifestRepository = manifestRepository;
        _storageProvider = storageProvider;
        _telemetryService = telemetryService;
        _tracingService = tracingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunContinuousAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var request in _uploadQueue.ReadAllAsync(cancellationToken))
        {
            try
            {
                _ = await ProcessQueuedItemAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Artifact upload coordinator failed enrollmentId={EnrollmentId}", request.EnrollmentId);
            }
        }
    }

    public async Task<ArtifactBatchUploadResult> ProcessQueuedItemAsync(
        ArtifactUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var trace = _tracingService.CreateContext(request.CorrelationId, request.TenantId, pipelineId: "artifact-storage");
        trace = _tracingService.StartSpan(trace, "artifact.upload.batch", "ArtifactStorage");

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ArtifactUploadResult>();
        var items = BuildUploadItems(request);

        _ = new ArtifactQueued(request.EnrollmentId, request.EnrollmentId, DateTime.UtcNow);
        _ = new ArtifactUploadStarted(request.EnrollmentId, request.EnrollmentId, DateTime.UtcNow);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var artifactId = Guid.NewGuid();
            var storageKey = BuildStorageKey(request, item.ArtifactType);
            var context = new ArtifactStorageContext
            {
                ArtifactId = artifactId,
                EnrollmentId = request.EnrollmentId,
                StudentId = request.Artifact.StudentId,
                BatchId = request.BatchId,
                StorageProvider = _storageProvider.ProviderName,
                Bucket = _storageProvider.Bucket,
                StorageKey = storageKey,
                Checksum = item.Checksum,
                CorrelationId = request.CorrelationId,
                TraceId = request.TraceId,
                CreatedUtc = DateTime.UtcNow,
            };

            await _registryRepository.SaveAsync(new ArtifactRegistryRecord
            {
                Id = artifactId,
                EnrollmentId = request.EnrollmentId,
                BatchId = request.BatchId,
                ManifestId = request.Artifact.ManifestId,
                TenantId = request.TenantId,
                StudentId = request.Artifact.StudentId,
                ArtifactType = item.ArtifactType,
                Status = ArtifactUploadState.Uploading,
                StorageProvider = context.StorageProvider,
                Bucket = context.Bucket,
                StorageKey = storageKey,
                Checksum = item.Checksum,
                FileSize = item.Content.LongLength,
                ArtifactVersion = _versionManager.AssignArtifactVersion(item.ArtifactType, request.Artifact.StudentId),
                StorageVersion = 1,
                CorrelationId = request.CorrelationId,
                TraceId = request.TraceId,
                CreatedUtc = DateTime.UtcNow,
            }, cancellationToken);

            var uploadResult = await _uploadService.UploadItemAsync(context, item, cancellationToken);
            if (uploadResult.FinalState == ArtifactUploadState.Failed)
            {
                await _registryRepository.UpdateStatusAsync(
                    artifactId,
                    ArtifactUploadState.Failed,
                    failureReason: uploadResult.FailureReason,
                    cancellationToken: cancellationToken);
                _ = new ArtifactUploadFailed(artifactId, uploadResult.FailureReason ?? "Upload failed", DateTime.UtcNow);
                results.Add(uploadResult);
                continue;
            }

            _ = new ArtifactUploaded(artifactId, storageKey, DateTime.UtcNow);
            await _registryRepository.UpdateStatusAsync(artifactId, ArtifactUploadState.Verifying, cancellationToken: cancellationToken);

            var metadata = new ArtifactMetadata
            {
                ArtifactType = item.ArtifactType,
                ContentType = item.ContentType,
                FileSize = item.Content.LongLength,
                Checksum = item.Checksum,
                Compression = false,
                Version = item.Version,
                CreatedUtc = DateTime.UtcNow,
                RetentionPolicy = _options.StorageClass,
                StorageClass = _options.StorageClass,
            };

            var verification = await _verificationService.VerifyAsync(context, metadata, item.Content, cancellationToken);
            if (!verification.Passed)
            {
                var reason = string.Join("; ", verification.Errors ?? []);
                await _registryRepository.UpdateStatusAsync(
                    artifactId,
                    ArtifactUploadState.Failed,
                    verificationJson: JsonSerializer.Serialize(verification),
                    failureReason: reason,
                    cancellationToken: cancellationToken);
                _ = new ArtifactVerificationFailed(artifactId, reason, DateTime.UtcNow);
                results.Add(uploadResult with { FinalState = ArtifactUploadState.Failed, Verified = false, FailureReason = reason });
                continue;
            }

            await _registryRepository.UpdateStatusAsync(
                artifactId,
                ArtifactUploadState.Verified,
                verificationJson: JsonSerializer.Serialize(verification),
                cancellationToken: cancellationToken);
            _ = new ArtifactVerified(artifactId, item.Checksum, DateTime.UtcNow);
            results.Add(uploadResult with { Verified = true, FinalState = ArtifactUploadState.Verified });
        }

        var manifestJson = JsonSerializer.Serialize(new
        {
            request.EnrollmentId,
            request.BatchId,
            request.Artifact.ManifestId,
            request.Artifact.EnrollmentVersion,
            Results = results.Select(r => new { r.ArtifactId, r.StorageKey, r.Checksum, r.FinalState }),
        });

        await _manifestRepository.SaveManifestAsync(new ArtifactStorageManifestRecord
        {
            Id = request.Artifact.ManifestId,
            BatchId = request.BatchId,
            EnrollmentId = request.EnrollmentId,
            TenantId = request.TenantId,
            ManifestJson = manifestJson,
            ManifestVersion = _versionManager.ResolveManifestVersion(_options.ManifestVersion),
            Status = results.All(r => r.FinalState == ArtifactUploadState.Verified)
                ? ArtifactUploadState.Verified
                : ArtifactUploadState.Failed,
            CreatedUtc = DateTime.UtcNow,
        }, cancellationToken);

        stopwatch.Stop();
        _telemetryService.RecordDuration("artifact.upload.duration", stopwatch.Elapsed);

        var verified = results.Count(r => r.Verified);
        var failed = results.Count - verified;

        return new ArtifactBatchUploadResult
        {
            EnrollmentId = request.EnrollmentId,
            Results = results,
            Statistics = new ArtifactStorageStatistics
            {
                Uploaded = results.Count,
                Verified = verified,
                Failed = failed,
                RetryCount = 0,
                AverageUploadTime = stopwatch.Elapsed / Math.Max(1, results.Count),
                AverageFileSize = results.Count == 0 ? 0 : (long)results.Average(r => r.FileSize),
                StorageUsed = results.Sum(r => r.FileSize),
                CompressionRatio = 1m,
            },
        };
    }

    private IReadOnlyList<ArtifactUploadItem> BuildUploadItems(ArtifactUploadRequest request)
    {
        var items = new List<ArtifactUploadItem>();
        var embeddingVersion = _versionManager.ResolveEmbeddingVersion(request.Artifact.EmbeddingVersion);

        if (request.OriginalPhotoBytes is { Length: > 0 })
        {
            items.Add(new ArtifactUploadItem
            {
                ArtifactType = "original-photo",
                ContentType = request.OriginalContentType ?? "image/jpeg",
                Content = request.OriginalPhotoBytes,
                Checksum = _integrityService.ComputeSha256(request.OriginalPhotoBytes),
                Version = request.Artifact.EnrollmentVersion,
            });
        }

        items.Add(new ArtifactUploadItem
        {
            ArtifactType = "aligned-face",
            ContentType = "image/jpeg",
            Content = request.AlignedFaceBytes,
            Checksum = request.Artifact.Checksum,
            Version = request.Artifact.EnrollmentVersion,
        });

        var embeddingBytes = SerializeEmbedding(request.Embedding);
        items.Add(new ArtifactUploadItem
        {
            ArtifactType = "embedding",
            ContentType = "application/octet-stream",
            Content = embeddingBytes,
            Checksum = _integrityService.ComputeSha256(embeddingBytes),
            Version = embeddingVersion,
        });

        var metadataJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Artifact.StudentId,
            request.Artifact.QualityScore,
            request.Artifact.EmbeddingDimension,
            request.Artifact.EnrollmentVersion,
            RecognitionVersion = _versionManager.ResolveRecognitionVersion(_options.RecognitionVersion),
        });

        items.Add(new ArtifactUploadItem
        {
            ArtifactType = "metadata",
            ContentType = "application/json",
            Content = metadataJson,
            Checksum = _integrityService.ComputeSha256(metadataJson),
            Version = request.Artifact.EnrollmentVersion,
        });

        var qualityReport = JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Artifact.QualityScore,
            request.Artifact.Checksum,
            GeneratedUtc = DateTime.UtcNow,
        });

        items.Add(new ArtifactUploadItem
        {
            ArtifactType = "quality-report",
            ContentType = "application/json",
            Content = qualityReport,
            Checksum = _integrityService.ComputeSha256(qualityReport),
            Version = request.Artifact.EnrollmentVersion,
        });

        return items;
    }

    private string BuildStorageKey(ArtifactUploadRequest request, string artifactType) =>
        $"{_options.KeyPrefix}/{request.TenantId}/{request.BatchId}/{request.EnrollmentId}/{artifactType}";

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}

public sealed class ArtifactUploadBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArtifactUploadBackgroundService> _logger;

    public ArtifactUploadBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ArtifactUploadBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Artifact upload background worker started.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<IArtifactUploadCoordinator>();
        await coordinator.RunContinuousAsync(stoppingToken);
    }
}
