using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Storage;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Embedding;

/// <summary>
/// Enrollment embedding orchestrator — resolves aligned-face artifact and produces immutable embedding output.
/// </summary>
public sealed class EnrollmentEmbeddingService : IEnrollmentEmbeddingService
{
    private readonly IEnrollmentArtifactResolver _artifactResolver;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly IEmbeddingValidator _validator;
    private readonly IEmbeddingNormalizer _normalizer;
    private readonly IEmbeddingQualityAnalyzer _qualityAnalyzer;
    private readonly IEmbeddingGenerationMetrics _metrics;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentEmbeddingService> _logger;

    public EnrollmentEmbeddingService(
        IEnrollmentArtifactResolver artifactResolver,
        IEmbeddingEngine embeddingEngine,
        IEmbeddingValidator validator,
        IEmbeddingNormalizer normalizer,
        IEmbeddingQualityAnalyzer qualityAnalyzer,
        IEmbeddingGenerationMetrics metrics,
        TimeProvider clock,
        ILogger<EnrollmentEmbeddingService> logger)
    {
        _artifactResolver = artifactResolver;
        _embeddingEngine = embeddingEngine;
        _validator = validator;
        _normalizer = normalizer;
        _qualityAnalyzer = qualityAnalyzer;
        _metrics = metrics;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentEmbeddingResult> GenerateAsync(
        EnrollmentEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var correlationId = request.CorrelationId ?? request.Manifest.CorrelationId;
        var pipelineVersion = request.PipelineVersion ?? request.Manifest.PipelineVersion;

        _logger.LogInformation(
            "Enrollment embedding started. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} ArtifactType={ArtifactType}",
            request.StudentId,
            request.BatchId,
            correlationId,
            pipelineVersion,
            request.ArtifactType);

        if (!string.Equals(request.ArtifactType, EnrollmentArtifactTypeNames.AlignedFace, StringComparison.Ordinal))
        {
            return LogAndFail(
                totalStopwatch,
                request,
                correlationId,
                pipelineVersion,
                EnrollmentEmbeddingFailureCodes.UnsupportedArtifact,
                $"Artifact type '{request.ArtifactType}' is not supported for enrollment embedding.");
        }

        var resolveStopwatch = Stopwatch.StartNew();
        var resolveResult = await _artifactResolver.ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = request.Manifest,
            ArtifactType = request.ArtifactType,
            CorrelationId = correlationId,
            PipelineVersion = pipelineVersion,
        }, cancellationToken);
        resolveStopwatch.Stop();

        if (!resolveResult.Success || resolveResult.Artifact is null)
        {
            var code = MapResolveFailureCode(resolveResult.FailureCode);
            return LogAndFail(
                totalStopwatch,
                request,
                correlationId,
                pipelineVersion,
                code,
                resolveResult.FailureReason ?? "Aligned face artifact could not be resolved.");
        }

        await using var artifact = resolveResult.Artifact;

        if (!artifact.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return LogAndFail(
                totalStopwatch,
                request,
                correlationId,
                pipelineVersion,
                EnrollmentEmbeddingFailureCodes.UnsupportedArtifact,
                $"Resolved artifact content type '{artifact.ContentType}' is not an image.");
        }

