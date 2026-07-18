using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.FaceEnrollment;

public sealed class EnrollmentBatchProcessor : IEnrollmentBatchProcessor
{
    private readonly IFaceDetectionEngine _detectionEngine;
    private readonly IFaceAlignmentEngine _alignmentEngine;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly IEmbeddingValidator _embeddingValidator;
    private readonly IEmbeddingNormalizer _embeddingNormalizer;
    private readonly IEnrollmentQualityEngine _qualityEngine;
    private readonly IEnrollmentDuplicateDetectorService _duplicateDetector;
    private readonly IEnrollmentArtifactBuilder _artifactBuilder;
    private readonly IEnrollmentProgressTracker _progressTracker;
    private readonly IEnrollmentFailureHandler _failureHandler;
    private readonly IEnrollmentRepository _repository;
    private readonly IEnrollmentManifestGenerator _manifestGenerator;
    private readonly IArtifactUploadQueue _uploadQueue;
    private readonly IAITracingService _tracingService;
    private readonly IAITelemetryService _telemetryService;
    private readonly IEnrollmentPolicy _policy;
    private readonly EnrollmentPolicyOptions _options;
    private readonly ILogger<EnrollmentBatchProcessor> _logger;

    public EnrollmentBatchProcessor(
        IFaceDetectionEngine detectionEngine,
        IFaceAlignmentEngine alignmentEngine,
        IEmbeddingEngine embeddingEngine,
        IEmbeddingValidator embeddingValidator,
        IEmbeddingNormalizer embeddingNormalizer,
        IEnrollmentQualityEngine qualityEngine,
        IEnrollmentDuplicateDetectorService duplicateDetector,
        IEnrollmentArtifactBuilder artifactBuilder,
        IEnrollmentProgressTracker progressTracker,
        IEnrollmentFailureHandler failureHandler,
        IEnrollmentRepository repository,
        IEnrollmentManifestGenerator manifestGenerator,
        IArtifactUploadQueue uploadQueue,
        IAITracingService tracingService,
        IAITelemetryService telemetryService,
        IEnrollmentPolicy policy,
        IOptions<EnrollmentPolicyOptions> options,
        ILogger<EnrollmentBatchProcessor> logger)
    {
        _detectionEngine = detectionEngine;
        _alignmentEngine = alignmentEngine;
        _embeddingEngine = embeddingEngine;
        _embeddingValidator = embeddingValidator;
        _embeddingNormalizer = embeddingNormalizer;
        _qualityEngine = qualityEngine;
        _duplicateDetector = duplicateDetector;
        _artifactBuilder = artifactBuilder;
        _progressTracker = progressTracker;
        _failureHandler = failureHandler;
        _repository = repository;
        _manifestGenerator = manifestGenerator;
        _uploadQueue = uploadQueue;
        _tracingService = tracingService;
        _telemetryService = telemetryService;
        _policy = policy;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EnrollmentBatchResult> ProcessBatchAsync(
        FaceEnrollmentBatch batch,
        IReadOnlyDictionary<Guid, byte[]> photoBytesByItemId,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _repository.GetJobsByBatchAsync(batch.Id, cancellationToken);
        var maxParallel = Math.Max(1, _options.MaxParallelism);
        using var semaphore = new SemaphoreSlim(maxParallel);
        var results = new ConcurrentBag<EnrollmentManifestEntry>();

        var tasks = jobs.Select(async job =>
        {
            if (!photoBytesByItemId.TryGetValue(job.AcquisitionItemId, out var photoBytes))
            {
                results.Add(ToEntry(job, EnrollmentState.Failed, null, "Photo bytes missing."));
                return;
            }

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var context = BuildContext(job, batch.Id);
                var entry = await ProcessSingleAsync(job, photoBytes, context, cancellationToken);
                results.Add(entry);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        var refreshedJobs = await _repository.GetJobsByBatchAsync(batch.Id, cancellationToken);
        var manifest = _manifestGenerator.Generate(batch, refreshedJobs);
        var statistics = BuildStatistics(refreshedJobs);

        batch.ManifestJson = JsonSerializer.Serialize(manifest);
        batch.CompletedCount = refreshedJobs.Count(j => j.State == EnrollmentState.Completed);
        batch.FailedCount = refreshedJobs.Count(j => j.State == EnrollmentState.Failed);
        batch.DuplicateCount = refreshedJobs.Count(j => j.FailureReason?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true);
        batch.RetryCount = refreshedJobs.Count(j => j.State == EnrollmentState.Retry);
        batch.State = EnrollmentState.Completed;
        batch.CompletedUtc = DateTime.UtcNow;
        await _repository.UpdateBatchAsync(batch, cancellationToken);

        return new EnrollmentBatchResult
        {
            BatchId = batch.Id,
            Manifest = manifest,
            Statistics = statistics,
        };
    }

    public async Task<EnrollmentManifestEntry> ProcessSingleAsync(
        FaceEnrollmentJob job,
        byte[] photoBytes,
        EnrollmentContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = new EnrollmentStarted(context.EnrollmentId, context.BatchId, context.StudentId, DateTime.UtcNow);

        var trace = _tracingService.CreateContext(context.CorrelationId, tenantId: job.TenantId, pipelineId: "face-enrollment");
        trace = _tracingService.StartSpan(trace, "enrollment.process", "FaceEnrollment");

        try
        {
            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.Processing, cancellationToken);
            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.DetectingFace, cancellationToken);

            var detection = await _detectionEngine.DetectAsync(photoBytes, cancellationToken);
            _ = new FaceDetected(context.EnrollmentId, detection.FaceCount, DateTime.UtcNow);

            var faceQuality = _qualityEngine.ValidateFaceCount(detection, _policy);
            if (!faceQuality.Passed)
            {
                await _failureHandler.HandleAsync(job, EnrollmentState.DetectingFace, string.Join("; ", faceQuality.Errors!), false, cancellationToken);
                return ToEntry(job, EnrollmentState.Failed, null, string.Join("; ", faceQuality.Errors!));
            }

            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.AligningFace, cancellationToken);
            var alignment = await _alignmentEngine.AlignAsync(photoBytes, cancellationToken);
            if (!alignment.Success || alignment.AlignedFaceBytes is not { Length: > 0 })
            {
                await _failureHandler.HandleAsync(job, EnrollmentState.AligningFace, alignment.FailureReason ?? "Alignment failed.", true, cancellationToken);
                return ToEntry(job, job.State, null, alignment.FailureReason);
            }

            _ = new FaceAligned(context.EnrollmentId, DateTime.UtcNow);

            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.GeneratingEmbedding, cancellationToken);
            var embedStopwatch = Stopwatch.StartNew();
            await using var alignedStream = new MemoryStream(alignment.AlignedFaceBytes);
            var engineResult = await _embeddingEngine.GenerateFromAlignedFaceAsync(alignedStream, cancellationToken);
            embedStopwatch.Stop();
            _telemetryService.RecordDuration("embedding.duration", embedStopwatch.Elapsed);

            var normalized = _embeddingNormalizer.Normalize(engineResult.EmbeddingVector);
            var validation = _embeddingValidator.ValidateNormalized(normalized, _embeddingEngine.ExpectedDimension);
            if (!validation.IsValid)
            {
                await _failureHandler.HandleAsync(job, EnrollmentState.GeneratingEmbedding, validation.FailureReason ?? "Invalid embedding.", true, cancellationToken);
                return ToEntry(job, job.State, null, validation.FailureReason);
            }

            _ = new EmbeddingGenerated(context.EnrollmentId, normalized.Length, DateTime.UtcNow);

            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.QualityValidation, cancellationToken);
            var embeddingQuality = _qualityEngine.ValidateEmbedding(normalized, _embeddingEngine, _policy);
            var compositeQuality = _qualityEngine.ValidateComposite((decimal)detection.TopConfidence, _policy);
            if (!embeddingQuality.Passed || !compositeQuality.Passed)
            {
                var reason = string.Join("; ", (embeddingQuality.Errors ?? []).Concat(compositeQuality.Errors ?? []));
                await _failureHandler.HandleAsync(job, EnrollmentState.QualityValidation, reason, false, cancellationToken);
                return ToEntry(job, EnrollmentState.Failed, null, reason);
            }

