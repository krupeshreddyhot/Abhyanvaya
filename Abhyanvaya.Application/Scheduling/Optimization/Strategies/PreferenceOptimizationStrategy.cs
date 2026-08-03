using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Strategies;

/// <summary>
/// Aligns assignments with faculty preferred rooms / locality preferences.
/// Generates sandbox scenario candidates only.
/// </summary>
public sealed class PreferenceOptimizationStrategy : IOptimizationStrategy
{
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public PreferenceOptimizationStrategy(IOptimizationScoreCalculator scoreCalculator) =>
        _scoreCalculator = scoreCalculator;

    public string StrategyCode => "PREFERENCE";
    public string StrategyName => "Preference Optimization";
    public OptimizationStrategyKind Kind => OptimizationStrategyKind.PreferenceOptimization;
    public bool IsImplemented => true;

    public Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var working = OptimizationWorkingSet.CloneEntries(context.WorkingEntries);
        var candidates = new List<OptimizationCandidate>();

        foreach (var entry in working)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.FacultyPreferredRoomIds.TryGetValue(entry.StaffId, out var preferred))
                continue;
            if (entry.RoomId == preferred) continue;

            var blocked = working.Any(e =>
                e.EntryId != entry.EntryId &&
                e.RoomId == preferred &&
                e.DayOfWeek == entry.DayOfWeek &&
                e.TimeSlotId == entry.TimeSlotId);
            if (blocked) continue;

            var candidate = new OptimizationCandidate
            {
                CandidateId = $"pref-room-{entry.EntryId}-{preferred}",
                Description = $"Honor faculty {entry.StaffId} preferred room {preferred} for entry {entry.EntryId}.",
                ProposedChangeSummaries =
                [
                    $"Entry {entry.EntryId}: Room {entry.RoomId} → {preferred}",
                    "Improves preference satisfaction / delivery locality."
                ],
                ChangeType = "PreferenceRoom",
                EntryId = entry.EntryId,
                ProposedRoomId = preferred,
                StrategyCode = StrategyCode
            };

            OptimizationWorkingSet.ApplyCandidate(working, candidate);
            candidates.Add(candidate);
            if (candidates.Count >= 20) break;
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
                StatusMessage = $"Preference strategy proposed {candidates.Count} preference-aligned moves."
            },
            BaselineScore = baseline.Score,
            ProjectedScore = projected.Score,
            Candidates = candidates
        };
    }
}
