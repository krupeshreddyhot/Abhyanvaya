using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Strategies;

/// <summary>
/// Greedy local-search strategy. Proposes room/slot moves that reduce hard conflicts.
/// Never mutates production timetable — only returns advisory candidates.
/// </summary>
public sealed class GreedyOptimizationStrategy : IOptimizationStrategy
{
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public GreedyOptimizationStrategy(IOptimizationScoreCalculator scoreCalculator) =>
        _scoreCalculator = scoreCalculator;

    public string StrategyCode => "GREEDY";
    public string StrategyName => "Greedy Optimization";
    public OptimizationStrategyKind Kind => OptimizationStrategyKind.Greedy;
    public bool IsImplemented => true;

    public Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var working = OptimizationWorkingSet.CloneEntries(context.WorkingEntries);
        var candidates = new List<OptimizationCandidate>();
        var roomIds = context.Rooms.Keys.ToList();
        if (roomIds.Count == 0)
            roomIds = working.Select(e => e.RoomId).Distinct().ToList();

        var baselineConflicts = OptimizationWorkingSet.CountHardConflicts(working);
        var improved = true;
        var guard = 0;
        while (improved && guard++ < 40 && candidates.Count < 25)
        {
            improved = false;
            var conflicted = FindConflictedEntries(working).Take(8).ToList();
            foreach (var entry in conflicted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var best = TryBestRoomMove(working, entry, roomIds, baselineConflicts);
                if (best is null) continue;

                OptimizationWorkingSet.ApplyCandidate(working, best);
                candidates.Add(best);
                baselineConflicts = OptimizationWorkingSet.CountHardConflicts(working);
                improved = true;
                break;
            }
        }

        return Task.FromResult(BuildResult(context, working, candidates, started, baselineConflicts));
    }

    private OptimizationCandidate? TryBestRoomMove(
        List<OptimizationEntrySnapshot> working,
        OptimizationEntrySnapshot entry,
        IReadOnlyList<int> roomIds,
        int currentConflicts)
    {
        OptimizationCandidate? best = null;
        var bestConflicts = currentConflicts;
        foreach (var roomId in roomIds)
        {
            if (roomId == entry.RoomId) continue;
            var occupied = working.Any(e =>
                e.EntryId != entry.EntryId &&
                e.RoomId == roomId &&
                e.DayOfWeek == entry.DayOfWeek &&
                e.TimeSlotId == entry.TimeSlotId);
            if (occupied) continue;

            var trial = OptimizationWorkingSet.CloneEntries(working);
            var target = trial.First(e => e.EntryId == entry.EntryId);
            target.RoomId = roomId;
            var conflicts = OptimizationWorkingSet.CountHardConflicts(trial);
            if (conflicts >= bestConflicts) continue;

            bestConflicts = conflicts;
            best = new OptimizationCandidate
            {
                CandidateId = $"greedy-room-{entry.EntryId}-{roomId}",
                Description = $"Move entry {entry.EntryId} to room {roomId} to reduce conflicts.",
                ProposedChangeSummaries =
                [
                    $"Entry {entry.EntryId}: Room {entry.RoomId} → {roomId}",
                    $"Projected conflict count {conflicts} (was {currentConflicts})"
                ],
                ChangeType = "RoomReassign",
                EntryId = entry.EntryId,
                ProposedRoomId = roomId,
                StrategyCode = StrategyCode
            };
        }

        return best;
    }

    private static IEnumerable<OptimizationEntrySnapshot> FindConflictedEntries(List<OptimizationEntrySnapshot> working)
    {
        var staffClash = working.GroupBy(e => (e.StaffId, e.DayOfWeek, e.TimeSlotId))
            .Where(g => g.Count() > 1).SelectMany(g => g);
        var roomClash = working.GroupBy(e => (e.RoomId, e.DayOfWeek, e.TimeSlotId))
            .Where(g => g.Count() > 1).SelectMany(g => g);
        return staffClash.Concat(roomClash).DistinctBy(e => e.EntryId);
    }

    private OptimizationResult BuildResult(
        OptimizationContext context,
        List<OptimizationEntrySnapshot> working,
        List<OptimizationCandidate> candidates,
        DateTime started,
        int projectedConflicts)
    {
        var metrics = OptimizationWorkingSet.BuildMetrics(
            working, context.Rooms, context.TimeSlots, context.FacultyPreferredRoomIds, projectedConflicts);
        var projectedContext = OptimizationWorkingSet.WithWorkingState(context, working, metrics, projectedConflicts);
        var baselineSummary = _scoreCalculator.Calculate(context);
        var projectedSummary = _scoreCalculator.Calculate(projectedContext);
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
                ScoringTimeMs = 0,
                Outcome = "SandboxProposal"
            },
            Summary = new OptimizationSummary
            {
                CandidateCount = candidates.Count,
                BaselineScore = baselineSummary.Score.NormalizedScore,
                BestProjectedScore = projectedSummary.Score.NormalizedScore,
                ImprovementDelta = projectedSummary.Score.NormalizedScore - baselineSummary.Score.NormalizedScore,
                BaselineConflictCount = context.ConflictCount,
                ProjectedConflictCount = projectedConflicts,
                StatusMessage = candidates.Count == 0
                    ? "Greedy strategy found no improving room moves."
                    : $"Greedy strategy proposed {candidates.Count} conflict-reducing moves."
            },
            BaselineScore = baselineSummary.Score,
            ProjectedScore = projectedSummary.Score,
            Candidates = candidates
        };
    }
}
