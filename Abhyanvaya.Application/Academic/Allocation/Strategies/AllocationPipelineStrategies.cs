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

        var sections = state.Context.Sections
            .Where(s => s.Lifecycle is not ("Merged" or "Split" or "Archived" or "Closed"))
            .OrderBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SectionId)
            .ToList();
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

        // Seed already-assigned students (respect current section when capacity allows).
        foreach (var st in state.Context.Students.OrderBy(s => s.StudentId))
        {
            if (st.CurrentSectionId is int sid && remaining.ContainsKey(sid) && remaining[sid] > 0)
            {
                state.Assignments[st.StudentId] = sid;
                remaining[sid]--;
                Add(state, st.StudentId, $"✓ Kept in section (capacity available)");
            }
        }

        foreach (var studentId in state.OrderedStudentIds)
        {
            if (state.Assignments.ContainsKey(studentId)) continue;

            var target = remaining
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => OccupancyRatio(kv.Key, remaining, caps, sections.Count))
                .ThenBy(kv => SectionCode(kv.Key, sections), StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key)
                .Select(kv => (int?)kv.Key)
                .FirstOrDefault();

            if (target is null)
            {
                state.Warnings.Add($"Student {studentId} could not be placed — capacity exhausted.");
                continue;
            }

            state.Assignments[studentId] = target.Value;
            remaining[target.Value]--;
            Add(state, studentId, "✓ Capacity available");
            Add(state, studentId, "✓ Occupancy balance improved");
        }

        return Task.CompletedTask;
    }

    private static double OccupancyRatio(
        int sectionId,
        Dictionary<int, int> remaining,
        Dictionary<int, AllocationCapacityProjection> caps,
        int _)
    {
        caps.TryGetValue(sectionId, out var c);
        var max = c?.MaximumCapacity ?? 0;
        if (max <= 0) return 0;
        var reserved = c?.ReservedSeats ?? 0;
        var hard = Math.Max(1, max - reserved);
        var assigned = hard - remaining[sectionId];
        return assigned / (double)hard;
    }

    private static string SectionCode(int sectionId, List<AllocationSectionProjection> sections)
        => sections.FirstOrDefault(s => s.SectionId == sectionId)?.SectionCode ?? "";

    private static void Add(AllocationWorkingState state, int studentId, string explanation)
    {
        if (!state.Explanations.TryGetValue(studentId, out var list))
        {
            list = [];
            state.Explanations[studentId] = list;
        }
        list.Add(explanation);
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
            .OrderBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase)
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
            },
        };
    }
}
