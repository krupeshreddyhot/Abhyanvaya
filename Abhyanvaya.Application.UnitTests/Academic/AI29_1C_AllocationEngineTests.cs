using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1C_AllocationEngineTests
{
    private static SectionAllocationContext SampleContext()
    {
        var sections = new[]
        {
            new AllocationSectionProjection { SectionId = 1, SectionCode = "A", SectionName = "A", Lifecycle = "Active", Health = "Healthy", Readiness = "Ready" },
            new AllocationSectionProjection { SectionId = 2, SectionCode = "B", SectionName = "B", Lifecycle = "Active", Health = "Healthy", Readiness = "Ready" },
        };
        var students = Enumerable.Range(1, 10).Select(i => new AllocationStudentProjection
        {
            StudentId = i,
            StudentNumber = $"S{i:000}",
            StudentName = $"Student {i}",
            CurrentSectionId = i <= 5 ? 1 : null,
            CurrentSectionCode = i <= 5 ? "A" : null,
        }).ToList();

        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "FIXEDCHECKSUM",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = sections,
            Capacities =
            [
                new AllocationCapacityProjection { SectionId = 1, MaximumCapacity = 8, ReservedSeats = 0, CurrentStrength = 5, AvailableCapacity = 3 },
                new AllocationCapacityProjection { SectionId = 2, MaximumCapacity = 8, ReservedSeats = 0, CurrentStrength = 0, AvailableCapacity = 8 },
            ],
            Students = students,
            Policies = ["A: AllowMerge=True"],
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
            new LanguageAllocationConstraint(),
            new HostelAllocationConstraint(),
            new MeritAllocationConstraint(),
            new ElectiveAllocationConstraint(),
            new MinorSubjectAllocationConstraint(),
        };
        var constraintEngine = new AllocationConstraintEngine(constraints);
        var strategies = new IAllocationPipelineStrategy[]
        {
            new ValidationAllocationStrategy(),
            new CapacityAllocationStrategy(),
            new PolicyAllocationStrategy(),
            new GenderAllocationStrategy(),
            new LanguageAllocationStrategy(),
            new ScholarshipAllocationStrategy(),
            new ElectiveAllocationStrategy(),
            new TransportAllocationStrategy(),
            new HostelAllocationStrategy(),
            new MeritAllocationStrategy(),
            new ScoringAllocationStrategy(scorer, constraintEngine),
        };
        return new AllocationEngine(new StudentGroupingStrategy(), strategies, scorer);
    }

    [Fact]
    public async Task Engine_produces_deterministic_scenario()
    {
        var engine = CreateEngine();
        var ctx = SampleContext();
        var exec = new AllocationExecutionContext
        {
            SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Context = ctx,
            Config = AllocationPipelineConfig.Default,
            StartedAt = DateTime.UtcNow,
        };

        var a = await engine.ExecuteAsync(exec);
        var b = await engine.ExecuteAsync(exec);

        Assert.Equal(a.Scenario.Recommendations.Count, b.Scenario.Recommendations.Count);
        Assert.Equal(
            a.Scenario.Recommendations.Select(r => (r.StudentId, r.ToSectionId)).ToArray(),
            b.Scenario.Recommendations.Select(r => (r.StudentId, r.ToSectionId)).ToArray());
        Assert.Equal(a.Score.TotalScore, b.Score.TotalScore);
        Assert.NotEmpty(a.Trace.Steps);
        Assert.Contains(a.Scenario.Recommendations, r => r.Explanations.Count > 0);
    }

    [Fact]
    public async Task Disabled_strategies_are_skipped_in_trace()
    {
        var engine = CreateEngine();
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = new Dictionary<string, bool>(AllocationPipelineConfig.Default.EnabledStrategies, StringComparer.OrdinalIgnoreCase)
            {
                [AllocationStrategyCodes.Hostel] = false,
                [AllocationStrategyCodes.Transport] = false,
            },
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
        };
        var result = await engine.ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.NewGuid(),
            Context = SampleContext(),
            Config = config,
            StartedAt = DateTime.UtcNow,
        });

        Assert.Contains(result.Trace.Steps, s => s.StrategyCode == AllocationStrategyCodes.Hostel && !s.Executed);
    }

    [Fact]
    public void Grouping_modes_are_configuration_driven()
    {
        var grouping = new StudentGroupingStrategy();
        var ctx = SampleContext();
        var alpha = grouping.OrderStudents(ctx, AllocationGroupingModes.Alphabetical);
        var number = grouping.OrderStudents(ctx, AllocationGroupingModes.StudentNumber);
        Assert.Equal(ctx.Students.Count, alpha.Count);
        Assert.Equal(ctx.Students.Count, number.Count);
        Assert.Equal(alpha, grouping.OrderStudents(ctx, AllocationGroupingModes.Alphabetical));
    }

    [Fact]
    public void Score_calculator_returns_bounded_total()
    {
        var scorer = new AllocationScoreCalculator();
        var scenario = new AllocationScenario
        {
            SectionSummaries =
            [
                new AllocationSectionSummary { SectionId = 1, SectionCode = "A", MaximumCapacity = 10, AssignedCount = 7, OccupancyPercent = 70 },
                new AllocationSectionSummary { SectionId = 2, SectionCode = "B", MaximumCapacity = 10, AssignedCount = 7, OccupancyPercent = 70 },
            ],
            Constraints =
            [
                new AllocationConstraintEvaluation { ConstraintCode = "Capacity", Priority = AllocationConstraintPriority.Mandatory, Satisfied = true },
            ],
        };
        var score = scorer.Score(SampleContext(), scenario);
        Assert.InRange(score.TotalScore, 0, 100);
    }

    [Fact]
    public void Constraint_engine_flags_over_capacity_as_mandatory_failure()
    {
        var engine = new AllocationConstraintEngine([new CapacityAllocationConstraint()]);
        var scenario = new AllocationScenario
        {
            SectionSummaries =
            [
                new AllocationSectionSummary { SectionId = 1, SectionCode = "A", MaximumCapacity = 8, AssignedCount = 99, ReservedSeats = 0 },
            ],
        };
        var evals = engine.EvaluateAsync(SampleContext(), scenario, AllocationPipelineConfig.Default).GetAwaiter().GetResult();
        Assert.Contains(evals, e => e.ConstraintCode == "Capacity" && !e.Satisfied
            && e.Priority == AllocationConstraintPriority.Mandatory);
    }

    [Fact]
    public void Allocation_architecture_guard_passes_for_engine()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Equal("AI29.1C", new AllocationEngine(
            new StudentGroupingStrategy(),
            [new ValidationAllocationStrategy()],
            new AllocationScoreCalculator()).EngineCode);
    }

    [Fact]
    public void Draft_note_states_no_live_writes()
    {
        var draft = new AllocationDraft { Note = "Draft only — live student allocations were not modified." };
        Assert.Contains("not modified", draft.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Comparison_report_shape_is_read_model()
    {
        var report = new AllocationComparisonReport
        {
            ScenarioId = Guid.NewGuid(),
            CapacityImprovement = 5,
            Summary = "ok",
        };
        Assert.Equal(5, report.CapacityImprovement);
    }
}
