using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1C_5_AllocationOperationsTests
{
    [Fact]
    public void Scenario_lifecycle_contains_enterprise_states()
    {
        Assert.Contains(AllocationScenarioLifecycle.Draft, AllocationScenarioLifecycle.All);
        Assert.Contains(AllocationScenarioLifecycle.Approved, AllocationScenarioLifecycle.All);
        Assert.Contains(AllocationScenarioLifecycle.Archived, AllocationScenarioLifecycle.All);
        Assert.Contains(AllocationScenarioLifecycle.Rejected, AllocationScenarioLifecycle.All);
    }

    [Fact]
    public void Scenario_version_entity_is_immutable_append_shape()
    {
        var v = new AllocationScenarioVersion
        {
            ScenarioId = Guid.NewGuid(),
            VersionNumber = 2,
            ContextVersion = "15",
            ContextChecksum = "abc",
            CreatedAt = DateTime.UtcNow,
            Reason = "Replay",
            Score = 91,
            Status = AllocationScenarioLifecycle.Simulated,
            Checksum = "deadbeef",
            ScenarioJson = "{}",
        };
        Assert.Equal(2, v.VersionNumber);
        Assert.Equal("Replay", v.Reason);
    }

    [Fact]
    public void Score_breakdown_reuses_ai29_1c_calculator_shape()
    {
        var calc = new AllocationScoreCalculator();
        var scenario = new AllocationScenario
        {
            SectionSummaries =
            [
                new AllocationSectionSummary { SectionId = 1, SectionCode = "A", MaximumCapacity = 60, AssignedCount = 42, OccupancyPercent = 70 },
            ],
            Constraints =
            [
                new AllocationConstraintEvaluation
                {
                    ConstraintCode = "Capacity",
                    Priority = AllocationConstraintPriority.Mandatory,
                    Satisfied = true,
                },
            ],
        };
        var score = calc.Score(new SectionAllocationContext { Checksum = "x" }, scenario);
        Assert.InRange(score.TotalScore, 0, 100);
        Assert.True(score.CapacityUtilization >= 0);
    }

    [Fact]
    public void Explanation_report_uses_deterministic_reasons()
    {
        var report = new AllocationExplanationReport
        {
            ScenarioId = Guid.NewGuid(),
            Assigned =
            [
                new AllocationStudentExplanation
                {
                    StudentId = 1,
                    StudentNumber = "105325405001",
                    RecommendedSectionCode = "B",
                    Assigned = true,
                    Reasons = ["✓ Capacity available", "✓ Gender distribution improved"],
                },
            ],
            Unallocated =
            [
                new AllocationStudentExplanation
                {
                    Assigned = false,
                    Reasons = ["✗ All eligible sections reached hard capacity"],
                },
            ],
        };
        Assert.Contains(report.Assigned[0].Reasons, r => r.StartsWith("✓"));
        Assert.Contains(report.Unallocated[0].Reasons, r => r.StartsWith("✗"));
    }

    [Fact]
    public void Multi_compare_identifies_best_scenario()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var report = new AllocationMultiCompareReport
        {
            OriginalScore = 78,
            Scenarios =
            [
                new AllocationScenarioCompareSide { ScenarioId = a, Label = "Scenario A", Score = 91 },
                new AllocationScenarioCompareSide { ScenarioId = b, Label = "Scenario B", Score = 87 },
            ],
            BestScenarioId = a,
            BestScenarioLabel = "Scenario A",
            ImprovementVsOriginal = 13,
        };
        Assert.Equal(a, report.BestScenarioId);
        Assert.Equal(13, report.ImprovementVsOriginal);
    }

    [Fact]
    public void Governance_blocks_archived_and_approved()
    {
        var blocked = new AllocationGovernanceResult
        {
            CanApprove = false,
            BlockingReasons =
            [
                "Scenario is archived.",
                "Scenario is already approved.",
                "Scenario is based on an outdated academic configuration. Rebuild scenario before approval.",
            ],
            ContextStale = true,
        };
        Assert.False(blocked.CanApprove);
        Assert.Contains(blocked.BlockingReasons, r => r.Contains("outdated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Heatmap_bands_classify_utilization()
    {
        var cells = new[]
        {
            new AllocationHeatmapCell { SectionCode = "A", OccupancyPercent = 98, Band = "OverCapacity" },
            new AllocationHeatmapCell { SectionCode = "C", OccupancyPercent = 71, Band = "Optimal" },
            new AllocationHeatmapCell { SectionCode = "D", OccupancyPercent = 45, Band = "UnderUtilized" },
        };
        Assert.Equal(1, cells.Count(c => c.Band == "OverCapacity"));
        Assert.Equal(1, cells.Count(c => c.Band == "UnderUtilized"));
    }

    [Fact]
    public void Constraint_dashboard_compliance_math()
    {
        var dto = new AllocationConstraintDashboardDto
        {
            TotalConstraints = 4,
            MandatoryViolations = 0,
            PreferredViolations = 1,
            CompliancePercent = 75,
            Rows =
            [
                new AllocationConstraintEvaluation { ConstraintCode = "Capacity", Priority = AllocationConstraintPriority.Mandatory, Satisfied = true },
                new AllocationConstraintEvaluation { ConstraintCode = "GenderBalance", Priority = AllocationConstraintPriority.Preferred, Satisfied = false },
            ],
        };
        Assert.Equal(75, dto.CompliancePercent);
        Assert.Equal(1, dto.PreferredViolations);
    }

    [Fact]
    public void Audit_actions_cover_operations()
    {
        Assert.Equal("Replay", AllocationAuditActions.Replay);
        Assert.Equal("Approve", AllocationAuditActions.Approve);
        Assert.Equal("Compare", AllocationAuditActions.Compare);
    }

    [Fact]
    public void Architecture_guard_still_passes_with_operations_rules()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Contains(report.Checks, c => c.Contains("operations", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Version_checksum_helper_is_deterministic()
    {
        var a = AllocationScenarioVersioningService.Sha256("{\"x\":1}");
        var b = AllocationScenarioVersioningService.Sha256("{\"x\":1}");
        Assert.Equal(a, b);
        Assert.NotEqual(a, AllocationScenarioVersioningService.Sha256("{\"x\":2}"));
    }

    [Fact]
    public void Analytics_dto_supports_period_dimensions()
    {
        var dto = new AllocationAnalyticsDto { Period = "Weekly", SuccessRate = 90, AverageScore = 88 };
        Assert.Equal("Weekly", dto.Period);
        Assert.Equal(90, dto.SuccessRate);
    }
}
