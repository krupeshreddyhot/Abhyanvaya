namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

/// <summary>
/// Owns advisory recommendations only. Detection remains in <see cref="ConflictEngine"/>.
/// Never edits the timetable.
/// </summary>
public sealed class ConflictResolutionAdvisor : IConflictResolutionAdvisor
{
    private readonly IEnumerable<IConflictRecommendationProvider> _providers;

    public ConflictResolutionAdvisor(IEnumerable<IConflictRecommendationProvider> providers) =>
        _providers = providers;

    public async Task<ConflictResolutionAdvice> AdviseAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<ConflictRecommendation>();
        foreach (var provider in _providers.Where(p => p.CanHandle(conflict)))
        {
            var batch = await provider.RecommendAsync(conflict, context, cancellationToken);
            recommendations.AddRange(batch);
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new ConflictRecommendation
            {
                RecommendationId = $"GENERIC:{conflict.RuleCode}:{conflict.TimetableEntryId}",
                Title = "Manual resolution",
                Summary = conflict.Recommendation.SuggestedResolution,
                ProviderCode = "GENERIC",
                Options =
                [
                    new ResolutionOption
                    {
                        OptionCode = "OPEN_CELL",
                        Label = "Open timetable cell",
                        Description = conflict.Recommendation.SuggestedResolution,
                        NavigationPath = conflict.Recommendation.NavigationPath
                    }
                ],
                Score = new ResolutionScore
                {
                    Confidence = 0.35m,
                    Impact = Domain.Enums.Scheduling.ResolutionImpactLevel.Medium,
                    Difficulty = Domain.Enums.Scheduling.ResolutionDifficulty.Moderate,
                    Rank = 99
                },
                Reasons =
                [
                    new ResolutionReason { Code = "FALLBACK", Message = "No specialized provider matched; generic advisory guidance returned." }
                ],
                EstimatedResolution = "Manual review required",
                NavigationPath = conflict.Recommendation.NavigationPath
            });
        }

        return new ConflictResolutionAdvice
        {
            Conflict = conflict,
            Recommendations = recommendations
                .OrderBy(r => r.Score.Rank)
                .ThenByDescending(r => r.Score.Confidence)
                .ToList()
        };
    }

    public async Task<IReadOnlyList<ConflictResolutionAdvice>> AdviseManyAsync(
        IReadOnlyList<ConflictResult> conflicts,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var list = new List<ConflictResolutionAdvice>();
        foreach (var conflict in conflicts)
            list.Add(await AdviseAsync(conflict, context, cancellationToken));
        return list;
    }
}
