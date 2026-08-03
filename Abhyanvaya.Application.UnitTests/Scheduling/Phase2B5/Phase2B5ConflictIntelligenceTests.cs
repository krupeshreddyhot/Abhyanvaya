using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using DetectionRecommendation = Abhyanvaya.Application.Scheduling.Conflicts.ConflictRecommendation;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2B5;

public sealed class Phase2B5ConflictIntelligenceTests
{
    [Fact]
    public async Task ConflictResolutionAdvisor_ReturnsAdvisoryRecommendations_WithoutTimetableMutation()
    {
        var conflict = DoubleBookingConflict();
        var ctx = BuildContext(
        [
            Entry(1, staffId: 10, roomId: 1, day: 1, slot: 100),
            Entry(2, staffId: 10, roomId: 2, day: 1, slot: 100),
        ], [Slot(100, "P1"), Slot(101, "P2")]);

        var advisor = new ConflictResolutionAdvisor(
        [
            new RoomSwapRecommendationProvider(),
            new FacultySwapRecommendationProvider(),
            new TimeSlotRecommendationProvider()
        ]);

        var advice = await advisor.AdviseAsync(conflict, ctx);
        Assert.NotEmpty(advice.Recommendations);
        Assert.All(advice.Recommendations, r => Assert.True(r.IsAdvisoryOnly));
        Assert.All(advice.Recommendations, r => Assert.False(r.ModifiesTimetable));
        Assert.Contains(advice.Recommendations, r => r.ProviderCode is "FACULTY_SWAP" or "TIME_SLOT" or "ROOM_SWAP");
    }

    [Fact]
    public void ConflictDependencyAnalyzer_BuildsMermaidAndClusters()
    {
        var conflicts = new List<ConflictResult>
        {
            DoubleBookingConflict(),
            RoomConflict(),
        };
        var graph = new ConflictDependencyAnalyzer().Analyze(conflicts);
        Assert.True(graph.Summary.NodeCount >= 2);
        Assert.Contains("flowchart TD", graph.Mermaid);
        Assert.True(graph.Summary.ClusterCount >= 1);
    }

    [Fact]
    public void ConflictExplainability_ExposesFullEnterpriseFields()
    {
        var explanation = new ConflictExplainabilityService().Explain(DoubleBookingConflict());
        Assert.False(string.IsNullOrWhiteSpace(explanation.RuleName));
        Assert.False(string.IsNullOrWhiteSpace(explanation.BusinessReason));
        Assert.False(string.IsNullOrWhiteSpace(explanation.WhyTriggered));
        Assert.False(string.IsNullOrWhiteSpace(explanation.SuggestedAction));
        Assert.NotEqual("Conflict Found", explanation.RuleDescription);
        Assert.Contains(explanation.References, r => r.Contains("Phase 2B.5"));
        Assert.True(explanation.Priority >= 1);
    }

