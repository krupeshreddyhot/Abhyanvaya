using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization;

/// <summary>
/// Readiness placeholder strategy. Does not optimize, generate, or mutate timetables.
/// Future Phase 3 strategies replace this via DI registration of <see cref="IOptimizationStrategy"/>.
/// </summary>
public sealed class NoOpOptimizationStrategy : IOptimizationStrategy
{
    public string StrategyCode => "NONE";
    public string StrategyName => "No Optimization (Readiness)";
    public OptimizationStrategyKind Kind => OptimizationStrategyKind.None;
    public bool IsImplemented => false;

    public Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var execution = new OptimizationExecution
        {
            ExecutionId = Guid.NewGuid(),
            StrategyKind = Kind,
            StartedUtc = started,
            CompletedUtc = DateTime.UtcNow,
            ExecutionTimeMs = 0,
            ScoringTimeMs = 0,
            Outcome = "ReadinessOnly"
        };

        var result = new OptimizationResult
        {
            Execution = execution,
            Summary = new OptimizationSummary
            {
                CandidateCount = 0,
                BaselineScore = 0,
                BestProjectedScore = 0,
                ImprovementDelta = 0,
                BaselineConflictCount = context.ConflictCount,
                ProjectedConflictCount = context.ConflictCount,
                StatusMessage = "Phase 2B.6 readiness: strategy contracts registered; optimizer not implemented."
            },
            BaselineScore = new OptimizationScore { TotalScore = 0, NormalizedScore = 0, Dimensions = [] },
            ProjectedScore = null,
            Candidates = []
        };

        return Task.FromResult(result);
    }
}
