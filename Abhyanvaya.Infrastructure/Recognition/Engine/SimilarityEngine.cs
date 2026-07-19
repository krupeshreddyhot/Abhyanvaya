using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class SimilarityEngine : ISimilarityEngine
{
    private readonly IEnumerable<ISimilarityProvider> _providers;

    public SimilarityEngine(IEnumerable<ISimilarityProvider> providers)
    {
        _providers = providers;
    }

    public Task<IReadOnlyList<SimilarityMatch>> RankAsync(
        IReadOnlyList<RecognitionSearchResult> searchResults,
        SimilarityMetric metric,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matches = searchResults
            .Select(r => new SimilarityMatch
            {
                StudentId = r.StudentId,
                EmbeddingId = r.EmbeddingId,
                NormalizedScore = r.SimilarityScore,
                RawDistance = r.Distance,
                Rank = r.Rank,
            })
            .OrderByDescending(m => m.NormalizedScore)
            .ThenBy(m => m.RawDistance)
            .Select((m, index) => m with { Rank = index + 1 })
            .ToList();

        return Task.FromResult<IReadOnlyList<SimilarityMatch>>(matches);
    }

    public SimilarityStatistics ComputeStatistics(IReadOnlyList<SimilarityMatch> matches)
    {
        if (matches.Count == 0)
        {
            return new SimilarityStatistics { MatchCount = 0 };
        }

        var scores = matches.Select(m => m.NormalizedScore).ToList();
        var best = scores.Max();
        var worst = scores.Min();
        var mean = scores.Average();
        return new SimilarityStatistics
        {
            BestScore = best,
            WorstScore = worst,
            MeanScore = (float)mean,
            ScoreSpread = best - worst,
            MatchCount = matches.Count,
        };
    }

    internal ISimilarityProvider ResolveProvider(SimilarityMetric metric) =>
        _providers.First(p => p.Metric == metric);
}
