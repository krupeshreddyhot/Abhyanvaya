using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.4 Prompt 4 — Configurable Roll Number Bands placement.</summary>
public sealed class AI29_1D_24B4_RollNumberBandStrategyTests
{
    private static AllocationEngine CreateEngine()
    {
        var scorer = new AllocationScoreCalculator();
        var constraints = new IAllocationConstraint[]
        {
            new CapacityAllocationConstraint(),
            new ReservedSeatsAllocationConstraint(),
        };
        var constraintEngine = new AllocationConstraintEngine(constraints);
        var strategies = new IAllocationPipelineStrategy[]
        {
            new ValidationAllocationStrategy(),
            new RollNumberBandsAllocationStrategy(),
            new CapacityAllocationStrategy(),
            new PolicyAllocationStrategy(),
            new ScoringAllocationStrategy(scorer, constraintEngine),
        };
        return new AllocationEngine(new StudentGroupingStrategy(), strategies, scorer);
    }

    private static SectionAllocationContext BuildContext(
        int studentCount,
        int sectionCount,
        int capacityPerSection,
        int? assignedStudentId = null,
        int? assignedSectionId = null)
    {
        var sections = Enumerable.Range(0, sectionCount)
            .Select(i => new AllocationSectionProjection
            {
                SectionId = i + 1,
                SectionCode = $"S{(char)('A' + i)}",
                SectionName = $"Section {(char)('A' + i)}",
                Lifecycle = "Active",
            })
            .ToList();

        var students = Enumerable.Range(1, studentCount)
            .Select(i => new AllocationStudentProjection
            {
                StudentId = i,
                StudentNumber = $"105325405{i:D3}",
                StudentName = $"Student{i:D3}",
                CurrentSectionId = assignedStudentId == i ? assignedSectionId : null,
                CurrentSectionCode = assignedStudentId == i ? sections.First(s => s.SectionId == assignedSectionId).SectionCode : null,
            })
            .ToList();

        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "BAND24B4",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = sections,
            Capacities = sections.Select(s => new AllocationCapacityProjection
            {
                SectionId = s.SectionId,
                MaximumCapacity = capacityPerSection,
                AvailableCapacity = capacityPerSection,
                ReservedSeats = 0,
            }).ToList(),
            Students = students,
        };
    }

    private static AllocationPipelineConfig BandConfig(
        int? bandSize,
        IReadOnlyList<int>? targetSectionIds = null)
    {
        var enabled = new Dictionary<string, bool>(AllocationPipelineConfig.Default.EnabledStrategies, StringComparer.OrdinalIgnoreCase)
        {
            [AllocationStrategyCodes.Capacity] = false,
            [AllocationStrategyCodes.RollNumberBands] = true,
        };
        return new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.LastThreeDigits,
            EnabledStrategies = enabled,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = AllocationPopulationSelection.AllEligible,
            TargetSectionIds = targetSectionIds,
            RollNumberBandSize = bandSize,
        }.Normalize();
    }

    private static async Task<AllocationExecutionResult> ExecuteAsync(
        AllocationEngine engine,
        SectionAllocationContext ctx,
        AllocationPipelineConfig config)
        => await engine.ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = ctx,
            Config = config,
            StartedAt = DateTime.UtcNow,
        });

    [Theory]
    [InlineData(60, 1, 60)]
    [InlineData(120, 2, 60)]
    [InlineData(240, 4, 60)]
    [InlineData(250, 5, 60)]
    [InlineData(241, 5, 60)]
    public async Task Roll_number_bands_place_by_digit_band(int students, int sections, int capacity)
    {
        var ctx = BuildContext(students, sections, capacity);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(capacity));
        Assert.True(result.Succeeded);

        var bySection = result.Scenario.Recommendations
            .GroupBy(r => r.ToSectionId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Students 001..(capacity) → section 1, etc.
        var expectedFull = Math.Min(students, capacity);
        Assert.Equal(expectedFull, bySection.GetValueOrDefault(1));

        if (students >= 61)
            Assert.Equal(Math.Min(capacity, Math.Max(0, Math.Min(students, capacity * 2) - capacity)), bySection.GetValueOrDefault(2));

        if (students == 250)
        {
            Assert.Equal(60, bySection.GetValueOrDefault(1));
            Assert.Equal(60, bySection.GetValueOrDefault(2));
            Assert.Equal(60, bySection.GetValueOrDefault(3));
            Assert.Equal(60, bySection.GetValueOrDefault(4));
            Assert.Equal(10, bySection.GetValueOrDefault(5));
        }

        if (students == 241)
        {
            Assert.Equal(1, bySection.GetValueOrDefault(5)); // 241 only in band 4
        }
    }

    [Fact]
    public async Task Capacity_50_limits_band_section_without_silent_overflow()
    {
        var ctx = BuildContext(60, 2, 50);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(60)); // band width 60, but section capacity 50
        Assert.True(result.Succeeded);
        Assert.Equal(50, result.Scenario.Recommendations.Count(r => r.ToSectionId == 1));
        Assert.Contains(result.Warnings, w => w.Contains("sufficient capacity", StringComparison.OrdinalIgnoreCase)
            || w.Contains("more students than", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capacity_75_allows_full_first_band_of_60()
    {
        var ctx = BuildContext(60, 2, 75);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(60));
        Assert.True(result.Succeeded);
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 1));
    }

    [Fact]
    public async Task Explicit_target_section_limits_available_bands()
    {
        var ctx = BuildContext(120, 4, 60);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(60, targetSectionIds: [1]));
        Assert.True(result.Succeeded);
        Assert.All(result.Scenario.Recommendations, r => Assert.Equal(1, r.ToSectionId));
        Assert.Equal(60, result.Scenario.Recommendations.Count); // only first band fits section 1
        Assert.Contains(result.Warnings, w => w.Contains("exceeds", StringComparison.OrdinalIgnoreCase)
            || w.Contains("sufficient capacity", StringComparison.OrdinalIgnoreCase)
            || w.Contains("more students than", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Existing_assigned_student_kept_when_capacity_allows()
    {
        var ctx = BuildContext(10, 2, 60, assignedStudentId: 5, assignedSectionId: 2);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(60));
        Assert.True(result.Succeeded);
        var kept = result.Scenario.Recommendations.Single(r => r.StudentId == 5);
        Assert.Equal(2, kept.ToSectionId);
    }

    [Fact]
    public async Task Band_size_defaults_from_first_section_capacity_when_null()
    {
        var ctx = BuildContext(120, 2, 60);
        var engine = CreateEngine();
        var result = await ExecuteAsync(engine, ctx, BandConfig(null));
        Assert.True(result.Succeeded);
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 1));
        Assert.Equal(60, result.Scenario.Recommendations.Count(r => r.ToSectionId == 2));
    }

    [Fact]
    public async Task Capacity_strategy_still_works_when_roll_bands_disabled()
    {
        var ctx = BuildContext(10, 2, 60);
        var engine = CreateEngine();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.LastThreeDigits,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = AllocationPopulationSelection.AllEligible,
        }.Normalize();
        Assert.False(config.EnabledStrategies[AllocationStrategyCodes.RollNumberBands]);
        var result = await ExecuteAsync(engine, ctx, config);
        Assert.True(result.Succeeded);
        Assert.Equal(10, result.Scenario.Recommendations.Count);
    }

    [Fact]
    public void ConfigJson_includes_roll_number_band_size()
    {
        var config = BandConfig(60).Normalize();
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        Assert.Contains("RollNumberBandSize", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("60", json);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<AllocationPipelineConfig>(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(60, roundTrip!.Normalize().RollNumberBandSize);
    }
}
