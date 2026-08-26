using System.Text.Json;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1D_Prompt10A_AllocationScopeTests
{
    private static SectionAllocationContext SampleContext()
    {
        var sections = new[]
        {
            new AllocationSectionProjection { SectionId = 1, SectionCode = "A", SectionName = "A", Lifecycle = "Active" },
            new AllocationSectionProjection { SectionId = 2, SectionCode = "B", SectionName = "B", Lifecycle = "Active" },
            new AllocationSectionProjection { SectionId = 3, SectionCode = "C", SectionName = "C", Lifecycle = "Active" },
        };
        var students = new List<AllocationStudentProjection>
        {
            new() { StudentId = 1, StudentNumber = "A10", StudentName = "Ada", Gender = "Female", Language = "Telugu" },
            new() { StudentId = 2, StudentNumber = "A2", StudentName = "Bob", Gender = "Male", Language = "Hindi" },
            new() { StudentId = 3, StudentNumber = "B01", StudentName = "Cara", Gender = "Female", Language = "Telugu" },
            new() { StudentId = 4, StudentNumber = "C99", StudentName = "Dan", Gender = "Male" },
        };

        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "SCOPECHECKSUM",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = sections,
            Capacities =
            [
                new AllocationCapacityProjection { SectionId = 1, MaximumCapacity = 10, AvailableCapacity = 10 },
                new AllocationCapacityProjection { SectionId = 2, MaximumCapacity = 10, AvailableCapacity = 10 },
                new AllocationCapacityProjection { SectionId = 3, MaximumCapacity = 10, AvailableCapacity = 10 },
            ],
            Students = students,
        };
    }

    private static AllocationEngine CreateEngine()
    {
        var scorer = new AllocationScoreCalculator();
        var constraints = new IAllocationConstraint[]
        {
            new CapacityAllocationConstraint(),
            new ReservedSeatsAllocationConstraint(),
            new GenderBalanceAllocationConstraint(),
        };
        var constraintEngine = new AllocationConstraintEngine(constraints);
        var strategies = new IAllocationPipelineStrategy[]
        {
            new ValidationAllocationStrategy(),
            new CapacityAllocationStrategy(),
            new PolicyAllocationStrategy(),
            new ScoringAllocationStrategy(scorer, constraintEngine),
        };
        return new AllocationEngine(new StudentGroupingStrategy(), strategies, scorer);
    }

    [Fact]
    public void All_eligible_students_resolves_full_context()
    {
        var ctx = SampleContext();
        var v = AllocationScopeSelectionValidator.Validate(ctx, AllocationPipelineConfig.Default);
        Assert.True(v.IsValid);
        Assert.Equal(4, v.ResolvedStudentIds.Count);
        Assert.Equal(3, v.ResolvedSectionIds.Count);
        Assert.Equal("All eligible students", v.PopulationSummary);
        Assert.Equal("All eligible sections", v.TargetSectionsSummary);
    }

    [Fact]
    public void Student_number_range_filters_deterministically()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentNumberRange,
                FromStudentNumber = "A10",
                ToStudentNumber = "A2",
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 1, 2 }, v.ResolvedStudentIds);
    }

    [Fact]
    public void Explicit_student_ids_honored_when_in_context()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentIds,
                StudentIds = [3, 1],
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 1, 3 }, v.ResolvedStudentIds);
    }

    [Fact]
    public void Gender_filter_uses_context_facet_only()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.Gender,
                FacetValue = "Female",
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 1, 3 }, v.ResolvedStudentIds);
    }

    [Fact]
    public void Unsupported_facet_is_rejected()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.Hostel,
                FacetValue = "Block-A",
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Unavailable", AllocationScopeSelectionValidator.FacetReadiness(ctx.Students, AllocationPopulationModes.Hostel));
    }

    [Fact]
    public void Invalid_range_rejected()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentNumberRange,
                FromStudentNumber = "B01",
                ToStudentNumber = "A10",
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("Invalid student number range", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Student_outside_context_rejected()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentIds,
                StudentIds = [1, 999],
            },
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("999") && e.Contains("cannot be injected"));
    }

    [Fact]
    public void Explicit_target_sections_resolved()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = [2, 1],
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 1, 2 }, v.ResolvedSectionIds);
        Assert.Contains("Selected sections", v.TargetSectionsSummary);
    }

    [Fact]
    public void All_sections_when_targets_omitted()
    {
        var ctx = SampleContext();
        var v = AllocationScopeSelectionValidator.Validate(ctx, AllocationPipelineConfig.Default);
        Assert.Equal(new[] { 1, 2, 3 }, v.ResolvedSectionIds);
    }

    [Fact]
    public void Target_section_not_in_context_rejected()
    {
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = [1, 99],
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("99"));
    }

    [Fact]
    public void Different_population_produces_different_config_json()
    {
        var a = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = AllocationPopulationSelection.AllEligible,
        }.Normalize();
        var b = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.Gender,
                FacetValue = "Female",
            },
        }.Normalize();
        var ja = JsonSerializer.Serialize(a);
        var jb = JsonSerializer.Serialize(b);
        Assert.NotEqual(ja, jb);
    }

    [Fact]
    public void Different_target_sections_produce_different_config_json()
    {
        var a = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = [1, 2],
        }.Normalize();
        var b = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = [1, 3],
        }.Normalize();
        Assert.NotEqual(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Fact]
    public void Governance_checksum_includes_selection_via_config_json()
    {
        var cfgA = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = AllocationPopulationSelection.AllEligible,
        }.Normalize();
        var cfgB = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentIds,
                StudentIds = [1, 2],
            },
        }.Normalize();

        string Hash(AllocationPipelineConfig cfg) => AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            VersionNumber = 1,
            ContextVersion = "abc",
            ContextChecksum = "checksum",
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = "Draft",
            Operation = "Create",
            Score = 10,
            ScenarioJson = "{}",
            TraceJson = "[]",
            ConfigJson = JsonSerializer.Serialize(cfg),
        });

        Assert.NotEqual(Hash(cfgA), Hash(cfgB));
    }

    [Fact]
    public void Replay_preserves_population_selection_in_normalized_config()
    {
        var cfg = new AllocationPipelineConfig
        {
            GroupingMode = "Alphabetical",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentNumberRange,
                FromStudentNumber = "A10",
                ToStudentNumber = "B01",
            },
            TargetSectionIds = [1, 2],
        }.Normalize();
        var json = JsonSerializer.Serialize(cfg);
        var roundTrip = JsonSerializer.Deserialize<AllocationPipelineConfig>(json)!.Normalize();
        Assert.Equal(AllocationPopulationModes.StudentNumberRange, roundTrip.PopulationSelection!.Mode);
        Assert.Equal("A10", roundTrip.PopulationSelection.FromStudentNumber);
        Assert.Equal(new[] { 1, 2 }, roundTrip.TargetSectionIds);
    }

    [Fact]
    public void Compare_preserves_population_selection_roundtrip()
    {
        var cfg = new AllocationPipelineConfig
        {
            GroupingMode = "Gender",
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.Language,
                FacetValue = "Telugu",
            },
        }.Normalize();
        var again = JsonSerializer.Deserialize<AllocationPipelineConfig>(JsonSerializer.Serialize(cfg))!.Normalize();
        Assert.Equal(cfg.PopulationSelection!.Mode, again.PopulationSelection!.Mode);
        Assert.Equal(cfg.PopulationSelection.FacetValue, again.PopulationSelection.FacetValue);
    }

    [Fact]
    public void Legacy_request_behavior_unchanged_all_eligible()
    {
        var legacy = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = null,
            TargetSectionIds = null,
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(SampleContext(), legacy);
        Assert.True(v.IsValid);
        Assert.Equal(4, v.ResolvedStudentIds.Count);
        Assert.Equal(3, v.ResolvedSectionIds.Count);
    }

    [Fact]
    public async Task Engine_honors_population_and_target_sections()
    {
        var engine = CreateEngine();
        var ctx = SampleContext();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.Gender,
                FacetValue = "Female",
            },
            TargetSectionIds = [1, 2],
        }.Normalize();

        var result = await engine.ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = ctx,
            Config = config,
            StartedAt = DateTime.UtcNow,
        });

        Assert.All(result.Scenario.Recommendations, r => Assert.Contains(r.StudentId, new[] { 1, 3 }));
        Assert.All(result.Scenario.Recommendations, r => Assert.Contains(r.ToSectionId, new[] { 1, 2 }));
        Assert.DoesNotContain(result.Scenario.SectionSummaries, s => s.SectionId == 3 && s.AssignedCount > 0);
        Assert.Equal("Gender", result.Scenario.Metadata["PopulationMode"]);
    }

    [Fact]
    public async Task Engine_rejects_student_outside_context()
    {
        var engine = CreateEngine();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = new AllocationPopulationSelection
            {
                Mode = AllocationPopulationModes.StudentIds,
                StudentIds = [999],
            },
        }.Normalize();

        await Assert.ThrowsAsync<ArgumentException>(() => engine.ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = SampleContext(),
            Config = config,
            StartedAt = DateTime.UtcNow,
        }));
    }

    [Fact]
    public void Architecture_guard_still_passes()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
    }

    [Fact]
    public void Language_facet_partially_available()
    {
        var ctx = SampleContext();
        Assert.Equal("PartiallyAvailable", AllocationScopeSelectionValidator.FacetReadiness(ctx.Students, AllocationPopulationModes.Language));
        Assert.Equal("Available", AllocationScopeSelectionValidator.FacetReadiness(ctx.Students, AllocationPopulationModes.Gender));
    }
}
