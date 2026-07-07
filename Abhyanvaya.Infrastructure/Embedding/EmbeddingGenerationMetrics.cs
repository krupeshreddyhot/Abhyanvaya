using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Thread-safe in-process counters for embedding generation observability.
/// </summary>
public sealed class EmbeddingGenerationMetrics : IEmbeddingGenerationMetrics
{
    private long _successfulEmbeddings;
    private long _failedEmbeddings;
    private long _totalGenerationTimeMs;
    private long _totalRetries;

    public void RecordSuccess(
        string provider,
        string model,
        int embeddingDimension,
        long generationDurationMs,
        long normalizationDurationMs,
        long validationDurationMs,
        int retryCount)
    {
        Interlocked.Increment(ref _successfulEmbeddings);
        Interlocked.Add(ref _totalGenerationTimeMs, generationDurationMs);
        Interlocked.Add(ref _totalRetries, retryCount);
    }

    public void RecordFailure(string provider, string? model, int retryCount)
    {
        Interlocked.Increment(ref _failedEmbeddings);
        Interlocked.Add(ref _totalRetries, retryCount);
    }

    public EmbeddingMetricsSnapshot GetSnapshot()
    {
        var successes = Interlocked.Read(ref _successfulEmbeddings);
        var failures = Interlocked.Read(ref _failedEmbeddings);
        var totalGenerationMs = Interlocked.Read(ref _totalGenerationTimeMs);
        var totalRetries = Interlocked.Read(ref _totalRetries);
        var totalAttempts = successes + failures;

        var averageGeneration = successes > 0 ? (double)totalGenerationMs / successes : 0d;
        var averageRetries = totalAttempts > 0 ? (double)totalRetries / totalAttempts : 0d;

        return new EmbeddingMetricsSnapshot(successes, failures, averageGeneration, averageRetries);
    }
}