        try
        {
            var engineResult = await _embeddingEngine.GenerateFromAlignedFaceAsync(artifact.Content, cancellationToken);

            var normalizationStopwatch = Stopwatch.StartNew();
            float[] normalizedVector;
            try
            {
                normalizedVector = _normalizer.Normalize(engineResult.EmbeddingVector);
            }
            catch (InvalidOperationException ex)
            {
                return LogAndFail(
                    totalStopwatch,
                    request,
                    correlationId,
                    pipelineVersion,
                    EnrollmentEmbeddingFailureCodes.InvalidVector,
                    ex.Message);
            }

            normalizationStopwatch.Stop();

            var validationStopwatch = Stopwatch.StartNew();
            var validation = _validator.ValidateNormalized(normalizedVector, _embeddingEngine.ExpectedDimension);
            validationStopwatch.Stop();

            if (!validation.IsValid)
            {
                var code = validation.FailureReason?.Contains("dimension", StringComparison.OrdinalIgnoreCase) == true
                    ? EnrollmentEmbeddingFailureCodes.InvalidDimension
                    : EnrollmentEmbeddingFailureCodes.InvalidVector;

                return LogAndFail(
                    totalStopwatch,
                    request,
                    correlationId,
                    pipelineVersion,
                    code,
                    validation.FailureReason ?? "Embedding validation failed.");
            }

            var statistics = _validator.ComputeStatistics(normalizedVector);
            var quality = _qualityAnalyzer.Analyze(normalizedVector, statistics);
            var warnings = quality.Diagnostics.Count == 0 ? Array.Empty<string>() : quality.Diagnostics.ToArray();

            totalStopwatch.Stop();

            var metadata = new EmbeddingMetadata
            {
                Model = _embeddingEngine.ModelName,
                ModelVersion = _embeddingEngine.ModelVersion,
                EmbeddingDimension = statistics.Dimension,
                Normalization = _embeddingEngine.NormalizationMethod,
                FrameworkVersion = InsightFaceEmbeddingEngine.ResolveFrameworkVersion(),
                OnnxVersion = InsightFaceEmbeddingEngine.ResolveOnnxVersion(),
                InferenceProvider = "OnnxRuntime",
                ExecutionDevice = "CPU",
                ExecutionTime = TimeSpan.FromMilliseconds(engineResult.InferenceMilliseconds),
            };

            var embeddingArtifact = new EnrollmentEmbeddingArtifact
            {
                StudentId = request.StudentId,
                BatchId = request.BatchId,
                EmbeddingVector = Array.AsReadOnly((float[])normalizedVector.Clone()),
                EmbeddingDimension = statistics.Dimension,
                EmbeddingModel = _embeddingEngine.ModelName,
                EmbeddingModelVersion = ComposePlatformEmbeddingVersion(_embeddingEngine),
                PipelineVersion = request.Manifest.PipelineVersion,
                ValidationVersion = request.Manifest.ValidationVersion,
                StorageVersion = request.Manifest.StorageVersion,
                ArtifactVersion = artifact.Version,
                ManifestVersion = request.Manifest.ManifestVersion,
                QualityScore = quality.QualityScore,
                CorrelationId = correlationId,
                EmbeddingDuration = totalStopwatch.Elapsed,
                CreatedUtc = _clock.GetUtcNow(),
            };

            var telemetry = new EnrollmentEmbeddingTelemetry
            {
                ResolveDuration = resolveStopwatch.Elapsed,
                InferenceDuration = TimeSpan.FromMilliseconds(engineResult.InferenceMilliseconds),
                NormalizationDuration = normalizationStopwatch.Elapsed,
                ValidationDuration = validationStopwatch.Elapsed,
                TotalDuration = totalStopwatch.Elapsed,
            };

            _metrics.RecordSuccess(
                _embeddingEngine.EngineName,
                metadata.Model,
                statistics.Dimension,
                engineResult.InferenceMilliseconds,
                (long)normalizationStopwatch.ElapsedMilliseconds,
                (long)validationStopwatch.ElapsedMilliseconds,
                retryCount: 0);

            _logger.LogInformation(
                "Enrollment embedding completed. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} EmbeddingDimension={EmbeddingDimension} ModelVersion={ModelVersion} DurationMs={DurationMs}",
                request.StudentId,
                request.BatchId,
                correlationId,
                pipelineVersion,
                statistics.Dimension,
                metadata.ModelVersion,
                totalStopwatch.ElapsedMilliseconds);

            return EnrollmentEmbeddingResult.Succeeded(
                embeddingArtifact,
                metadata,
                statistics,
                warnings,
                telemetry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            _metrics.RecordFailure(_embeddingEngine.EngineName, _embeddingEngine.ModelName, retryCount: 0);

            _logger.LogError(
                ex,
                "Enrollment embedding failed. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} DurationMs={DurationMs}",
                request.StudentId,
                request.BatchId,
                correlationId,
                pipelineVersion,
                totalStopwatch.ElapsedMilliseconds);

            return EnrollmentEmbeddingResult.Failed(
                EnrollmentEmbeddingFailureCodes.EmbeddingFailure,
                ex.Message);
        }
    }

    private EnrollmentEmbeddingResult LogAndFail(
        Stopwatch totalStopwatch,
        EnrollmentEmbeddingRequest request,
        Guid correlationId,
        int pipelineVersion,
        string code,
        string reason)
    {
        totalStopwatch.Stop();
        _metrics.RecordFailure(_embeddingEngine.EngineName, _embeddingEngine.ModelName, retryCount: 0);

        _logger.LogWarning(
            "Enrollment embedding failed. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} FailureCode={FailureCode} DurationMs={DurationMs} Reason={Reason}",
            request.StudentId,
            request.BatchId,
            correlationId,
            pipelineVersion,
            code,
            totalStopwatch.ElapsedMilliseconds,
            reason);

        return EnrollmentEmbeddingResult.Failed(code, reason);
    }

    private static string MapResolveFailureCode(string? resolveCode) =>
        resolveCode switch
        {
            EnrollmentArtifactResolveCodes.UnsupportedArtifact => EnrollmentEmbeddingFailureCodes.UnsupportedArtifact,
            EnrollmentArtifactResolveCodes.ArtifactMissing => EnrollmentEmbeddingFailureCodes.ArtifactMissing,
            _ => EnrollmentEmbeddingFailureCodes.ArtifactMissing,
        };

    private static string ComposePlatformEmbeddingVersion(IEmbeddingEngine engine)
    {
        var modelFamily = engine.ModelName
            .Replace(".onnx", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', '-')
            .ToLowerInvariant();

        return $"{engine.EngineName.ToLowerInvariant()}-{modelFamily}-v{engine.ModelVersion.ToLowerInvariant()}";
    }
}
