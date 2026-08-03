using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;
using System.Diagnostics;

namespace Abhyanvaya.Application.Scheduling.Optimization.Pipeline;

public interface IOptimizationPipeline
{
    Task<OptimizationExecutionResult> RunAsync(
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic strategy pipeline:
/// Conflict Detection → Conflict Intelligence → Greedy → Workload → Room → Preference → Scoring → Sandbox.
/// No AI / genetic algorithms.
/// </summary>
public sealed class OptimizationPipeline : IOptimizationPipeline
{
    private readonly IEnumerable<IOptimizationStrategy> _strategies;
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public OptimizationPipeline(
        IEnumerable<IOptimizationStrategy> strategies,
        IOptimizationScoreCalculator scoreCalculator)
    {
        _strategies = strategies;
        _scoreCalculator = scoreCalculator;
    }

    public async Task<OptimizationExecutionResult> RunAsync(
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var session = context.Session;
        var working = OptimizationWorkingSet.CloneEntries(context.WorkingContext.WorkingEntries);

        context.BaselineConflictCount = context.WorkingContext.ConflictCount;
        context.BaselineScore = _scoreCalculator.Calculate(context.WorkingContext).Score;
        context.CurrentScore = context.BaselineScore;

        Report(context, sw, "ConflictDetection", 5, "Baseline conflicts loaded.");
        Report(context, sw, "ConflictIntelligence", 10, "Conflict intelligence stage acknowledged (advisory only).");

        var orderedKinds = new[]
        {
            OptimizationStrategyKind.Greedy,
            OptimizationStrategyKind.WorkloadBalancing,
            OptimizationStrategyKind.RoomOptimization,
            OptimizationStrategyKind.PreferenceOptimization
        };

        var progressMarks = new[] { 30, 50, 70, 85 };
        for (var i = 0; i < orderedKinds.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = orderedKinds[i];
            var strategy = _strategies.FirstOrDefault(s => s.Kind == kind && s.IsImplemented);
            if (strategy is null)
            {
                context.IntermediateResults.Add(new OptimizationIntermediateResult
                {
                    StrategyCode = kind.ToString().ToUpperInvariant(),
                    StrategyName = kind.ToString(),
                    Kind = kind,
                    Message = "Strategy not registered."
                });
                continue;
            }

            Report(context, sw, strategy.StrategyName, progressMarks[i], $"Running {strategy.StrategyName}...");

            var stepContext = RefreshContext(context.WorkingContext, working);
            var stepSw = Stopwatch.StartNew();
            var stepResult = await strategy.ProposeAsync(stepContext, context.Request, cancellationToken);
            stepSw.Stop();

            OptimizationWorkingSet.ApplyAll(working, stepResult.Candidates);
            foreach (var c in stepResult.Candidates)
                context.AccumulatedCandidates.Add(c);

            var conflictsAfter = OptimizationWorkingSet.CountHardConflicts(working);
            var metrics = OptimizationWorkingSet.BuildMetrics(
                working,
                context.WorkingContext.Rooms,
                context.WorkingContext.TimeSlots,
                context.WorkingContext.FacultyPreferredRoomIds,
                conflictsAfter);
            context.WorkingContext = OptimizationWorkingSet.WithWorkingState(
                context.WorkingContext, working, metrics, conflictsAfter);
            context.CurrentScore = _scoreCalculator.Calculate(context.WorkingContext).Score;

            context.IntermediateResults.Add(new OptimizationIntermediateResult
            {
                StrategyCode = strategy.StrategyCode,
                StrategyName = strategy.StrategyName,
                Kind = strategy.Kind,
                CandidateCount = stepResult.Candidates.Count,
                ScoreAfter = context.CurrentScore.NormalizedScore,
                ConflictCountAfter = conflictsAfter,
                ElapsedMs = stepSw.ElapsedMilliseconds,
                Message = stepResult.Summary.StatusMessage
            });
        }

        Report(context, sw, "Scoring", 92, "Final scoring with Phase 2B.6 calculator.");
        var finalConflicts = OptimizationWorkingSet.CountHardConflicts(working);
        var finalMetrics = OptimizationWorkingSet.BuildMetrics(
            working,
            context.WorkingContext.Rooms,
            context.WorkingContext.TimeSlots,
            context.WorkingContext.FacultyPreferredRoomIds,
            finalConflicts);
        context.WorkingContext = OptimizationWorkingSet.WithWorkingState(
            context.WorkingContext, working, finalMetrics, finalConflicts);
        var projected = _scoreCalculator.Calculate(context.WorkingContext).Score;
        context.CurrentScore = projected;

        var comparison = OptimizationComparison.Build(
            context.BaselineScore!,
            projected,
            context.BaselineConflictCount,
            finalConflicts,
            context.WorkingContext.BaselineMetrics,
            finalMetrics);

        var combined = new OptimizationResult
        {
            Execution = new OptimizationExecution
            {
                ExecutionId = session.RunId,
                StrategyKind = OptimizationStrategyKind.Pipeline,
                StartedUtc = session.StartedUtc,
                CompletedUtc = DateTime.UtcNow,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                Outcome = "SandboxProposal"
            },
            Summary = new OptimizationSummary
            {
                CandidateCount = context.AccumulatedCandidates.Count,
                BaselineScore = context.BaselineScore!.NormalizedScore,
                BestProjectedScore = projected.NormalizedScore,
                ImprovementDelta = projected.NormalizedScore - context.BaselineScore.NormalizedScore,
                BaselineConflictCount = context.BaselineConflictCount,
                ProjectedConflictCount = finalConflicts,
                StatusMessage = "Pipeline completed. Results require sandbox review and explicit approval."
            },
            BaselineScore = context.BaselineScore,
            ProjectedScore = projected,
            Candidates = context.AccumulatedCandidates.ToList()
        };

        Report(context, sw, "OptimizationSandbox", 98, "Ready to persist sandbox scenario.");

        return new OptimizationExecutionResult
        {
            RunId = session.RunId,
            SessionId = session.SessionId,
            Status = OptimizationEngineRunStatus.Completed,
            CombinedResult = combined,
            IntermediateResults = context.IntermediateResults.ToList(),
            Comparison = comparison,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private static OptimizationContext RefreshContext(
        OptimizationContext baseline,
        List<OptimizationEntrySnapshot> working)
    {
        var conflicts = OptimizationWorkingSet.CountHardConflicts(working);
        var metrics = OptimizationWorkingSet.BuildMetrics(
            working, baseline.Rooms, baseline.TimeSlots, baseline.FacultyPreferredRoomIds, conflicts);
        return OptimizationWorkingSet.WithWorkingState(baseline, working, metrics, conflicts);
    }

    private static void Report(
        OptimizationExecutionContext context,
        Stopwatch sw,
        string strategy,
        int percent,
        string message)
    {
        var improvement = (context.CurrentScore?.NormalizedScore ?? 0) - (context.BaselineScore?.NormalizedScore ?? 0);
        var remaining = percent >= 100 ? 0 : (long)(sw.ElapsedMilliseconds * (100.0 - percent) / Math.Max(percent, 1));
        context.ProgressCallback?.Invoke(new OptimizationProgress
        {
            RunId = context.Session.RunId,
            SessionId = context.Session.SessionId,
            CurrentStrategy = strategy,
            ProgressPercent = percent,
            ElapsedMs = sw.ElapsedMilliseconds,
            EstimatedRemainingMs = remaining,
            CurrentScore = context.CurrentScore?.NormalizedScore ?? 0,
            ImprovementDelta = improvement,
            StatusMessage = message,
            Status = OptimizationEngineRunStatus.Running
        });
    }
}

public static class OptimizationComparison
{
    public static OptimizationComparisonDto Build(
        OptimizationScore original,
        OptimizationScore optimized,
        int originalConflicts,
        int optimizedConflicts,
        IReadOnlyDictionary<string, decimal> originalMetrics,
        IReadOnlyDictionary<string, decimal> optimizedMetrics)
    {
        decimal Dim(OptimizationScore score, OptimizationDimension d) =>
            score.Dimensions.FirstOrDefault(x => x.Dimension == d)?.RawValue ?? 0;

        var highlights = new List<string>();
        var scoreDelta = optimized.NormalizedScore - original.NormalizedScore;
        var conflictDelta = originalConflicts - optimizedConflicts;
        if (scoreDelta != 0) highlights.Add($"Score improvement: {scoreDelta:0.##}.");
        if (conflictDelta != 0) highlights.Add($"Conflict reduction: {conflictDelta}.");

        return new OptimizationComparisonDto
        {
            OriginalScore = original.NormalizedScore,
            OptimizedScore = optimized.NormalizedScore,
            ScoreImprovement = scoreDelta,
            OriginalConflicts = originalConflicts,
            OptimizedConflicts = optimizedConflicts,
            ConflictReduction = conflictDelta,
            FacultySatisfactionDelta = Dim(optimized, OptimizationDimension.FacultySatisfaction) - Dim(original, OptimizationDimension.FacultySatisfaction),
            RoomUsageDelta = Dim(optimized, OptimizationDimension.RoomUtilization) - Dim(original, OptimizationDimension.RoomUtilization),
            TravelDelta = Dim(optimized, OptimizationDimension.TravelReduction) - Dim(original, OptimizationDimension.TravelReduction),
            BreaksDelta = optimizedMetrics.GetValueOrDefault("AverageBreak") - originalMetrics.GetValueOrDefault("AverageBreak"),
            Highlights = highlights
        };
    }
}
