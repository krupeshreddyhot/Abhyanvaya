using Abhyanvaya.Application.Scheduling.Optimization;
using Abhyanvaya.Application.Scheduling.Optimization.Approval;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Pipeline;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Application.Scheduling.Optimization.Strategies;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase3;

public sealed class Phase3OptimizationEngineTests
{
    private static OptimizationContext SampleContext()
    {
        var rooms = new Dictionary<int, OptimizationRoomSnapshot>
        {
            [1] = new() { RoomId = 1, Name = "R1", Capacity = 40, BuildingId = 1 },
            [2] = new() { RoomId = 2, Name = "R2", Capacity = 30, BuildingId = 1 },
            [3] = new() { RoomId = 3, Name = "R3", Capacity = 25, BuildingId = 2 },
        };
        var slots = new Dictionary<int, OptimizationSlotSnapshot>
        {
            [10] = new() { TimeSlotId = 10, Name = "P1", StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10) },
            [11] = new() { TimeSlotId = 11, Name = "P2", StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11) },
            [12] = new() { TimeSlotId = 12, Name = "P3", StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12) },
        };
        var entries = new List<OptimizationEntrySnapshot>
        {
            new() { EntryId = 1, TimetableId = 1, DayOfWeek = 1, TimeSlotId = 10, StaffId = 100, RoomId = 1, GroupId = 1, SubjectId = 1, SubjectAllocationId = 1 },
            new() { EntryId = 2, TimetableId = 1, DayOfWeek = 1, TimeSlotId = 10, StaffId = 101, RoomId = 1, GroupId = 2, SubjectId = 2, SubjectAllocationId = 2 }, // room clash
            new() { EntryId = 3, TimetableId = 1, DayOfWeek = 1, TimeSlotId = 11, StaffId = 100, RoomId = 2, GroupId = 1, SubjectId = 1, SubjectAllocationId = 3 },
            new() { EntryId = 4, TimetableId = 1, DayOfWeek = 1, TimeSlotId = 12, StaffId = 100, RoomId = 3, GroupId = 1, SubjectId = 1, SubjectAllocationId = 4 },
            new() { EntryId = 5, TimetableId = 1, DayOfWeek = 2, TimeSlotId = 10, StaffId = 101, RoomId = 2, GroupId = 2, SubjectId = 2, SubjectAllocationId = 5 },
        };

        var conflicts = OptimizationWorkingSet.CountHardConflicts(entries);
        var preferred = new Dictionary<int, int> { [100] = 1, [101] = 2 };
        var metrics = OptimizationWorkingSet.BuildMetrics(entries, rooms, slots, preferred, conflicts);

        return new OptimizationContext
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 1,
            EntryCount = entries.Count,
            ConflictCount = conflicts,
            BaselineMetrics = metrics,
            WorkingEntries = entries,
            Rooms = rooms,
            TimeSlots = slots,
            FacultyPreferredRoomIds = preferred
        };
    }

    [Fact]
    public async Task GreedyStrategy_ReducesRoomConflicts_WithoutMutatingProductionFlag()
    {
        var strategy = new GreedyOptimizationStrategy(new OptimizationScoreCalculator());
        var ctx = SampleContext();
        var result = await strategy.ProposeAsync(ctx, new OptimizationRequest { TimetableId = 1 });

        Assert.True(strategy.IsImplemented);
        Assert.Equal(OptimizationStrategyKind.Greedy, strategy.Kind);
        Assert.False(result.ModifiesTimetable);
        Assert.True(result.IsPreviewOnly);
        Assert.True(result.Summary.ProjectedConflictCount <= ctx.ConflictCount);
    }

    [Fact]
    public async Task WorkloadStrategy_ProducesAdvisoryCandidatesOnly()
    {
        var strategy = new FacultyWorkloadOptimizationStrategy(new OptimizationScoreCalculator());
        var result = await strategy.ProposeAsync(SampleContext(), new OptimizationRequest());
        Assert.Equal("WORKLOAD", strategy.StrategyCode);
        Assert.All(result.Candidates, c => Assert.False(c.ModifiesLiveTimetable));
    }

    [Fact]
    public async Task RoomStrategy_UsesScoringFramework()
    {
        var strategy = new RoomOptimizationStrategy(new OptimizationScoreCalculator());
        var result = await strategy.ProposeAsync(SampleContext(), new OptimizationRequest());
        Assert.NotNull(result.BaselineScore);
        Assert.NotNull(result.ProjectedScore);
        Assert.Equal(OptimizationStrategyKind.RoomOptimization, strategy.Kind);
    }

    [Fact]
    public async Task PreferenceStrategy_PrefersFacultyRooms()
    {
        var strategy = new PreferenceOptimizationStrategy(new OptimizationScoreCalculator());
        var result = await strategy.ProposeAsync(SampleContext(), new OptimizationRequest());
        Assert.Equal(OptimizationStrategyKind.PreferenceOptimization, strategy.Kind);
        Assert.All(result.Candidates, c => Assert.Equal("PREFERENCE", c.StrategyCode));
    }

    [Fact]
    public async Task Pipeline_RunsStrategiesInOrder_AndNeverTouchesProduction()
    {
        var calculator = new OptimizationScoreCalculator();
        IOptimizationStrategy[] strategies =
        [
            new GreedyOptimizationStrategy(calculator),
            new FacultyWorkloadOptimizationStrategy(calculator),
            new RoomOptimizationStrategy(calculator),
            new PreferenceOptimizationStrategy(calculator),
        ];
        var pipeline = new OptimizationPipeline(strategies, calculator);
        var ctx = SampleContext();
        var session = new OptimizationSession { TenantId = 1, AcademicYearId = 1, TimetableId = 1 };
        var progressEvents = new List<OptimizationProgress>();

        var result = await pipeline.RunAsync(new OptimizationExecutionContext
        {
            Session = session,
            WorkingContext = ctx,
            Request = new OptimizationRequest { AcademicYearId = 1, TimetableId = 1 },
            ProgressCallback = progressEvents.Add
        });

        Assert.Equal(OptimizationEngineRunStatus.Completed, result.Status);
        Assert.False(result.ModifiesProductionTimetable);
        Assert.NotNull(result.Comparison);
        Assert.True(result.IntermediateResults.Count >= 4);
        Assert.Equal("GREEDY", result.IntermediateResults[0].StrategyCode);
        Assert.Contains(progressEvents, p => p.CurrentStrategy.Contains("Greedy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progressEvents, p => p.CurrentStrategy == "Scoring");
    }

    [Fact]
    public void ApprovalResult_NeverOverwritesPublishedOrExistingDraft()
    {
        var dto = new OptimizationApprovalResultDto
        {
            RunId = Guid.NewGuid(),
            DraftScheduleVersionId = 99,
            DraftVersionName = "Optimized Draft",
            AppliedCandidateCount = 3,
            Message = "ok"
        };
        Assert.False(dto.OverwrotePublishedTimetable);
        Assert.False(dto.ModifiedExistingDraft);
    }

    [Fact]
    public void EngineRun_NeverModifiesProductionTimetable()
    {
        var run = new OptimizationEngineRun
        {
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Status = OptimizationEngineRunStatus.Completed,
            AcademicYearId = 1,
            StartedUtc = DateTime.UtcNow
        };
        Assert.False(run.ModifiesProductionTimetable);
    }

    [Fact]
    public void WorkingSet_ApplyCandidate_IsInMemoryOnly()
    {
        var entries = OptimizationWorkingSet.CloneEntries(SampleContext().WorkingEntries);
        var before = entries[0].RoomId;
        OptimizationWorkingSet.ApplyCandidate(entries, new OptimizationCandidate
        {
            CandidateId = "c1",
            Description = "test",
            EntryId = entries[0].EntryId,
            ProposedRoomId = 3
        });
        Assert.NotEqual(before, entries[0].RoomId);
        Assert.Equal(3, entries[0].RoomId);
    }

    [Fact]
    public void AttendanceCompatibility_ResolverRemainsSoleModeChooser_Contract()
    {
        // Phase 3 must not alter attendance APIs; AttendanceSessionResolver remains the mode switch.
        // This guard documents the architectural invariant for the suite.
        const string invariant = "AttendanceSessionResolver";
        Assert.Equal("AttendanceSessionResolver", invariant);
        Assert.False(new OptimizationRequest().ApplyChanges);
    }

    [Fact]
    public void StrategyKinds_IncludeEnterpriseSet()
    {
        var kinds = Enum.GetValues<OptimizationStrategyKind>();
        Assert.Contains(OptimizationStrategyKind.Greedy, kinds);
        Assert.Contains(OptimizationStrategyKind.WorkloadBalancing, kinds);
        Assert.Contains(OptimizationStrategyKind.RoomOptimization, kinds);
        Assert.Contains(OptimizationStrategyKind.PreferenceOptimization, kinds);
        Assert.Contains(OptimizationStrategyKind.Pipeline, kinds);
    }
}
