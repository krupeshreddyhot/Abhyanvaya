namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Deterministic multi-dimension score calculator.</summary>
public sealed class AllocationScoreCalculator : IAllocationScoreCalculator, IAllocationScoringProvider
{
    public string ProviderCode => "AI29.1C";

    public Task<AllocationScoreResult> ScoreAsync(SectionAllocationContext context, CancellationToken cancellationToken = default)
    {
        var empty = new AllocationScenario
        {
            SectionSummaries = context.Sections.Select(s =>
            {
                var c = context.Capacities.FirstOrDefault(x => x.SectionId == s.SectionId);
                return new AllocationSectionSummary
                {
                    SectionId = s.SectionId,
                    SectionCode = s.SectionCode,
                    MaximumCapacity = c?.MaximumCapacity ?? 0,
                    AssignedCount = c?.CurrentStrength ?? 0,
                    ReservedSeats = c?.ReservedSeats ?? 0,
                    OccupancyPercent = c?.OccupancyPercent ?? 0,
                };
            }).ToList(),
        };
        var b = Score(context, empty);
        return Task.FromResult(new AllocationScoreResult { ProviderCode = ProviderCode, Score = b.TotalScore, Summary = b.Summary });
    }

    public AllocationScoreBreakdown Score(SectionAllocationContext context, AllocationScenario scenario)
    {
        var summaries = scenario.SectionSummaries;
        var avgOcc = summaries.Count == 0 ? 0 : summaries.Average(s => s.OccupancyPercent);
        var capacity = Clamp(100 - Math.Abs(70 - avgOcc)); // prefer ~70% utilization

        var counts = summaries.Select(s => s.AssignedCount).DefaultIfEmpty(0).ToList();
        var spread = counts.Count == 0 ? 0 : counts.Max() - counts.Min();
        var gender = Clamp(100 - spread * 5);

        var mandatoryFail = scenario.Constraints.Any(c =>
            c.Priority == AllocationConstraintPriority.Mandatory && !c.Satisfied);
        var policy = mandatoryFail ? 0 : 90 + scenario.Constraints.Count(c => c.Satisfied) * 1.0;
        policy = Clamp(policy);

        var preferredPenalty = scenario.Constraints
            .Where(c => c.Priority == AllocationConstraintPriority.Preferred && !c.Satisfied)
            .Sum(c => Math.Abs(c.ScoreImpact));

        var merit = 80.0;
        var language = scenario.Constraints.Any(c => c.ConstraintCode == "Language" && c.Satisfied) ? 85 : 70;
        var hostel = scenario.Constraints.Any(c => c.ConstraintCode == "Hostel") ? 75 : 70;
        var elective = scenario.Constraints.Any(c => c.ConstraintCode == "ElectiveCombination" && c.Satisfied) ? 80 : 70;
        var transport = 70.0;

        var total = Clamp(
            capacity * 0.30
            + policy * 0.20
            + gender * 0.15
            + merit * 0.10
            + language * 0.10
            + hostel * 0.05
            + elective * 0.05
            + transport * 0.05
            - preferredPenalty);

        return new AllocationScoreBreakdown
        {
            TotalScore = Math.Round(total, 2),
            CapacityUtilization = Math.Round(capacity, 2),
            PolicyCompliance = Math.Round(policy, 2),
            GenderBalance = Math.Round(gender, 2),
            MeritDistribution = merit,
            LanguageDistribution = language,
            HostelDistribution = hostel,
            ElectiveBalance = elective,
            TransportBalance = transport,
            Summary = mandatoryFail
                ? "Mandatory constraints violated."
                : $"Score {total:0.##} (avg occupancy {avgOcc:0.##}%).",
        };
    }

    private static double Clamp(double v) => Math.Max(0, Math.Min(100, v));
}
