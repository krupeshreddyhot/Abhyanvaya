using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.4A — Existing assignment policy, section order, band/capacity.</summary>
public sealed class AI29_1D_24B4A_ExistingAssignmentPolicyTests
{
    private static AllocationEngine CreateEngine()
    {
        var scorer = new AllocationScoreCalculator();
        var constraints = new IAllocationConstraint[] { new CapacityAllocationConstraint() };
        var constraintEngine = new AllocationConstraintEngine(constraints);
        return new AllocationEngine(
            new StudentGroupingStrategy(),
            [
                new ValidationAllocationStrategy(),
                new RollNumberBandsAllocationStrategy(),
                new CapacityAllocationStrategy(),
                new ScoringAllocationStrategy(scorer, constraintEngine),
            ],
            scorer);
    }

    private static SectionAllocationContext Build(
        int students,
        (int Id, string Code, int Order, int Cap)[] sections,
        Dictionary<int, int>? assigned = null)
    {
        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "POL24B4A",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = sections.Select(s => new AllocationSectionProjection
            {
                SectionId = s.Id,
                SectionCode = s.Code,
                SectionName = s.Code,
                Lifecycle = "Active",
                DisplayOrder = s.Order,
            }).ToList(),
            Capacities = sections.Select(s => new AllocationCapacityProjection
            {
                SectionId = s.Id,
                MaximumCapacity = s.Cap,
                AvailableCapacity = s.Cap,
            }).ToList(),
            Students = Enumerable.Range(1, students).Select(i => new AllocationStudentProjection
            {
                StudentId = i,
                StudentNumber = $"105325405{i:D3}",
                StudentName = $"S{i}",
                CurrentSectionId = assigned != null && assigned.TryGetValue(i, out var sid) ? sid : null,
                CurrentSectionCode = assigned != null && assigned.TryGetValue(i, out var sid2)
                    ? sections.First(x => x.Id == sid2).Code
                    : null,
            }).ToList(),
        };
    }

    private static AllocationPipelineConfig Config(
        string policy,
        int? bandSize = 60,
        IReadOnlyList<int>? targets = null,
        bool rollBands = true)
    {
        var enabled = new Dictionary<string, bool>(AllocationPipelineConfig.Default.EnabledStrategies, StringComparer.OrdinalIgnoreCase)
        {
            [AllocationStrategyCodes.Capacity] = !rollBands,
            [AllocationStrategyCodes.RollNumberBands] = rollBands,
        };
        return new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.LastThreeDigits,
            EnabledStrategies = enabled,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = AllocationPopulationSelection.AllEligible,
            TargetSectionIds = targets,
            RollNumberBandSize = bandSize,
            ExistingAssignmentPolicy = policy,
        }.Normalize();
    }

    private static Task<AllocationExecutionResult> Run(SectionAllocationContext ctx, AllocationPipelineConfig config)
        => CreateEngine().ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = ctx,
            Config = config,
            StartedAt = DateTime.UtcNow,
        });

    [Fact]
    public async Task All_unassigned_roll_bands_by_display_order()
    {
        var ctx = Build(120, [(1, "A", 1, 60), (2, "B", 2, 60)]);
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.PreserveExisting));
        Assert.True(result.Succeeded);
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 1));
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 2));
    }

    [Fact]
    public async Task DisplayOrder_beats_lexical_section_codes()
    {
        // Lexical would put "10" before "2"; DisplayOrder puts 2 first.
        var ctx = Build(120, [(10, "10", 2, 60), (2, "2", 1, 60)]);
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.Reallocate, bandSize: 60));
        Assert.True(result.Succeeded);
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 2)); // first band
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 10));
    }

    [Fact]
    public async Task Preserve_keeps_assigned_even_when_band_differs()
    {
        var ctx = Build(10, [(1, "A", 1, 60), (2, "B", 2, 60)], assigned: new Dictionary<int, int> { [5] = 2 });
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.PreserveExisting));
        var kept = result.Scenario.Recommendations.Single(r => r.StudentId == 5);
        Assert.Equal(2, kept.ToSectionId);
        Assert.Contains(kept.Explanations, e => e.Contains("Preserve Existing Assignments", StringComparison.OrdinalIgnoreCase)
            || e.Contains("Kept in Section", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reallocate_moves_assigned_to_band_section()
    {
        var ctx = Build(10, [(1, "A", 1, 60), (2, "B", 2, 60)], assigned: new Dictionary<int, int> { [5] = 2 });
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.Reallocate));
        var moved = result.Scenario.Recommendations.Single(r => r.StudentId == 5);
        Assert.Equal(1, moved.ToSectionId); // last3=005 → band 0 → A
        Assert.Contains(moved.Explanations, e => e.Contains("Reassigned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preserve_does_not_move_outside_explicit_target()
    {
        var ctx = Build(10, [(1, "A", 1, 60), (2, "B", 2, 60)], assigned: new Dictionary<int, int> { [5] = 2 });
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.PreserveExisting, targets: [1]));
        Assert.DoesNotContain(result.Scenario.Recommendations, r => r.StudentId == 5 && r.ToSectionId == 1);
        Assert.DoesNotContain(result.Scenario.Recommendations, r => r.StudentId == 5);
    }

    [Fact]
    public async Task Legacy_outside_target_may_be_reconsidered()
    {
        var ctx = Build(10, [(1, "A", 1, 60), (2, "B", 2, 60)], assigned: new Dictionary<int, int> { [5] = 2 });
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.LegacyPreserveWhenCapacityAllows, targets: [1]));
        var rec = result.Scenario.Recommendations.SingleOrDefault(r => r.StudentId == 5);
        Assert.NotNull(rec);
        Assert.Equal(1, rec!.ToSectionId);
    }

    [Fact]
    public async Task Band_60_capacity_50_warns_and_does_not_overflow()
    {
        var ctx = Build(60, [(1, "A", 1, 50), (2, "B", 2, 50)]);
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.Reallocate, bandSize: 60));
        Assert.True(result.Succeeded);
        Assert.Equal(50, result.Scenario.Recommendations.Count(r => r.ToSectionId == 1));
        Assert.Contains(result.Warnings, w => w.Contains("more students than", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Explicit_A_only_under_reallocate()
    {
        var ctx = Build(120, [(1, "A", 1, 60), (2, "B", 2, 60)]);
        var result = await Run(ctx, Config(ExistingAssignmentPolicies.Reallocate, targets: [1]));
        Assert.All(result.Scenario.Recommendations, r => Assert.Equal(1, r.ToSectionId));
    }

    [Fact]
    public async Task Repeated_simulation_is_deterministic()
    {
        var ctx = Build(90, [(1, "A", 1, 60), (2, "B", 2, 60), (3, "C", 3, 60)]);
        var config = Config(ExistingAssignmentPolicies.PreserveExisting);
        var a = await Run(ctx, config);
        var b = await Run(ctx, config);
        Assert.Equal(
            a.Scenario.Recommendations.Select(r => (r.StudentId, r.ToSectionId)).ToList(),
            b.Scenario.Recommendations.Select(r => (r.StudentId, r.ToSectionId)).ToList());
    }

    [Fact]
    public void ConfigJson_includes_existing_assignment_policy()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Config(ExistingAssignmentPolicies.PreserveExisting));
        Assert.Contains("ExistingAssignmentPolicy", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PreserveExisting", json);
    }

    [Fact]
    public void Null_policy_normalizes_to_legacy()
    {
        var n = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.LastThreeDigits,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            ExistingAssignmentPolicy = null,
        }.Normalize();
        Assert.Equal(ExistingAssignmentPolicies.LegacyPreserveWhenCapacityAllows, n.ExistingAssignmentPolicy);
    }
}
