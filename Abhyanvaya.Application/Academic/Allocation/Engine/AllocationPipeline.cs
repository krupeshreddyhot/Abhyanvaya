using System.Diagnostics;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Executes only enabled strategies in deterministic order.</summary>
public sealed class AllocationPipeline
{
    private readonly IReadOnlyList<IAllocationPipelineStrategy> _strategies;

    public AllocationPipeline(IEnumerable<IAllocationPipelineStrategy> strategies)
        => _strategies = strategies.OrderBy(s => s.Order).ThenBy(s => s.StrategyCode, StringComparer.Ordinal).ToList();

    public async Task RunAsync(
        AllocationWorkingState state,
        IProgress<AllocationProgress>? progress,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var enabled = _strategies.Where(s => state.Config.IsStrategyEnabled(s.StrategyCode)).ToList();
        var total = Math.Max(1, enabled.Count);
        var index = 0;

        foreach (var strategy in _strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabledFlag = state.Config.IsStrategyEnabled(strategy.StrategyCode);
            if (!enabledFlag)
            {
                state.TraceSteps.Add(new AllocationTraceStep
                {
                    Order = strategy.Order,
                    StrategyCode = strategy.StrategyCode,
                    Enabled = false,
                    Executed = false,
                    Summary = "Skipped (disabled by configuration).",
                });
                continue;
            }

            index++;
            progress?.Report(new AllocationProgress
            {
                SessionId = sessionId,
                CurrentStrategy = strategy.DisplayName,
                ProgressPercent = (int)(index * 100.0 / total),
                StudentsProcessed = state.Assignments.Count,
                TotalStudents = state.OrderedStudentIds.Count,
                CurrentScore = state.CurrentScore.TotalScore,
                Message = $"Running {strategy.DisplayName}",
            });

            var sw = Stopwatch.StartNew();
            await strategy.ApplyAsync(state, cancellationToken);
            sw.Stop();

            state.TraceSteps.Add(new AllocationTraceStep
            {
                Order = strategy.Order,
                StrategyCode = strategy.StrategyCode,
                Enabled = true,
                Executed = true,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                ScoreAfter = state.CurrentScore.TotalScore,
                Summary = $"{strategy.DisplayName} completed.",
                ConstraintNotes = state.ConstraintEvals.Select(c => $"{c.ConstraintCode}:{c.Satisfied}").ToList(),
            });
        }
    }
}
