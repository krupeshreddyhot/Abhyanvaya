using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 6 — Publish Readiness service behavior.</summary>
public sealed class AiSchedCapPrompt6PublishReadinessServiceTests
{
    [Theory]
    [InlineData(ConflictSeverity.Critical, "FACULTY_DOUBLE_BOOKING", true)]
    [InlineData(ConflictSeverity.Error, "ROOM_CAPACITY", true)]
    [InlineData(ConflictSeverity.Error, "TEACHING_GROUP_CAPACITY_EXCEEDED", true)]
    [InlineData(ConflictSeverity.Error, "ROOM_WRONG_TYPE", false)]
    [InlineData(ConflictSeverity.Warning, "FACULTY_PREFERENCE", false)]
    [InlineData(ConflictSeverity.Information, "SOME_INFO", false)]
    public void IsBlockingConflict_matches_Prompt5_contract(
        ConflictSeverity severity, string code, bool expected)
    {
        var item = new ConflictResult
        {
            RuleCode = code,
            RuleName = code,
            Category = ConflictCategory.Other,
            Severity = severity,
            Description = "d",
            WhyOccurred = "w",
            Recommendation = new ConflictRecommendation { SuggestedResolution = "a" }
        };
        Assert.Equal(expected, TimetablePublishReadinessService.IsBlockingConflict(item));
    }

    [Fact]
    public void Ordering_is_deterministic_and_blockers_first()
    {
        var findings = new[]
        {
            Finding("WARN_A", "Warning", false, entryId: 2),
            Finding("ROOM_CAPACITY", "Error", true, entryId: 1),
            Finding("TEACHING_GROUP_CAPACITY_EXCEEDED", "Error", true, entryId: 1),
            Finding("WARN_B", "Warning", false, entryId: 1),
        };

        var a = TimetablePublishReadinessService.OrderDeterministically(findings).Select(f => f.Code).ToList();
        var b = TimetablePublishReadinessService.OrderDeterministically(findings.Reverse()).Select(f => f.Code).ToList();
        Assert.Equal(a, b);
        Assert.True(a.IndexOf("ROOM_CAPACITY") < a.IndexOf("WARN_A"));
        Assert.True(a.IndexOf("TEACHING_GROUP_CAPACITY_EXCEEDED") < a.IndexOf("WARN_B"));
    }

    [Fact]
    public async Task Evaluate_Locked_clean_timetable_IsReady_true()
    {
        var service = CreateService(
            Timetable(status: TimetableStatus.Locked),
            bag: new ConflictResultBag());

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.True(result.IsReady);
        Assert.Equal(0, result.BlockingFindingCount);
        Assert.Equal(TimetableStatus.Locked, result.LifecycleState);
    }

