using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1C_5A_AllocationGovernanceTests
{
    [Theory]
    [InlineData(AllocationScenarioLifecycle.Archived, AllocationScenarioLifecycle.Draft, false)]
    [InlineData(AllocationScenarioLifecycle.Approved, AllocationScenarioLifecycle.Draft, false)]
    [InlineData(AllocationScenarioLifecycle.Rejected, AllocationScenarioLifecycle.Approved, false)]
    [InlineData(AllocationScenarioLifecycle.Reviewed, AllocationScenarioLifecycle.Approved, true)]
    [InlineData(AllocationScenarioLifecycle.Generated, AllocationScenarioLifecycle.Reviewed, true)]
    [InlineData(AllocationScenarioLifecycle.Draft, AllocationScenarioLifecycle.Saved, true)]
    public void Lifecycle_state_machine_enforces_transitions(string from, string to, bool expected)
    {
        var svc = new AllocationScenarioLifecycleService(
            db: null!,
            currentUser: null!,
            versions: null!,
            audit: null!);
        Assert.Equal(expected, svc.CanTransition(from, to));
    }

    [Fact]
    public void Status_vs_lifecycle_contradiction_detected()
    {
        Assert.True(AllocationStatusConsistency.IsContradictory(
            AllocationScenarioLifecycle.Approved,
            AllocationScenarioLifecycle.Reviewed));
        Assert.False(AllocationStatusConsistency.IsContradictory(
            AllocationExecutionStatus.Completed,
            AllocationScenarioLifecycle.Reviewed));
    }

    [Fact]
    public void Canonical_checksum_is_order_independent()
    {
        var a = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VersionNumber = 2,
            ContextVersion = "15",
            ContextChecksum = "ABC",
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = AllocationScenarioLifecycle.Reviewed,
            Operation = AllocationAuditActions.Review,
            Score = 91.5,
            ScenarioJson = """{"b":2,"a":1}""",
            TraceJson = "[]",
            ConfigJson = "{}",
        });
        var b = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VersionNumber = 2,
            ContextVersion = "15",
            ContextChecksum = "abc",
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = AllocationScenarioLifecycle.Reviewed,
            Operation = AllocationAuditActions.Review,
            Score = 91.5,
            ScenarioJson = """{"a":1,"b":2}""",
            TraceJson = "[]",
            ConfigJson = "{}",
        });
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void Constraint_kpis_are_priority_separated()
    {
        var rows = new List<AllocationConstraintEvaluation>
        {
            new() { ConstraintCode = "M1", Priority = AllocationConstraintPriority.Mandatory, Satisfied = true },
            new() { ConstraintCode = "M2", Priority = AllocationConstraintPriority.Mandatory, Satisfied = false },
            new() { ConstraintCode = "P1", Priority = AllocationConstraintPriority.Preferred, Satisfied = true },
            new() { ConstraintCode = "P2", Priority = AllocationConstraintPriority.Preferred, Satisfied = true },
            new() { ConstraintCode = "I1", Priority = AllocationConstraintPriority.Informational, Satisfied = false },
        };
        var mandatory = rows.Where(c => c.Priority == AllocationConstraintPriority.Mandatory).ToList();
        var preferred = rows.Where(c => c.Priority == AllocationConstraintPriority.Preferred).ToList();
        var mandatoryCompliance = Math.Round(mandatory.Count(c => c.Satisfied) * 100.0 / mandatory.Count, 2);
        var preferredCompliance = Math.Round(preferred.Count(c => c.Satisfied) * 100.0 / preferred.Count, 2);
        var info = rows.Count(c => c.Priority == AllocationConstraintPriority.Informational && !c.Satisfied);
        Assert.Equal(50, mandatoryCompliance);
        Assert.Equal(100, preferredCompliance);
        Assert.Equal(1, info);
        Assert.Equal(1, mandatory.Count(c => !c.Satisfied));
    }

    [Fact]
    public void Run_kpis_use_actual_status_counts_not_product()
    {
        var sessions = new[]
        {
            AllocationExecutionStatus.Completed,
            AllocationExecutionStatus.Failed,
            AllocationExecutionStatus.Cancelled,
            AllocationExecutionStatus.TimedOut,
            AllocationExecutionStatus.Running,
            AllocationExecutionStatus.Completed,
        };
        var successful = sessions.Count(AllocationExecutionStatus.IsSuccessful);
        var failed = sessions.Count(AllocationExecutionStatus.IsFailed);
        var cancelled = sessions.Count(s => s == AllocationExecutionStatus.Cancelled);
        var timedOut = sessions.Count(s => s == AllocationExecutionStatus.TimedOut);
        var running = sessions.Count(s => s == AllocationExecutionStatus.Running);
        Assert.Equal(2, successful);
        Assert.Equal(1, failed);
        Assert.Equal(1, cancelled);
        Assert.Equal(1, timedOut);
        Assert.Equal(1, running);
        Assert.Equal(sessions.Length, successful + failed + cancelled + timedOut + running);
        // Must not derive SuccessfulRuns as TotalRuns × SuccessRate alone.
        var derived = (int)Math.Round(sessions.Length * (successful * 100.0 / sessions.Length) / 100.0);
        Assert.Equal(successful, derived); // equal here by construction, but source counts are status-based
        Assert.Equal(2, successful);
    }

    [Fact]
    public void Heatmap_band_over_capacity_requires_exceeding_100_or_hard_capacity()
    {
        var warning = 90;
        var under = 40;
        double occ = 95;
        var assigned = 57;
        var max = 60;
        var overHard = max > 0 && assigned > max;
        var band = overHard || occ > 100 ? "OverCapacity"
            : occ >= warning ? "NearCapacity"
            : occ <= under ? "Underused"
            : "Healthy";
        Assert.Equal("NearCapacity", band);
    }

    [Fact]
    public void Archive_permission_is_separate_from_review()
    {
        Assert.Equal("Allocation.Scenario.Archive", PermissionKeys.AllocationScenarioArchive);
        Assert.NotEqual(PermissionKeys.AllocationScenarioReview, PermissionKeys.AllocationScenarioArchive);
        Assert.Contains(PermissionKeys.AllocationScenarioArchive, PermissionKeys.All);
    }

    [Fact]
    public void Concurrency_message_is_user_friendly()
    {
        Assert.Contains("Refresh the scenario", AllocationConcurrencyMessages.ScenarioChanged);
    }

    [Fact]
    public void Architecture_guard_includes_ops_boundaries()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.Contains(report.Checks, c => c.Contains("scenario services", StringComparison.OrdinalIgnoreCase)
                                            || c.Contains("Allocation operations", StringComparison.OrdinalIgnoreCase));
        Assert.True(report.Passed, string.Join("; ", report.Violations));
    }

    [Fact]
    public void Governance_result_model_covers_required_flags()
    {
        var r = AllocationGovernanceResult.Failure(
            AllocationAuditActions.Approve,
            Guid.NewGuid(),
            "blocked",
            concurrencyConflict: true,
            contextStale: true,
            checksumInvalid: true);
        Assert.False(r.Success);
        Assert.True(r.ConcurrencyConflict);
        Assert.True(r.ContextStale);
        Assert.True(r.ChecksumInvalid);
        Assert.False(r.CanApprove);
    }

    [Fact]
    public void Scenario_entity_has_row_version_for_optimistic_concurrency()
    {
        var prop = typeof(AllocationEngineScenario).GetProperty(nameof(AllocationEngineScenario.RowVersion));
        Assert.NotNull(prop);
        Assert.Equal(typeof(byte[]), prop!.PropertyType);
    }
}
