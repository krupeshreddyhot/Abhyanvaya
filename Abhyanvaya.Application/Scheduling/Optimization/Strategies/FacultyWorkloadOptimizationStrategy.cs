using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Strategies;

/// <summary>
/// Balances daily/weekly faculty load and continuous stretches by proposing day moves.
/// Recommendations only — never edits production.
/// </summary>
public sealed class FacultyWorkloadOptimizationStrategy : IOptimizationStrategy
{
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public FacultyWorkloadOptimizationStrategy(IOptimizationScoreCalculator scoreCalculator) =>
        _scoreCalculator = scoreCalculator;

    public string StrategyCode => "WORKLOAD";
    public string StrategyName => "Faculty Workload Balancing";
    public OptimizationStrategyKind Kind => OptimizationStrategyKind.WorkloadBalancing;
    public bool IsImplemented => true;

    public Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var working = OptimizationWorkingSet.CloneEntries(context.WorkingEntries);
        var candidates = new List<OptimizationCandidate>();
        var days = working.Select(e => e.DayOfWeek).Distinct().OrderBy(d => d).ToList();
        if (days.Count < 2)
            return Task.FromResult(BuildResult(context, working, candidates, started));

        foreach (var staffId in working.Select(e => e.StaffId).Distinct().Take(20))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var byDay = working.Where(e => e.StaffId == staffId).GroupBy(e => e.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.ToList());
            if (byDay.Count == 0) continue;

            var heaviest = byDay.OrderByDescending(kv => kv.Value.Count).First();
            var lightestDay = days.OrderBy(d => byDay.TryGetValue(d, out var list) ? list.Count : 0).First();
            if (heaviest.Key == lightestDay || heaviest.Value.Count - (byDay.TryGetValue(lightestDay, out var l) ? l.Count : 0) < 2)
                continue;

            var move = heaviest.Value
                .OrderByDescending(e => CountAdjacent(working, e))
                .FirstOrDefault();
            if (move is null) continue;

            var freeSlot = working
                .Where(e => e.DayOfWeek == lightestDay)
                .Select(e => e.TimeSlotId)
                .Distinct()
                .FirstOrDefault(slot =>
                    !working.Any(e =>
                        e.StaffId == staffId && e.DayOfWeek == lightestDay && e.TimeSlotId == slot) &&
                    !working.Any(e =>
                        e.GroupId == move.GroupId && e.DayOfWeek == lightestDay && e.TimeSlotId == slot) &&
                    !working.Any(e =>
                        e.RoomId == move.RoomId && e.DayOfWeek == lightestDay && e.TimeSlotId == slot));

            if (freeSlot == 0)
            {
                // Keep same slot if free on target day
                freeSlot = move.TimeSlotId;
                var blocked = working.Any(e =>
                    e.EntryId != move.EntryId &&
                    e.DayOfWeek == lightestDay &&
                    e.TimeSlotId == freeSlot &&
                    (e.StaffId == staffId || e.RoomId == move.RoomId || e.GroupId == move.GroupId));
                if (blocked) continue;
            }

            var candidate = new OptimizationCandidate
            {
                CandidateId = $"workload-day-{move.EntryId}-{lightestDay}",
                Description = $"Rebalance staff {staffId}: move entry {move.EntryId} to day {lightestDay}.",
                ProposedChangeSummaries =
                [
                    $"Entry {move.EntryId}: Day {move.DayOfWeek} → {lightestDay}",
                    $"TimeSlot {move.TimeSlotId} → {freeSlot}",
                    "Improves daily workload balance / continuous stretch relief."
                ],
                ChangeType = "DayRebalance",
                EntryId = move.EntryId,
                ProposedDayOfWeek = lightestDay,
                ProposedTimeSlotId = freeSlot,
                StrategyCode = StrategyCode
            };

            OptimizationWorkingSet.ApplyCandidate(working, candidate);
            candidates.Add(candidate);
            if (candidates.Count >= 15) break;
        }

        return Task.FromResult(BuildResult(context, working, candidates, started));
    }

    private static int CountAdjacent(IReadOnlyList<OptimizationEntrySnapshot> working, OptimizationEntrySnapshot entry) =>
        working.Count(e =>
            e.StaffId == entry.StaffId &&
            e.DayOfWeek == entry.DayOfWeek &&
            e.EntryId != entry.EntryId);

    private OptimizationResult BuildResult(
        OptimizationContext context,
        List<OptimizationEntrySnapshot> working,
        List<OptimizationCandidate> candidates,
        DateTime started)
    {
        var conflicts = OptimizationWorkingSet.CountHardConflicts(working);
        var metrics = OptimizationWorkingSet.BuildMetrics(
            working, context.Rooms, context.TimeSlots, context.FacultyPreferredRoomIds, conflicts);
        var projectedContext = OptimizationWorkingSet.WithWorkingState(context, working, metrics, conflicts);
        var baseline = _scoreCalculator.Calculate(context);
        var projected = _scoreCalculator.Calculate(projectedContext);
        var completed = DateTime.UtcNow;

        return new OptimizationResult
        {
            Execution = new OptimizationExecution
            {
                ExecutionId = Guid.NewGuid(),
                StrategyKind = Kind,
                StartedUtc = started,
                CompletedUtc = completed,
                ExecutionTimeMs = (long)(completed - started).TotalMilliseconds,
                Outcome = "SandboxProposal"
            },
            Summary = new OptimizationSummary
            {
                CandidateCount = candidates.Count,
                BaselineScore = baseline.Score.NormalizedScore,
                BestProjectedScore = projected.Score.NormalizedScore,
                ImprovementDelta = projected.Score.NormalizedScore - baseline.Score.NormalizedScore,
                BaselineConflictCount = context.ConflictCount,
                ProjectedConflictCount = conflicts,
                StatusMessage = $"Workload strategy proposed {candidates.Count} rebalance moves."
            },
            BaselineScore = baseline.Score,
            ProjectedScore = projected.Score,
            Candidates = candidates
        };
    }
}