            _ = new QualityValidated(context.EnrollmentId, compositeQuality.QualityScore, DateTime.UtcNow);

            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.DuplicateChecking, cancellationToken);
            var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(photoBytes));
            var duplicate = await _duplicateDetector.DetectAsync(context, job.StudentNumber, contentHash, normalized, cancellationToken);
            if (duplicate.IsDuplicate)
            {
                _ = new DuplicateDetected(context.EnrollmentId, duplicate.DuplicateType ?? "Unknown", DateTime.UtcNow);
                job.State = EnrollmentState.Failed;
                job.FailureReason = $"Duplicate {duplicate.DuplicateType}: {duplicate.Detail}";
                await _repository.UpdateJobAsync(job, cancellationToken);
                return ToEntry(job, EnrollmentState.Failed, null, job.FailureReason);
            }

            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.ArtifactBuilding, cancellationToken);
            var artifact = _artifactBuilder.Build(
                context,
                $"photo://{context.PhotoId}",
                alignment.AlignedFaceBytes,
                normalized,
                _embeddingEngine.ModelVersion,
                compositeQuality.QualityScore);

            job.ArtifactJson = JsonSerializer.Serialize(artifact);
            job.QualityScore = compositeQuality.QualityScore;
            job.State = EnrollmentState.Completed;
            job.CompletedUtc = DateTime.UtcNow;
            await _repository.UpdateJobAsync(job, cancellationToken);
            await _progressTracker.UpdateStateAsync(job.Id, EnrollmentState.Completed, cancellationToken);

            await _uploadQueue.EnqueueAsync(new ArtifactUploadRequest
            {
                Artifact = artifact,
                EnrollmentId = context.EnrollmentId,
                BatchId = context.BatchId,
                PhotoId = context.PhotoId,
                TenantId = job.TenantId,
                CorrelationId = context.CorrelationId,
                TraceId = context.TraceId,
                OriginalPhotoBytes = photoBytes,
                AlignedFaceBytes = alignment.AlignedFaceBytes,
                Embedding = normalized,
                OriginalContentType = alignment.ContentType,
            }, cancellationToken);
            _ = new ArtifactBuilt(context.EnrollmentId, artifact.ManifestId, DateTime.UtcNow);
            _ = new EnrollmentCompleted(context.EnrollmentId, context.BatchId, DateTime.UtcNow);

            stopwatch.Stop();
            _telemetryService.RecordDuration("enrollment.duration", stopwatch.Elapsed);
            _logger.LogInformation(
                "Enrollment completed enrollmentId={EnrollmentId} studentId={StudentId} durationMs={DurationMs}",
                job.Id,
                job.StudentId,
                stopwatch.ElapsedMilliseconds);

            return ToEntry(job, EnrollmentState.Completed, compositeQuality.QualityScore, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _failureHandler.HandleAsync(job, job.State, ex.Message, true, cancellationToken);
            return ToEntry(job, EnrollmentState.Failed, null, ex.Message);
        }
    }

    private EnrollmentContext BuildContext(FaceEnrollmentJob job, Guid batchId) =>
        new()
        {
            EnrollmentId = job.Id,
            StudentId = job.StudentId,
            BatchId = batchId,
            PhotoId = job.AcquisitionItemId,
            CorrelationId = job.CorrelationId,
            TraceId = job.TraceId,
            StartedTime = DateTime.UtcNow,
            RecognitionConfigurationVersion = _options.RecognitionConfigurationVersion,
            EnrollmentPolicyVersion = _options.EnrollmentPolicyVersion,
        };

    private static EnrollmentManifestEntry ToEntry(FaceEnrollmentJob job, EnrollmentState state, decimal? quality, string? reason) =>
        new()
        {
            EnrollmentId = job.Id,
            StudentId = job.StudentId,
            StudentNumber = job.StudentNumber,
            FinalState = state,
            QualityScore = quality ?? job.QualityScore,
            FailureReason = reason ?? job.FailureReason,
        };

    private static EnrollmentStatistics BuildStatistics(IReadOnlyList<FaceEnrollmentJob> jobs)
    {
        var completed = jobs.Count(j => j.State == EnrollmentState.Completed);
        var failed = jobs.Count(j => j.State == EnrollmentState.Failed);
        var duplicates = jobs.Count(j => j.FailureReason?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true);
        var retries = jobs.Sum(j => j.RetryCount);
        var total = jobs.Count;
        var avgQuality = jobs.Where(j => j.QualityScore.HasValue).Select(j => j.QualityScore!.Value).DefaultIfEmpty(0).Average();

        return new EnrollmentStatistics
        {
            Queued = jobs.Count(j => j.State == EnrollmentState.Queued),
            Completed = completed,
            Failed = failed,
            Duplicates = duplicates,
            AverageDuration = TimeSpan.Zero,
            AverageQuality = avgQuality,
            AverageEmbeddingTime = TimeSpan.Zero,
            RetryCount = retries,
            SuccessRate = total == 0 ? 0 : (decimal)completed / total,
        };
    }
}

