using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Orchestrates provider resolution, generation, validation, normalization, and storage.
/// </summary>
public sealed class EmbeddingPipeline : IEmbeddingPipeline
{
    private readonly IEmbeddingProviderFactory _providerFactory;
    private readonly IEmbeddingValidator _validator;
    private readonly IEmbeddingNormalizer _normalizer;
    private readonly IEmbeddingStorage _storage;
    private readonly IApplicationDbContext _context;
    private readonly IEmbeddingGenerationMetrics _metrics;
    private readonly ILogger<EmbeddingPipeline> _logger;

    public EmbeddingPipeline(
        IEmbeddingProviderFactory providerFactory,
        IEmbeddingValidator validator,
        IEmbeddingNormalizer normalizer,
        IEmbeddingStorage storage,
        IApplicationDbContext context,
        IEmbeddingGenerationMetrics metrics,
        ILogger<EmbeddingPipeline> logger)
    {
        _providerFactory = providerFactory;
        _validator = validator;
        _normalizer = normalizer;
        _storage = storage;
        _context = context;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task GenerateAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default)
    {
        IEmbeddingGenerator provider;
        try
        {
            provider = _providerFactory.GetDefaultProvider();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "No embedding provider available; marking pending. StudentId={StudentId} TenantId={TenantId}",
                message.StudentId,
                message.TenantId);

            await _storage.MarkPendingAsync(message, cancellationToken);
            return;
        }

        var embeddingId = await _storage.MarkProcessingAsync(message, cancellationToken);
        var photoVersion = await ResolvePhotoVersionAsync(message.StudentId, cancellationToken);

        while (true)
        {
            var generationStopwatch = Stopwatch.StartNew();
            long normalizationDurationMs = 0;
            long validationDurationMs = 0;

            try
            {
                var request = new EmbeddingGenerationRequest(message.TenantId, message.StudentId, message.PhotoKey);

                _logger.LogInformation(
                    "Generating face embedding. StudentId={StudentId} TenantId={TenantId} Provider={Provider} Model={Model}",
                    message.StudentId,
                    message.TenantId,
                    provider.ProviderName,
                    provider.ModelName);

                var result = await provider.GenerateAsync(request, cancellationToken);
                generationStopwatch.Stop();

                var validationStopwatch = Stopwatch.StartNew();
                var validation = _validator.Validate(result.EmbeddingVector, result.ExpectedDimension);
                validationStopwatch.Stop();
                validationDurationMs = validationStopwatch.ElapsedMilliseconds;

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.FailureReason ?? "Embedding validation failed.");
                }

                var normalizationStopwatch = Stopwatch.StartNew();
                var normalized = _normalizer.Normalize(result.EmbeddingVector);
                normalizationStopwatch.Stop();
                normalizationDurationMs = normalizationStopwatch.ElapsedMilliseconds;

                await _storage.ResetRetryCountAsync(embeddingId, cancellationToken);
                await _storage.StoreCompletedAsync(
                    message,
                    embeddingId,
                    normalized,
                    result,
                    photoVersion,
                    cancellationToken);

                _metrics.RecordSuccess(
                    provider.ProviderName,
                    result.Model,
                    normalized.Length,
                    generationStopwatch.ElapsedMilliseconds,
                    normalizationDurationMs,
                    validationDurationMs,
                    retryCount: 0);

                _logger.LogInformation(
                    "Face embedding pipeline completed. StudentId={StudentId} Provider={Provider} Model={Model} EmbeddingDimension={EmbeddingDimension} GenerationDurationMs={GenerationDurationMs} NormalizationDurationMs={NormalizationDurationMs} ValidationResult={ValidationResult} RetryCount={RetryCount}",
                    message.StudentId,
                    provider.ProviderName,
                    result.Model,
                    normalized.Length,
                    generationStopwatch.ElapsedMilliseconds,
                    normalizationDurationMs,
                    validation.IsValid,
                    0);

                LogMetricsSnapshot();
                return;
            }
            catch (Exception ex)
            {
                generationStopwatch.Stop();
                var reason = ex.Message;

                await _storage.RecordFailureAsync(message, embeddingId, reason, cancellationToken);

                var snapshotEmbedding = await _context.StudentFaceEmbeddings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == embeddingId, cancellationToken);

                var retryCount = snapshotEmbedding?.RetryCount ?? EmbeddingStorage.MaxRetryCount;

                _metrics.RecordFailure(provider.ProviderName, provider.ModelName, retryCount);

                _logger.LogError(
                    ex,
                    "Face embedding pipeline attempt failed. StudentId={StudentId} Provider={Provider} Model={Model} RetryCount={RetryCount} ValidationResult={ValidationResult}",
                    message.StudentId,
                    provider.ProviderName,
                    provider.ModelName,
                    retryCount,
                    false);

                if (retryCount >= EmbeddingStorage.MaxRetryCount)
                {
                    LogMetricsSnapshot();
                    return;
                }
            }
        }
    }

    private void LogMetricsSnapshot()
    {
        var snapshot = _metrics.GetSnapshot();
        _logger.LogInformation(
            "Embedding generation metrics. SuccessfulEmbeddings={SuccessfulEmbeddings} FailedEmbeddings={FailedEmbeddings} AverageGenerationTimeMs={AverageGenerationTimeMs} AverageRetries={AverageRetries}",
            snapshot.SuccessfulEmbeddings,
            snapshot.FailedEmbeddings,
            snapshot.AverageGenerationTimeMs,
            snapshot.AverageRetries);
    }

    private async Task<long> ResolvePhotoVersionAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        return student?.PhotoUploadedUtc?.Ticks ?? 0L;
    }
}
