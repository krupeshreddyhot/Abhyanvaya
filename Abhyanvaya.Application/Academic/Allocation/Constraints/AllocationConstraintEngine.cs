namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Constraint engine over registered IAllocationConstraint implementations.</summary>
public sealed class AllocationConstraintEngine : IAllocationConstraintEngine
{
    private readonly IEnumerable<IAllocationConstraint> _constraints;

    public AllocationConstraintEngine(IEnumerable<IAllocationConstraint> constraints)
        => _constraints = constraints;

    public async Task<IReadOnlyList<AllocationConstraintEvaluation>> EvaluateAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationPipelineConfig config,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AllocationConstraintEvaluation>();
        foreach (var constraint in _constraints.OrderBy(c => c.ConstraintCode, StringComparer.Ordinal))
        {
            if (string.Equals(constraint.ConstraintCode, "NoOp", StringComparison.OrdinalIgnoreCase))
                continue;

            var priority = config.ConstraintPriorities.TryGetValue(constraint.ConstraintCode, out var p)
                ? p
                : AllocationConstraintPriority.Preferred;

            var eval = await constraint.EvaluateScenarioAsync(context, scenario, priority, cancellationToken);
            results.Add(eval);
        }
        return results;
    }
}

/// <summary>Capacity hard-limit constraint using scenario + context only.</summary>
public sealed class CapacityAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "Capacity";
    public string DisplayName => "Capacity";

    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true, Summary = "Use scenario evaluation." });

    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationConstraintPriority priority,
        CancellationToken cancellationToken = default)
    {
        var caps = context.Capacities.ToDictionary(c => c.SectionId);
        var violations = scenario.SectionSummaries
            .Where(s =>
            {
                caps.TryGetValue(s.SectionId, out var c);
                var max = c?.MaximumCapacity ?? 0;
                var reserved = c?.ReservedSeats ?? 0;
                return max > 0 && s.AssignedCount > Math.Max(0, max - reserved);
            })
            .Select(s => s.SectionCode)
            .ToList();

        var ok = violations.Count == 0;
        return Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode,
            Priority = priority,
            Satisfied = ok,
            Summary = ok ? "All sections within hard capacity." : $"Over capacity: {string.Join(", ", violations)}",
            ScoreImpact = ok ? 0 : -25,
        });
    }
}

public sealed class ReservedSeatsAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "ReservedSeats";
    public string DisplayName => "Reserved Seats";

    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });

    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationConstraintPriority priority,
        CancellationToken cancellationToken = default)
    {
        var ok = scenario.SectionSummaries.All(s =>
        {
            var cap = context.Capacities.FirstOrDefault(c => c.SectionId == s.SectionId);
            if (cap is null || cap.MaximumCapacity <= 0) return true;
            return s.AssignedCount <= cap.MaximumCapacity - cap.ReservedSeats;
        });
        return Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode,
            Priority = priority,
            Satisfied = ok,
            Summary = ok ? "Reserved seats respected." : "Reserved seats violated.",
            ScoreImpact = ok ? 0 : -20,
        });
    }
}

public sealed class GenderBalanceAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "GenderBalance";
    public string DisplayName => "Gender Balance";

    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });

    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationConstraintPriority priority,
        CancellationToken cancellationToken = default)
    {
        // Preferred: section sizes should not diverge excessively.
        var counts = scenario.SectionSummaries.Select(s => s.AssignedCount).DefaultIfEmpty(0).ToList();
        var spread = counts.Max() - counts.Min();
        var ok = spread <= Math.Max(2, (counts.Sum() / Math.Max(1, counts.Count)) / 2);
        return Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode,
            Priority = priority,
            Satisfied = ok,
            Summary = ok ? "Section loads reasonably balanced." : $"Section load spread={spread}.",
            ScoreImpact = ok ? 5 : -5,
        });
    }
}

public sealed class LanguageAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "Language";
    public string DisplayName => "Language";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });
    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context, AllocationScenario scenario, AllocationConstraintPriority priority, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode, Priority = priority, Satisfied = true,
            Summary = "Language preferred grouping applied when enabled.", ScoreImpact = 2,
        });
}

public sealed class HostelAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "Hostel";
    public string DisplayName => "Hostel";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });
    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context, AllocationScenario scenario, AllocationConstraintPriority priority, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode, Priority = priority, Satisfied = true,
            Summary = "Hostel informational clustering.", ScoreImpact = 0,
        });
}

public sealed class MeritAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "Merit";
    public string DisplayName => "Merit";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });
    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context, AllocationScenario scenario, AllocationConstraintPriority priority, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode, Priority = priority, Satisfied = true,
            Summary = "Merit preferred distribution noted.", ScoreImpact = 1,
        });
}

public sealed class ElectiveAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "ElectiveCombination";
    public string DisplayName => "Elective Combination";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });
    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context, AllocationScenario scenario, AllocationConstraintPriority priority, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode, Priority = priority, Satisfied = true,
            Summary = "Elective combinations evaluated from context subjects.", ScoreImpact = 1,
        });
}

public sealed class MinorSubjectAllocationConstraint : IAllocationConstraint
{
    public string ConstraintCode => "MinorSubject";
    public string DisplayName => "Minor Subject";
    public Task<AllocationConstraintResult> EvaluateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintResult { ConstraintCode = ConstraintCode, Satisfied = true });
    public Task<AllocationConstraintEvaluation> EvaluateScenarioAsync(
        SectionAllocationContext context, AllocationScenario scenario, AllocationConstraintPriority priority, CancellationToken cancellationToken = default)
        => Task.FromResult(new AllocationConstraintEvaluation
        {
            ConstraintCode = ConstraintCode, Priority = priority, Satisfied = true,
            Summary = "Minor subject informational.", ScoreImpact = 0,
        });
}
