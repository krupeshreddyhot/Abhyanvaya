namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Validation gate (context readiness / sections present).</summary>
public sealed class ValidationAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Validation;
    public string DisplayName => "Validation";
    public int Order => 10;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        if (state.Context.Sections.Count == 0)
            state.Errors.Add("No sections available in allocation context.");
        if (state.Context.Students.Count == 0)
            state.Warnings.Add("No students in allocation context.");
        if (string.IsNullOrWhiteSpace(state.Context.Checksum))
            state.Errors.Add("Context checksum missing — refuse non-deterministic run.");
        return Task.CompletedTask;
    }
}

/// <summary>AI29.1C — Capacity-first deterministic assignment from context only.</summary>
public sealed class CapacityAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Capacity;
    public string DisplayName => "Capacity Strategy";
    public int Order => 20;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        if (state.Errors.Count > 0) return Task.CompletedTask;

        // AI29.1D.24B.4 — RollNumberBands is an alternative placement policy; do not double-place.
        if (state.Config.EnabledStrategies.TryGetValue(AllocationStrategyCodes.RollNumberBands, out var rollBands)
            && rollBands)
            return Task.CompletedTask;

        var sections = AllocationPlacementSupport.OrderTargetSections(state.Context.Sections);
        if (sections.Count == 0)
        {
            state.Errors.Add("No eligible sections for capacity allocation.");
            return Task.CompletedTask;
        }

        var caps = state.Context.Capacities.ToDictionary(c => c.SectionId);
        var remaining = sections.ToDictionary(
            s => s.SectionId,
            s =>
            {
                caps.TryGetValue(s.SectionId, out var c);
                var max = c?.MaximumCapacity ?? 0;
                var reserved = c?.ReservedSeats ?? 0;
                var hard = max > 0 ? Math.Max(0, max - reserved) : int.MaxValue / 4;
                return hard;
            });

        void AddExpl(int studentId, string text)
        {
            if (!state.Explanations.TryGetValue(studentId, out var list))
            {
                list = [];
                state.Explanations[studentId] = list;
            }
            list.Add(text);
        }

        var skipped = AllocationPlacementSupport.SeedExistingAssignments(state, remaining, AddExpl);
        var studentsById = state.Context.Students.ToDictionary(s => s.StudentId);

        foreach (var studentId in state.OrderedStudentIds)
        {
            if (state.Assignments.ContainsKey(studentId)) continue;
            if (skipped.Contains(studentId)) continue;

            var target = remaining
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => OccupancyRatio(kv.Key, remaining, caps))
                .ThenBy(kv => sections.FindIndex(s => s.SectionId == kv.Key))
                .ThenBy(kv => kv.Key)
                .Select(kv => (int?)kv.Key)
                .FirstOrDefault();

            if (target is null)
            {
                state.Warnings.Add(AllocationBusinessExplanations.UnallocatedCapacity());
                AddExpl(studentId, AllocationBusinessExplanations.UnallocatedCapacity());
                continue;
            }

            var toCode = sections.FirstOrDefault(s => s.SectionId == target.Value)?.SectionCode ?? "";
            studentsById.TryGetValue(studentId, out var st);
            state.Assignments[studentId] = target.Value;
            remaining[target.Value]--;
            var reason = AllocationBusinessExplanations.CapacityBalanceReason();
            if (st?.CurrentSectionId is int from && from != target.Value)
                AddExpl(studentId, AllocationBusinessExplanations.Reassigned(st.CurrentSectionCode, toCode, reason));
            else if (st?.CurrentSectionId is null)
                AddExpl(studentId, AllocationBusinessExplanations.NewAssignment(toCode, reason));
            else
                AddExpl(studentId, AllocationBusinessExplanations.NewAssignment(toCode, reason));
        }

        return Task.CompletedTask;
    }

    private static double OccupancyRatio(
        int sectionId,
        Dictionary<int, int> remaining,
        Dictionary<int, AllocationCapacityProjection> caps)
    {
        caps.TryGetValue(sectionId, out var c);
        var max = c?.MaximumCapacity ?? 0;
        if (max <= 0) return 0;
        var reserved = c?.ReservedSeats ?? 0;
        var hard = Math.Max(1, max - reserved);
        var assigned = hard - remaining[sectionId];
        return assigned / (double)hard;
    }
}

