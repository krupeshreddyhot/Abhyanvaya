namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1B.7 / AI29.1C — Legacy strategy evaluation extension point.</summary>
public interface IAllocationStrategy
{
    string StrategyCode { get; }
    string DisplayName { get; }
    Task<AllocationStrategyResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default);
}

/// <summary>AI29.1B.7 / AI29.1C — Constraint extension point (scenario-aware in 1C).</summary>
public interface IAllocationConstraint
{
    string ConstraintCode { get; }
    string DisplayName { get; }
    Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default);

    Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationConstraintPriority priority,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode,
            Priority = priority,
            Satisfied = true,
            Summary = "Default scenario evaluation.",
        });
}

/// <summary>AI29.1B.7 / AI29.1C — Scoring provider.</summary>
public interface IAllocationScoringProvider
{
    string ProviderCode { get; }
    Task<AllocationScoreResult> ScoreAsync(SectionAllocationContext context, CancellationToken cancellationToken = default);
}

/// <summary>AI29.1B.7 / AI29.1C — Recommendation provider.</summary>
public interface IAllocationRecommendationProvider
{
    string ProviderCode { get; }
    Task<IReadOnlyList<string>> RecommendAsync(SectionAllocationContext context, CancellationToken cancellationToken = default);
}

public sealed class AllocationStrategyResult
{
    public string StrategyCode { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool IsNoOp { get; init; }
}

public sealed class AllocationConstraintResult
{
    public string ConstraintCode { get; init; } = "";
    public bool Satisfied { get; init; } = true;
    public string Summary { get; init; } = "";
}

public sealed class AllocationScoreResult
{
    public string ProviderCode { get; init; } = "";
    public double Score { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class NoOpAllocationStrategy : IAllocationStrategy
{
    public string StrategyCode => "NoOp";
    public string DisplayName => "No-Op Strategy";
    public Task<AllocationStrategyResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationStrategyResult { StrategyCode = StrategyCode, IsNoOp = true, Summary = "NoOp" });
}

public sealed class NoOpAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "NoOp";
    public string DisplayName => "No-Op Constraint";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode });
}

public sealed class NoOpAllocationScoringProvider : IAllocationScoringProvider
{
    public string ProviderCode => "NoOp";
    public Task<AllocationScoreResult> ScoreAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationScoreResult { ProviderCode = ProviderCode });
}

/// <summary>AI29.1C — Context-level recommendation provider (non-executing).</summary>
public sealed class ContextAllocationRecommendationProvider : IAllocationRecommendationProvider
{
    public string ProviderCode => "Context";
    public Task<IReadOnlyList<string>> RecommendAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(context.Recommendations);
}

/// <summary>Immutable registry of constraint descriptors.</summary>
public static class AllocationConstraintRegistry
{
    public static IReadOnlyList<AllocationConstraintDescriptor> All { get; } =
    [
        new("Capacity", "Capacity", "Respect maximum/available capacity"),
        new("GenderBalance", "Gender Balance", "Balance gender distribution across sections"),
        new("Language", "Language", "Respect medium/language preferences"),
        new("Hostel", "Hostel", "Hostel co-location preferences"),
        new("Transport", "Transport", "Transport route constraints"),
        new("Merit", "Merit", "Merit/rank based placement"),
        new("Scholarship", "Scholarship", "Scholarship cohort constraints"),
        new("ElectiveCombination", "Elective Combination", "Elective subject combinations"),
        new("MinorSubject", "Minor Subject", "Minor subject cohort constraints"),
        new("ReservedSeats", "Reserved Seats", "Respect reserved seat hard limits"),
    ];
}

public sealed record AllocationConstraintDescriptor(string Code, string DisplayName, string Description);
