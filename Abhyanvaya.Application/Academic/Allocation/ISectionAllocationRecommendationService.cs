namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C extension point — allocation strategy recommendations for merge/split/readiness.
/// AI29.1B defines the contract only; strategies are implemented in AI29.1C.
/// </summary>
public interface ISectionAllocationRecommendationService
{
    Task<IReadOnlyList<SectionAllocationRecommendation>> RecommendForSplitAsync(
        SectionAllocationSplitContext context,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SectionAllocationRecommendation>> RecommendForMergeAsync(
        SectionAllocationMergeContext context,
        CancellationToken cancellationToken = default);
}

public sealed class SectionAllocationSplitContext
{
    public int SourceSectionId { get; init; }
    public string StrategyCode { get; init; } = "Manual";
    public int ChildCount { get; init; } = 2;
    public int StudentCount { get; init; }
}

public sealed class SectionAllocationMergeContext
{
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public int TargetSectionId { get; init; }
}

public sealed class SectionAllocationRecommendation
{
    public string StrategyCode { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyDictionary<string, int> ProposedCountsByKey { get; init; }
        = new Dictionary<string, int>();
}

/// <summary>Null-object placeholder until AI29.1C ships strategies.</summary>
public sealed class NullSectionAllocationRecommendationService : ISectionAllocationRecommendationService
{
    public Task<IReadOnlyList<SectionAllocationRecommendation>> RecommendForSplitAsync(
        SectionAllocationSplitContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SectionAllocationRecommendation>>([
            new SectionAllocationRecommendation
            {
                StrategyCode = context.StrategyCode,
                Summary = "AI29.1C allocation strategies not yet implemented; manual planning only.",
            }
        ]);

    public Task<IReadOnlyList<SectionAllocationRecommendation>> RecommendForMergeAsync(
        SectionAllocationMergeContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SectionAllocationRecommendation>>([
            new SectionAllocationRecommendation
            {
                StrategyCode = "Manual",
                Summary = "AI29.1C allocation strategies not yet implemented; merge preserves lineage only.",
            }
        ]);
}