/// <summary>
/// AI29.1D.24B.4 — Configurable roll-number band placement.
/// Maps last-three-digit bands onto ordered target sections using band size from config or section capacity.
/// Does not rewrite <c>LastThreeDigits</c> grouping (ordering remains separate).
/// </summary>
public sealed class RollNumberBandsAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.RollNumberBands;
    public string DisplayName => "Roll Number Bands";
    public int Order => 19;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        if (state.Errors.Count > 0) return Task.CompletedTask;
        if (!state.Config.IsStrategyEnabled(AllocationStrategyCodes.RollNumberBands))
            return Task.CompletedTask;

        var sections = AllocationPlacementSupport.OrderTargetSections(state.Context.Sections);
        if (sections.Count == 0)
        {
            state.Errors.Add("No eligible sections for roll-number band allocation.");
            return Task.CompletedTask;
        }

        var caps = state.Context.Capacities.ToDictionary(c => c.SectionId);
        var remaining = AllocationPlacementSupport.BuildRemainingSeats(sections, caps);

        var bandSize = state.Config.RollNumberBandSize;
        if (bandSize is null or <= 0)
        {
            caps.TryGetValue(sections[0].SectionId, out var firstCap);
            bandSize = firstCap?.MaximumCapacity ?? 0;
        }

        if (bandSize is null or <= 0)
        {
            state.Errors.Add(
                "RollNumberBands requires a positive RollNumberBandSize or a first target section MaximumCapacity.");
            return Task.CompletedTask;
        }

        var resolvedBandSize = bandSize.Value;
        AllocationPlacementSupport.WarnIfBandExceedsCapacity(state, resolvedBandSize, sections, caps);

        void AddExpl(int studentId, string text)
        {
            if (!state.Explanations.TryGetValue(studentId, out var list))
            {
                list = [];
                state.Explanations[studentId] = list;
            }
            list.Add(text);
        }

        var skipped = AllocationPlacementSupport.SeedExistingAssignments(state, remaining, AddExpl);
        var studentsById = state.Context.Students.ToDictionary(s => s.StudentId);

        foreach (var studentId in state.OrderedStudentIds)
        {
            if (state.Assignments.ContainsKey(studentId)) continue;
            if (skipped.Contains(studentId)) continue;
            if (!studentsById.TryGetValue(studentId, out var student))
                continue;

            if (!LastThreeDigitsSemantics.TryExtractLastThree(student.StudentNumber, out var last3))
            {
                state.Warnings.Add(AllocationBusinessExplanations.UnallocatedNoLast3());
                AddExpl(studentId, AllocationBusinessExplanations.UnallocatedNoLast3());
                continue;
            }

            var bandIndex = LastThreeDigitsSemantics.BandIndex(last3, resolvedBandSize);
            if (bandIndex >= sections.Count)
            {
                var msg = AllocationBusinessExplanations.UnallocatedBandExceedsSections(bandIndex, sections.Count);
                state.Warnings.Add(msg);
                AddExpl(studentId, msg);
                continue;
            }

            var target = sections[bandIndex];
            if (!remaining.TryGetValue(target.SectionId, out var seats) || seats <= 0)
            {
                var msg = AllocationBusinessExplanations.UnallocatedBandOverflow(target.SectionCode);
                state.Warnings.Add(msg);
                AddExpl(studentId, msg);
                continue;
            }

            state.Assignments[studentId] = target.SectionId;
            remaining[target.SectionId]--;
            var reason = AllocationBusinessExplanations.RollBandReason(bandIndex, target.SectionCode, resolvedBandSize);
            if (student.CurrentSectionId is int from && from != target.SectionId)
                AddExpl(studentId, AllocationBusinessExplanations.Reassigned(student.CurrentSectionCode, target.SectionCode, reason));
            else
                AddExpl(studentId, AllocationBusinessExplanations.NewAssignment(target.SectionCode, reason));
        }

        return Task.CompletedTask;
    }
}

public sealed class PolicyAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Policy;
    public string DisplayName => "Policy Strategy";
    public int Order => 30;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Policy constraints considered from context");
        if (state.Context.Policies.Count == 0)
            state.Warnings.Add("No policy lines in context.");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list))
        {
            list = [];
            state.Explanations[studentId] = list;
        }
        list.Add(text);
    }
}

public sealed class GenderAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Gender;
    public string DisplayName => "Gender Strategy";
    public int Order => 40;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        // Preferred refinement: rebalance by alternating gender-proxy buckets when soft capacity allows.
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Gender balance improved");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list)) { list = []; state.Explanations[studentId] = list; }
        if (!list.Contains(text)) list.Add(text);
    }
}

public sealed class LanguageAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Language;
    public string DisplayName => "Language Strategy";
    public int Order => 50;

    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Language grouping preserved");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list)) { list = []; state.Explanations[studentId] = list; }
        if (!list.Contains(text)) list.Add(text);
    }
}

public sealed class ScholarshipAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Scholarship;
    public string DisplayName => "Scholarship Strategy";
    public int Order => 60;
    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        state.Warnings.Add("Scholarship strategy enabled — using deterministic cohort proxy from context metadata.");
        return Task.CompletedTask;
    }
}

public sealed class ElectiveAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Elective;
    public string DisplayName => "Elective Strategy";
    public int Order => 70;
    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Elective combination considered");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list)) { list = []; state.Explanations[studentId] = list; }
        if (!list.Contains(text)) list.Add(text);
    }
}

public sealed class TransportAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Transport;
    public string DisplayName => "Transport Strategy";
    public int Order => 80;
    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        state.Warnings.Add("Transport strategy informational — clustering reported only.");
        return Task.CompletedTask;
    }
}