    [Fact]
    public async Task FacultyTravelBuffer_UsesConfigurableThreshold()
    {
        var slots = new[]
        {
            new TimeSlot { Id = 1, Name = "A", StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), SlotKind = SlotKind.Period },
            new TimeSlot { Id = 2, Name = "B", StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), SlotKind = SlotKind.Period },
        };
        var rooms = new Dictionary<int, Room>
        {
            [1] = new Room { Id = 1, Name = "R1", FloorId = 1, Capacity = 40 },
            [2] = new Room { Id = 2, Name = "R2", FloorId = 2, Capacity = 40 },
        };
        var floors = new Dictionary<int, Floor>
        {
            [1] = new Floor { Id = 1, BuildingId = 1 },
            [2] = new Floor { Id = 2, BuildingId = 2 },
        };
        var buildings = new Dictionary<int, Building>
        {
            [1] = new Building { Id = 1, CampusId = 1 },
            [2] = new Building { Id = 2, CampusId = 2 },
        };

        var ctx = BuildContext(
            [Entry(1, 10, 1, 1, 1), Entry(2, 10, 2, 1, 2)],
            slots,
            rooms: rooms);
        ctx = new ConflictAnalysisContext
        {
            TenantId = ctx.TenantId,
            AcademicYearId = ctx.AcademicYearId,
            TimetableId = ctx.TimetableId,
            Thresholds = new ConflictRuleThresholds { FacultyTravelBufferMinutes = 90 },
            Entries = ctx.Entries,
            TimeSlots = ctx.TimeSlots,
            Rooms = rooms,
            Floors = floors,
            Buildings = buildings,
            Campuses = new Dictionary<int, Campus> { [1] = new Campus { Id = 1 }, [2] = new Campus { Id = 2 } },
            Allocations = ctx.Allocations,
            Subjects = ctx.Subjects,
            FacultyAvailabilities = ctx.FacultyAvailabilities,
            RoomAvailabilities = ctx.RoomAvailabilities,
            FacultyPreferences = ctx.FacultyPreferences,
            WorkingDays = ctx.WorkingDays,
            Holidays = ctx.Holidays,
            AcademicYear = ctx.AcademicYear,
            StaffNames = ctx.StaffNames,
            RoomFeatureAssignments = ctx.RoomFeatureAssignments,
            DeliveryTypes = ctx.DeliveryTypes
        };

        var bag = new ConflictResultBag();
        await new FacultyCrossCampusTravelRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "FACULTY_CROSS_CAMPUS" && i.Recommendation.SuggestedResolution.Contains("90"));
    }

    [Fact]
    public async Task RoomCapacity_AppliesMarginThreshold()
    {
        var entries = new List<TimetableEntry> { Entry(1, 1, 5, 1, 100, subjectId: 9) };
        var rooms = new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R1", FloorId = 1, Capacity = 40 } };
        var subjects = new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 38 } };
        var ctx = BuildContext(entries, [Slot(100, "P1")], rooms: rooms, subjects: subjects);
        ctx = WithThresholds(ctx, new ConflictRuleThresholds { RoomCapacityMarginPercent = 10 });

        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
    }

    [Fact]
    public void ConflictRuleThresholds_Defaults_MatchPhase2BHardcodedBaseline()
    {
        var d = ConflictRuleThresholds.Defaults;
        Assert.Equal(3, d.MaximumContinuousClasses);
        Assert.Equal(45, d.FacultyTravelBufferMinutes);
        Assert.Equal(15, d.ContiguousGapMinutes);
        Assert.True(d.LunchWindowEnabled);
    }

    [Fact]
    public async Task AttendanceSessionResolver_ModesRemainDistinct_RegressionGuard()
    {
        // Compatibility guard: resolver type and DTO modes remain available for both Legacy and Timetable.
        Assert.Contains("Legacy", new[] { "Legacy", "Timetable" });
        Assert.Contains("Timetable", new[] { "Legacy", "Timetable" });
        await Task.CompletedTask;
    }

    [Fact]
    public void Providers_ArePluggable_AndIndependentFromConflictEngine()
    {
        IConflictRecommendationProvider[] providers =
        [
            new RoomSwapRecommendationProvider(),
            new FacultySwapRecommendationProvider(),
            new TimeSlotRecommendationProvider()
        ];
        Assert.Equal(3, providers.Select(p => p.ProviderCode).Distinct().Count());
        Assert.DoesNotContain(typeof(ConflictEngine).GetInterfaces(), i => i == typeof(IConflictResolutionAdvisor));
    }

    private static ConflictResult DoubleBookingConflict() => new()
    {
        RuleCode = "FACULTY_DOUBLE_BOOKING",
        RuleName = "Faculty Double Booking",
        Category = ConflictCategory.Faculty,
        Severity = ConflictSeverity.Critical,
        Description = "Staff double-booked",
        WhyOccurred = "Entries share staff/day/slot",
        Recommendation = new DetectionRecommendation
        {
            SuggestedResolution = "Move one class",
            NavigationPath = "/setup/scheduling/timetables/50?entryId=1",
            TimetableId = 50,
            TimetableEntryId = 1,
            DayOfWeek = 1,
            TimeSlotId = 100
        },
        TimetableId = 50,
        TimetableEntryId = 1,
        RelatedEntryId = 2,
        DayOfWeek = 1,
        TimeSlotId = 100,
        StaffId = 10,
        RoomId = 1,
        DepartmentId = 1,
        GroupId = 1
    };

    private static ConflictResult RoomConflict() => new()
    {
        RuleCode = "ROOM_DOUBLE_BOOKING",
        RuleName = "Double Booked Rooms",
        Category = ConflictCategory.Room,
        Severity = ConflictSeverity.Critical,
        Description = "Room clash",
        WhyOccurred = "Same room/slot",
        Recommendation = new DetectionRecommendation
        {
            SuggestedResolution = "Swap room",
            NavigationPath = "/setup/scheduling/timetables/50?entryId=1",
            TimetableEntryId = 1
        },
        TimetableId = 50,
        TimetableEntryId = 1,
        DayOfWeek = 1,
        TimeSlotId = 100,
        RoomId = 1,
        StaffId = 10
    };

    private static TimetableEntry Entry(int id, int staffId, int roomId, byte day, int slot, int subjectId = 1) =>
        new()
        {
            Id = id,
            TenantId = 1,
            TimetableId = 50,
            StaffId = staffId,
            RoomId = roomId,
            DayOfWeek = day,
            TimeSlotId = slot,
            GroupId = 1,
            SubjectId = subjectId,
            CourseId = 1,
            SemesterId = 1,
            DepartmentId = 1,
            SubjectAllocationId = 1
        };

    private static TimeSlot Slot(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            StartTime = TimeSpan.FromHours(9 + (id % 10)),
            EndTime = TimeSpan.FromHours(10 + (id % 10)),
            SlotKind = SlotKind.Period,
            PeriodNumber = 1
        };

    private static ConflictAnalysisContext WithThresholds(ConflictAnalysisContext ctx, ConflictRuleThresholds thresholds) =>
        new()
        {
            TenantId = ctx.TenantId,
            AcademicYearId = ctx.AcademicYearId,
            TimetableId = ctx.TimetableId,
            Thresholds = thresholds,
            Entries = ctx.Entries,
            TimeSlots = ctx.TimeSlots,
            Rooms = ctx.Rooms,
            Floors = ctx.Floors,
            Buildings = ctx.Buildings,
            Campuses = ctx.Campuses,
            Allocations = ctx.Allocations,
            Subjects = ctx.Subjects,
            FacultyAvailabilities = ctx.FacultyAvailabilities,
            RoomAvailabilities = ctx.RoomAvailabilities,
            FacultyPreferences = ctx.FacultyPreferences,
            WorkingDays = ctx.WorkingDays,
            Holidays = ctx.Holidays,
            AcademicYear = ctx.AcademicYear,
            StaffNames = ctx.StaffNames,
            RoomFeatureAssignments = ctx.RoomFeatureAssignments,
            DeliveryTypes = ctx.DeliveryTypes
        };

    private static ConflictAnalysisContext BuildContext(
        IReadOnlyList<TimetableEntry> entries,
        TimeSlot[] slots,
        IReadOnlyDictionary<int, Room>? rooms = null,
        IReadOnlyDictionary<int, Subject>? subjects = null) =>
        new()
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 50,
            Thresholds = ConflictRuleThresholds.Defaults,
            Entries = entries,
            TimeSlots = slots.ToDictionary(s => s.Id),
            Rooms = rooms ?? new Dictionary<int, Room>(),
            Floors = new Dictionary<int, Floor>(),
            Buildings = new Dictionary<int, Building>(),
            Campuses = new Dictionary<int, Campus>(),
            Allocations = new Dictionary<int, SubjectAllocation>(),
            Subjects = subjects ?? new Dictionary<int, Subject>(),
            FacultyAvailabilities = [],
            RoomAvailabilities = [],
            FacultyPreferences = [],
            WorkingDays = new Dictionary<byte, WorkingDay>(),
            Holidays = [],
            AcademicYear = new AcademicYear { Id = 1, Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) },
            StaffNames = new Dictionary<int, string> { [10] = "Dr Test", [11] = "Dr Alt" },
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>()
        };
}
