using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.4 Prompt 6 — Population filter must not silently change strategy (and vice versa).</summary>
public sealed class AI29_1D_24B4_PopulationStrategyInteractionTests
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

    private static SectionAllocationContext Ctx()
    {
        var students = Enumerable.Range(1, 120)
            .Select(i => new AllocationStudentProjection
            {
                StudentId = i,
                StudentNumber = $"105325405{i:D3}",
                StudentName = $"S{i}",
            })
            .ToList();
        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "IX24B4",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections =
            [
                new() { SectionId = 1, SectionCode = "A", SectionName = "A", Lifecycle = "Active" },
                new() { SectionId = 2, SectionCode = "B", SectionName = "B", Lifecycle = "Active" },
            ],
            Capacities =
            [
                new() { SectionId = 1, MaximumCapacity = 60, AvailableCapacity = 60 },
                new() { SectionId = 2, MaximumCapacity = 60, AvailableCapacity = 60 },
            ],
            Students = students,
        };
    }

    private static AllocationPipelineConfig Config(
        string grouping,
        string populationMode,
        string? from,
        string? to,
        bool rollBands,
        IReadOnlyList<int>? targets = null)
    {
        var enabled = new Dictionary<string, bool>(AllocationPipelineConfig.Default.EnabledStrategies, StringComparer.OrdinalIgnoreCase)
        {
            [AllocationStrategyCodes.Capacity] = !rollBands,
            [AllocationStrategyCodes.RollNumberBands] = rollBands,
        };
        return new AllocationPipelineConfig
        {
            GroupingMode = grouping,
            EnabledStrategies = enabled,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = populationMode,
                FromStudentNumber = from,
                ToStudentNumber = to,
            },
            TargetSectionIds = targets,
            RollNumberBandSize = rollBands ? 60 : null,
        }.Normalize();
    }

    [Theory]
    [InlineData(AllocationPopulationModes.StudentNumberRange, AllocationGroupingModes.StudentNumber, false)]
    [InlineData(AllocationPopulationModes.StudentNumberRange, AllocationGroupingModes.LastThreeDigits, false)]
    [InlineData(AllocationPopulationModes.LastThreeDigitsRange, AllocationGroupingModes.LastThreeDigits, false)]
    [InlineData(AllocationPopulationModes.LastThreeDigitsRange, AllocationGroupingModes.StudentNumber, false)]
    [InlineData(AllocationPopulationModes.LastThreeDigitsRange, AllocationGroupingModes.LastThreeDigits, true)]
    [InlineData(AllocationPopulationModes.AllEligible, AllocationGroupingModes.LastThreeDigits, true)]
    public async Task Population_and_strategy_remain_independent(string popMode, string grouping, bool rollBands)
    {
        var from = popMode == AllocationPopulationModes.AllEligible ? null : (popMode == AllocationPopulationModes.LastThreeDigitsRange ? "001" : "105325405001");
        var to = popMode == AllocationPopulationModes.AllEligible ? null : (popMode == AllocationPopulationModes.LastThreeDigitsRange ? "060" : "105325405060");
        var config = Config(grouping, popMode, from, to, rollBands);
        Assert.Equal(grouping, config.GroupingMode);
        Assert.Equal(popMode == AllocationPopulationModes.AllEligible ? AllocationPopulationModes.AllEligible : popMode, config.PopulationSelection!.Mode);
        Assert.Equal(rollBands, config.EnabledStrategies[AllocationStrategyCodes.RollNumberBands]);

        var result = await CreateEngine().ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = Ctx(),
            Config = config,
            StartedAt = DateTime.UtcNow,
        });
        Assert.True(result.Succeeded);
        Assert.Equal(config.PopulationSelection.Mode, result.Scenario.Metadata["PopulationMode"]);
    }

    [Fact]
    public async Task Explicit_section_with_roll_bands_only_uses_selected_targets()
    {
        var config = Config(
            AllocationGroupingModes.LastThreeDigits,
            AllocationPopulationModes.AllEligible,
            null,
            null,
            rollBands: true,
            targets: [1]);
        var result = await CreateEngine().ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = Ctx(),
            Config = config,
            StartedAt = DateTime.UtcNow,
        });
        Assert.True(result.Succeeded);
        Assert.All(result.Scenario.Recommendations, r => Assert.Equal(1, r.ToSectionId));
    }

    [Fact]
    public void Last3_filter_does_not_enable_roll_bands()
    {
        var config = Config(
            AllocationGroupingModes.Alphabetical,
            AllocationPopulationModes.LastThreeDigitsRange,
            "046",
            "050",
            rollBands: false);
        Assert.False(config.EnabledStrategies[AllocationStrategyCodes.RollNumberBands]);
        Assert.Equal(AllocationPopulationModes.LastThreeDigitsRange, config.PopulationSelection!.Mode);
        Assert.Equal(AllocationGroupingModes.Alphabetical, config.GroupingMode);
    }
}