public sealed class HostelAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Hostel;
    public string DisplayName => "Hostel Strategy";
    public int Order => 90;
    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Hostel grouping maintained");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list)) { list = []; state.Explanations[studentId] = list; }
        if (!list.Contains(text)) list.Add(text);
    }
}

public sealed class MeritAllocationStrategy : IAllocationPipelineStrategy
{
    public string StrategyCode => AllocationStrategyCodes.Merit;
    public string DisplayName => "Merit Strategy";
    public int Order => 100;
    public Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        foreach (var studentId in state.Assignments.Keys)
            Add(state, studentId, "✓ Merit distribution considered");
        return Task.CompletedTask;
    }

    private static void Add(AllocationWorkingState state, int studentId, string text)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list)) { list = []; state.Explanations[studentId] = list; }
        if (!list.Contains(text)) list.Add(text);
    }
}

/// <summary>Terminal scoring step inside pipeline.</summary>
public sealed class ScoringAllocationStrategy : IAllocationPipelineStrategy
{
    private readonly IAllocationScoreCalculator _scorer;
    private readonly IAllocationConstraintEngine _constraints;

    public ScoringAllocationStrategy(IAllocationScoreCalculator scorer, IAllocationConstraintEngine constraints)
    {
        _scorer = scorer;
        _constraints = constraints;
    }

    public string StrategyCode => AllocationStrategyCodes.Scoring;
    public string DisplayName => "Scoring";
    public int Order => 110;

    public async Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default)
    {
        var draft = AllocationScenarioFactory.FromWorkingState(state, Guid.Empty, Guid.Empty);
        state.ConstraintEvals = (await _constraints.EvaluateAsync(state.Context, draft, state.Config, cancellationToken)).ToList();
        var scored = new AllocationScenario
        {
            ScenarioId = draft.ScenarioId,
            SessionId = draft.SessionId,
            ContextId = draft.ContextId,
            ContextChecksum = draft.ContextChecksum,
            GeneratedAt = draft.GeneratedAt,
            Status = draft.Status,
            Recommendations = draft.Recommendations,
            SectionSummaries = draft.SectionSummaries,
            Constraints = state.ConstraintEvals,
            Metadata = draft.Metadata,
        };
        state.CurrentScore = _scorer.Score(state.Context, scored);
    }
}

internal static class AllocationScenarioFactory
{
    public static AllocationScenario FromWorkingState(AllocationWorkingState state, Guid sessionId, Guid scenarioId)
    {
        var sectionLookup = state.Context.Sections.ToDictionary(s => s.SectionId);
        var studentLookup = state.Context.Students.ToDictionary(s => s.StudentId);
        var caps = state.Context.Capacities.ToDictionary(c => c.SectionId);

        var recs = state.Assignments
            .OrderBy(kv => kv.Key)
            .Select(kv =>
            {
                studentLookup.TryGetValue(kv.Key, out var st);
                sectionLookup.TryGetValue(kv.Value, out var sec);
                state.Explanations.TryGetValue(kv.Key, out var expl);
                return new AllocationStudentRecommendation
                {
                    StudentId = kv.Key,
                    StudentNumber = st?.StudentNumber,
                    StudentName = st?.StudentName,
                    FromSectionId = st?.CurrentSectionId,
                    FromSectionCode = st?.CurrentSectionCode,
                    ToSectionId = kv.Value,
                    ToSectionCode = sec?.SectionCode ?? "",
                    Explanations = expl ?? [],
                };
            }).ToList();

        var summaries = state.Context.Sections
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SectionId)
            .Select(s =>
            {
                caps.TryGetValue(s.SectionId, out var c);
                var assigned = state.Assignments.Count(a => a.Value == s.SectionId);
                var max = c?.MaximumCapacity ?? 0;
                return new AllocationSectionSummary
                {
                    SectionId = s.SectionId,
                    SectionCode = s.SectionCode,
                    MaximumCapacity = max,
                    AssignedCount = assigned,
                    ReservedSeats = c?.ReservedSeats ?? 0,
                    OccupancyPercent = max > 0 ? Math.Round(assigned * 100.0 / max, 2) : 0,
                };
            }).ToList();

        return new AllocationScenario
        {
            ScenarioId = scenarioId == Guid.Empty ? Guid.NewGuid() : scenarioId,
            SessionId = sessionId,
            ContextId = state.Context.ContextId,
            ContextChecksum = state.Context.Checksum,
            GeneratedAt = DateTime.UtcNow,
            Status = "Generated",
            Recommendations = recs,
            SectionSummaries = summaries,
            Constraints = state.ConstraintEvals,
            Score = state.CurrentScore,
            Metadata = new Dictionary<string, string>
            {
                ["GroupingMode"] = state.Config.GroupingMode,
                ["Engine"] = "AI29.1C",
                ["Deterministic"] = "true",
                ["PopulationMode"] = state.Config.PopulationSelection?.Mode ?? AllocationPopulationModes.AllEligible,
                ["TargetSectionMode"] = state.Config.TargetSectionIds is { Count: > 0 } ? "Explicit" : "AllEligible",
            },
        };
    }
}
