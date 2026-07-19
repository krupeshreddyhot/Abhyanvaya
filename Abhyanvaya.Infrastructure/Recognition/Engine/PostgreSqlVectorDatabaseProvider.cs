using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

/// <summary>
/// PostgreSQL-backed vector search using real[] embeddings.
/// Computes Top-K in-process; swappable with pgvector ANN index via <see cref="IVectorDatabaseProvider"/>.
/// </summary>
public sealed class PostgreSqlVectorDatabaseProvider : IVectorDatabaseProvider
{
    private readonly SimilarityEngine _similarityEngine;
    private readonly ILogger<PostgreSqlVectorDatabaseProvider> _logger;

    public PostgreSqlVectorDatabaseProvider(
        SimilarityEngine similarityEngine,
        ILogger<PostgreSqlVectorDatabaseProvider> logger)
    {
        _similarityEngine = similarityEngine;
        _logger = logger;
    }

    public string ProviderName => "PostgreSQL";

    public Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        var provider = _similarityEngine.ResolveProvider(request.Metric);
        var query = request.QueryEmbedding;

        var scored = new List<(RecognitionCandidate Candidate, float Distance, float Score)>();

        foreach (var candidate in request.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (candidate.EmbeddingVector.Length == 0 || candidate.EmbeddingVector.Length != query.Length)
            {
                continue;
            }

            var distance = provider.ComputeDistance(query, candidate.EmbeddingVector);
            var score = provider.NormalizeScore(distance);
            scored.Add((candidate, distance, score));
        }

        var topK = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Distance)
            .Take(Math.Max(1, request.TopK))
            .Select((entry, index) => new RecognitionSearchResult
            {
                StudentId = entry.Candidate.StudentId,
                EmbeddingId = entry.Candidate.EmbeddingId,
                SimilarityScore = entry.Score,
                Rank = index + 1,
                Distance = entry.Distance,
                Metadata = entry.Candidate.Metadata,
            })
            .ToList();

        stopwatch.Stop();

        _logger.LogInformation(
            "Vector search completed. Provider={Provider} CandidatesSearched={CandidatesSearched} TopK={TopK} ResultCount={ResultCount} DurationMs={DurationMs}",
            ProviderName,
            request.Candidates.Count,
            request.TopK,
            topK.Count,
            stopwatch.ElapsedMilliseconds);

        return Task.FromResult(new VectorSearchResponse
        {
            Results = topK,
            Duration = stopwatch.Elapsed,
            CandidatesSearched = request.Candidates.Count,
        });
    }
}
