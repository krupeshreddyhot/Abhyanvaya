using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Strategies;

/// <summary>
/// Improves capacity fit, building locality, and room utilization via room reassignment proposals.
/// </summary>
public sealed class RoomOptimizationStrategy : IOptimizationStrategy
{
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public RoomOptimizationStrategy(IOptimizationScoreCalculator scoreCalculator) =>
        _scoreCalculator = scoreCalculator;

    public string StrategyCode => "ROOM";
    public string StrategyName => "Room Optimization";
    public OptimizationStrategyKind Kind => OptimizationStrategyKind.RoomOptimization;
    public bool IsImplemented => true;

    public Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var working = OptimizationWorkingSet.CloneEntries(context.WorkingEntries);
        var candidates = new List<OptimizationCandidate>();
        var rooms = context.Rooms.Values.OrderBy(r => r.Capacity).ToList();
        if (rooms.Count == 0)
            return Task.FromResult(BuildResult(context, working, candidates, started));

        var load = working.GroupBy(e => e.RoomId).ToDictionary(g => g.Key, g => g.Count());

        foreach (var entry in working.OrderByDescending(e => load.GetValueOrDefault(e.RoomId)).Take(30))
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Rooms.TryGetValue(entry.RoomId, out var currentRoom);
            var preferredBuilding = currentRoom?.BuildingId;

            var better = rooms
                .Where(r => r.RoomId != entry.RoomId)
                .Where(r => !working.Any(e =>
                    e.EntryId != entry.EntryId &&
                    e.RoomId == r.RoomId &&
                    e.DayOfWeek == entry.DayOfWeek &&
                    e.TimeSlotId == entry.TimeSlotId))
                .Select(r => new
                {
                    Room = r,
                    Score =
                        (preferredBuilding.HasValue && r.BuildingId == preferredBuilding ? 30 : 0) +
                        (r.Capacity >= 20 ? 10 : 0) -
                        load.GetValueOrDefault(r.RoomId) * 2 +
                        (currentRoom is not null && r.Capacity < currentRoom.Capacity && r.Capacity >= 10 ? 15 : 0)
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (better is null || better.Score < 10) continue;

            var candidate = new OptimizationCandidate
            {
                CandidateId = $"room-opt-{entry.EntryId}-{better.Room.RoomId}",
                Description = $"Improve room fit for entry {entry.EntryId} → room {better.Room.Name}.",
                ProposedChangeSummaries =
                [
                    $"Entry {entry.EntryId}: Room {entry.RoomId} → {better.Room.RoomId}",
                    "Targets capacity utilization / building locality / travel reduction."
                ],
                ChangeType = "RoomOptimize",
                EntryId = entry.EntryId,
                ProposedRoomId = better.Room.RoomId,
                StrategyCode = StrategyCode
            };

            OptimizationWorkingSet.ApplyCandidate(working, candidate);
            load = working.GroupBy(e => e.RoomId).ToDictionary(g => g.Key, g => g.Count());
            candidates.Add(candidate);
            if (candidates.Count >= 15) break;
        }

        return Task.FromResult(BuildResult(context, working, candidates, started));
    }

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
                StatusMessage = $"Room strategy proposed {candidates.Count} utilization moves."
            },
            BaselineScore = baseline.Score,
            ProjectedScore = projected.Score,
            Candidates = candidates
        };
    }
}