public sealed class EnrollmentCoordinator : IEnrollmentCoordinator
{
    private readonly IPhotoDownloadRepository _photoRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IEnrollmentBatchProcessor _batchProcessor;
    private readonly ILogger<EnrollmentCoordinator> _logger;

    public EnrollmentCoordinator(
        IPhotoDownloadRepository photoRepository,
        IEnrollmentRepository enrollmentRepository,
        IEnrollmentBatchProcessor batchProcessor,
        ILogger<EnrollmentCoordinator> logger)
    {
        _photoRepository = photoRepository;
        _enrollmentRepository = enrollmentRepository;
        _batchProcessor = batchProcessor;
        _logger = logger;
    }

    public async Task<EnrollmentBatchResult> RunAcquisitionBatchAsync(
        EnrollmentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var readyItems = await _photoRepository.GetEnrollmentReadyItemsAsync(request.AcquisitionBatchId, cancellationToken);
        if (readyItems.Count == 0)
        {
            throw new InvalidOperationException($"No ReadyForEnrollment items for acquisition batch {request.AcquisitionBatchId}.");
        }

        var batch = await _enrollmentRepository.CreateBatchAsync(
            request.AcquisitionBatchId,
            request.TenantId,
            readyItems,
            cancellationToken);

        var photoMap = readyItems
            .Where(i => i.PhotoBytes is { Length: > 0 })
            .ToDictionary(i => i.Id, i => i.PhotoBytes!);

        _logger.LogInformation(
            "Face enrollment batch started batchId={BatchId} acquisitionBatchId={AcquisitionBatchId} items={Items}",
            batch.Id,
            request.AcquisitionBatchId,
            readyItems.Count);

        return await _batchProcessor.ProcessBatchAsync(batch, photoMap, cancellationToken);
    }

    public async Task CancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _enrollmentRepository.GetBatchAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Batch not found: {batchId}");

        batch.State = EnrollmentState.Cancelled;
        await _enrollmentRepository.UpdateBatchAsync(batch, cancellationToken);

        var jobs = await _enrollmentRepository.GetJobsByBatchAsync(batchId, cancellationToken);
        foreach (var job in jobs.Where(j => j.State is not EnrollmentState.Completed and not EnrollmentState.Failed))
        {
            job.State = EnrollmentState.Cancelled;
            await _enrollmentRepository.UpdateJobAsync(job, cancellationToken);
        }
    }
}