    [Fact]
    public async Task Evaluate_Draft_adds_lifecycle_blocker()
    {
        var service = CreateService(
            Timetable(status: TimetableStatus.Draft),
            bag: new ConflictResultBag());

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f =>
            f.Code == TimetablePublishReadinessService.LifecycleNotEligibleCode && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_Critical_conflict_blocks()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("ROOM_DOUBLE_BOOKING", ConflictSeverity.Critical, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f => f.Code == "ROOM_DOUBLE_BOOKING" && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_ROOM_CAPACITY_blocks()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("ROOM_CAPACITY", ConflictSeverity.Error, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f => f.Code == "ROOM_CAPACITY" && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_TEACHING_GROUP_CAPACITY_EXCEEDED_blocks()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("TEACHING_GROUP_CAPACITY_EXCEEDED", ConflictSeverity.Error, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f => f.Code == "TEACHING_GROUP_CAPACITY_EXCEEDED" && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_non_capacity_Error_does_not_block()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("ROOM_WRONG_TYPE", ConflictSeverity.Error, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.True(result.IsReady);
        Assert.Contains(result.Findings, f => f.Code == "ROOM_WRONG_TYPE" && !f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_Warning_does_not_block()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("FACULTY_PREFERENCE", ConflictSeverity.Warning, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.True(result.IsReady);
        Assert.Equal(1, result.WarningFindingCount);
    }

    [Fact]
    public async Task Evaluate_Frozen_blocks()
    {
        var tt = Timetable(status: TimetableStatus.Locked);
        tt.IsFrozen = true;
        var service = CreateService(tt, new ConflictResultBag());

        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f =>
            f.Code == TimetablePublishReadinessService.LifecycleFrozenCode && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_Archived_blocks()
    {
        var service = CreateService(Timetable(status: TimetableStatus.Archived), new ConflictResultBag());
        var result = await service.EvaluatePublishReadinessAsync(50);
        Assert.False(result.IsReady);
        Assert.Contains(result.Findings, f =>
            f.Code == TimetablePublishReadinessService.LifecycleArchivedCode && f.IsBlocking);
    }

    [Fact]
    public async Task Evaluate_missing_timetable_throws()
    {
        var service = CreateService(timetable: null, bag: new ConflictResultBag());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EvaluatePublishReadinessAsync(999));
    }

    [Fact]
    public async Task Evaluate_repeated_calls_same_ordering()
    {
        var bag = new ConflictResultBag();
        bag.Add(Conflict("ROOM_CAPACITY", ConflictSeverity.Error, entryId: 2));
        bag.Add(Conflict("FACULTY_PREFERENCE", ConflictSeverity.Warning, entryId: 1));
        bag.Add(Conflict("TEACHING_GROUP_CAPACITY_EXCEEDED", ConflictSeverity.Error, entryId: 1));
        var service = CreateService(Timetable(status: TimetableStatus.Locked), bag);

        var first = (await service.EvaluatePublishReadinessAsync(50)).Findings.Select(f => f.Code).ToList();
        var second = (await service.EvaluatePublishReadinessAsync(50)).Findings.Select(f => f.Code).ToList();
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Evaluate_honors_cancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new Mock<IConflictAnalysisRunner>();
        runner.Setup(r => r.AnalyzeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var timetableRepo = new Mock<ITimetableRepository>();
        timetableRepo.Setup(r => r.GetByIdAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Timetable(status: TimetableStatus.Locked));

        var db = new Mock<IApplicationDbContext>();
        db.Setup(d => d.SchedulingTimetables).Returns(new List<Timetable>().AsAsyncQueryable());

        var service = new TimetablePublishReadinessService(
            timetableRepo.Object,
            Mock.Of<IScheduleVersionRepository>(),
            db.Object,
            CurrentUser(1),
            runner.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.EvaluatePublishReadinessAsync(50, cts.Token));
    }

    [Fact]
    public async Task Evaluate_does_not_call_ConflictDetection_persistence_path()
    {
        // Uses IConflictAnalysisRunner only — never SaveRun.
        var runner = new Mock<IConflictAnalysisRunner>(MockBehavior.Strict);
        runner.Setup(r => r.AnalyzeAsync(1, 10, 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmptyContext(), new ConflictResultBag()));

        var timetableRepo = new Mock<ITimetableRepository>();
        timetableRepo.Setup(r => r.GetByIdAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Timetable(status: TimetableStatus.Locked));

        var db = new Mock<IApplicationDbContext>();
        db.Setup(d => d.SchedulingTimetables).Returns(Array.Empty<Timetable>().AsAsyncQueryable());

        var service = new TimetablePublishReadinessService(
            timetableRepo.Object,
            Mock.Of<IScheduleVersionRepository>(),
            db.Object,
            CurrentUser(1),
            runner.Object);

        _ = await service.EvaluatePublishReadinessAsync(50);
        runner.Verify(r => r.AnalyzeAsync(1, 10, 50, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TimetablePublishReadinessService CreateService(Timetable? timetable, ConflictResultBag bag)
    {
        var timetableRepo = new Mock<ITimetableRepository>();
        timetableRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(timetable);

        var runner = new Mock<IConflictAnalysisRunner>();
        runner.Setup(r => r.AnalyzeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmptyContext(), bag));

        var db = new Mock<IApplicationDbContext>();
        db.Setup(d => d.SchedulingTimetables).Returns(Array.Empty<Timetable>().AsAsyncQueryable());

        return new TimetablePublishReadinessService(
            timetableRepo.Object,
            Mock.Of<IScheduleVersionRepository>(),
            db.Object,
            CurrentUser(1),
            runner.Object);
    }

    private static Timetable Timetable(TimetableStatus status) => new()
    {
        Id = 50,
        TenantId = 1,
        Name = "T",
        AcademicYearId = 10,
        DepartmentId = null,
        Status = status,
        IsFrozen = false
    };

    private static ICurrentUserService CurrentUser(int tenantId)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(x => x.TenantId).Returns(tenantId);
        return m.Object;
    }

    private static ConflictAnalysisContext EmptyContext() =>
        new()
        {
            TenantId = 1,
            AcademicYearId = 10,
            TimetableId = 50,
            Entries = [],
            TimeSlots = new Dictionary<int, TimeSlot>(),
            Rooms = new Dictionary<int, Room>(),
            Floors = new Dictionary<int, Floor>(),
            Buildings = new Dictionary<int, Building>(),
            Campuses = new Dictionary<int, Campus>(),
            Allocations = new Dictionary<int, SubjectAllocation>(),
            Subjects = new Dictionary<int, Subject>(),
            FacultyAvailabilities = [],
            RoomAvailabilities = [],
            FacultyPreferences = [],
            WorkingDays = new Dictionary<byte, WorkingDay>(),
            Holidays = [],
            AcademicYear = null,
            StaffNames = new Dictionary<int, string>(),
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>()
        };

    private static ConflictResult Conflict(string code, ConflictSeverity severity, int entryId) => new()
    {
        RuleCode = code,
        RuleName = code,
        Category = ConflictCategory.Room,
        Severity = severity,
        Description = code,
        WhyOccurred = "why",
        Recommendation = new ConflictRecommendation { SuggestedResolution = "act" },
        TimetableId = 50,
        TimetableEntryId = entryId,
        DayOfWeek = 1,
        TimeSlotId = 100,
        RoomId = 5
    };

    private static PublishReadinessFindingDto Finding(
        string code, string severity, bool blocking, int? entryId) => new()
    {
        Code = code,
        Severity = severity,
        IsBlocking = blocking,
        Title = code,
        Why = "w",
        RecommendedAction = "a",
        TimetableEntryId = entryId
    };
}
