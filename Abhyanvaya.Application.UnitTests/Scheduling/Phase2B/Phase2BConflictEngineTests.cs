using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2B;

public sealed class Phase2BConflictEngineTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingConflictView)]
    [InlineData(PermissionKeys.SchedulingConflictManage)]
    public void PermissionKeys_All_ContainsPhase2BConflictKeys(string key) =>
        Assert.Contains(key, PermissionKeys.All);

    [Fact]
    public void ConflictEngine_RegistersPluginRules_AndDoesNotBlockEditing()
    {
        var rules = CreateAllRules();
        var engine = new ConflictEngine(rules);
        Assert.True(engine.RegisteredRules.Count >= 25);
        Assert.Contains(engine.RegisteredRules, r => r.RuleCode == "FACULTY_DOUBLE_BOOKING");
        Assert.Contains(engine.RegisteredRules, r => r.RuleCode == "ROOM_DOUBLE_BOOKING");
        Assert.Contains(engine.RegisteredRules, r => r.RuleCode == "STUDENT_GROUP_OVERLAP");
        Assert.Contains(engine.RegisteredRules, r => r.RuleCode == "CALENDAR_HOLIDAY");
    }

    [Fact]
    public async Task FacultyDoubleBooking_EmitsCritical_WithExplainability()
    {
        var entries = new List<TimetableEntry>
        {
            Entry(1, staffId: 10, roomId: 1, day: 1, slot: 100),
            Entry(2, staffId: 10, roomId: 2, day: 1, slot: 100),
        };
        var ctx = BuildContext(entries, slots: [Slot(100, "P1")]);
        var bag = new ConflictResultBag();
        await new FacultyDoubleBookingRule().AnalyzeAsync(ctx, bag);

        Assert.NotEmpty(bag.Items);
        Assert.All(bag.Items, i => Assert.Equal(ConflictSeverity.Critical, i.Severity));
        Assert.All(bag.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.WhyOccurred)));
        Assert.All(bag.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Recommendation.SuggestedResolution)));
        Assert.All(bag.Items, i => Assert.Contains("/setup/scheduling/timetables/", i.Recommendation.NavigationPath));
        Assert.False(bag.BuildSummary(1, 1, 1, null, DateTime.UtcNow, "Test").BlocksEditing);
    }

    [Fact]
    public async Task RoomDoubleBooking_EmitsCritical()
    {
        var entries = new List<TimetableEntry>
        {
            Entry(1, staffId: 1, roomId: 5, day: 2, slot: 100),
            Entry(2, staffId: 2, roomId: 5, day: 2, slot: 100),
        };
        var rooms = new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "Lab-1", FloorId = 1, Capacity = 40 } };
        var ctx = BuildContext(entries, slots: [Slot(100, "P1")], rooms: rooms);
        var bag = new ConflictResultBag();
        await new RoomDoubleBookingRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_DOUBLE_BOOKING" && i.Description.Contains("Lab-1"));
    }

    [Fact]
    public async Task RoomCapacity_WhenExceeded_EmitsError()
    {
        var entries = new List<TimetableEntry> { Entry(1, staffId: 1, roomId: 5, day: 1, slot: 100, subjectId: 9) };
        var rooms = new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R1", FloorId = 1, Capacity = 10 } };
        var subjects = new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } };
        var ctx = BuildContext(entries, slots: [Slot(100, "P1")], rooms: rooms, subjects: subjects);
        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_CAPACITY" && i.Severity == ConflictSeverity.Error);
    }

    [Fact]
    public async Task StudentGroupOverlap_EmitsCritical()
    {
        var entries = new List<TimetableEntry>
        {
            Entry(1, staffId: 1, roomId: 1, day: 1, slot: 100, groupId: 7),
            Entry(2, staffId: 2, roomId: 2, day: 1, slot: 100, groupId: 7),
        };
        var ctx = BuildContext(entries, slots: [Slot(100, "P1")]);
        var bag = new ConflictResultBag();
        await new StudentGroupOverlapRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "STUDENT_GROUP_OVERLAP");
    }

    [Fact]
    public async Task CalendarWorkingDay_NonWorking_EmitsError()
    {
        var entries = new List<TimetableEntry> { Entry(1, staffId: 1, roomId: 1, day: 0, slot: 100) };
        var working = new Dictionary<byte, WorkingDay> { [0] = new WorkingDay { DayOfWeek = 0, IsWorking = false } };
        var ctx = BuildContext(entries, slots: [Slot(100, "P1")], workingDays: working);
        var bag = new ConflictResultBag();
        await new CalendarWorkingDayRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "CALENDAR_WORKING_DAY" && i.Severity == ConflictSeverity.Error);
    }

    [Fact]
    public async Task FacultyLunchViolation_WhenOverlapsLunch_EmitsWarning()
    {
        var entries = new List<TimetableEntry> { Entry(1, staffId: 1, roomId: 1, day: 1, slot: 100) };
        var slots = new Dictionary<int, TimeSlot>
        {
            [100] = new TimeSlot
            {
                Id = 100,
                Name = "P4",
                StartTime = TimeSpan.FromHours(12),
                EndTime = TimeSpan.FromHours(13),
                SlotKind = SlotKind.Period
            },
            [200] = new TimeSlot
            {
                Id = 200,
                Name = "Lunch",
                StartTime = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30)),
                EndTime = TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(15)),
                SlotKind = SlotKind.Lunch
            }
        };
        var ctx = BuildContext(entries, slots: slots.Values.ToArray());
        var bag = new ConflictResultBag();
        await new FacultyLunchViolationRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "FACULTY_LUNCH_VIOLATION");
    }

    [Fact]
    public async Task ConflictEngine_ExecuteAsync_AggregatesMultipleRules()
    {
        var entries = new List<TimetableEntry>
        {
            Entry(1, staffId: 10, roomId: 5, day: 1, slot: 100, groupId: 3),
            Entry(2, staffId: 10, roomId: 5, day: 1, slot: 100, groupId: 3),
        };
        var ctx = BuildContext(
            entries,
            slots: [Slot(100, "P1")],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R", FloorId = 1, Capacity = 20 } });
        var bag = await new ConflictEngine(CreateAllRules()).ExecuteAsync(ctx);
        Assert.True(bag.Items.Count >= 2);
        Assert.Contains(bag.Items, i => i.Category == ConflictCategory.Faculty);
        Assert.Contains(bag.Items, i => i.Category == ConflictCategory.Room);
        Assert.Contains(bag.Items, i => i.Category == ConflictCategory.Student);
    }

    [Fact]
    public void ConflictSummary_NeverBlocksEditing_EvenWhenCritical()
    {
        var bag = new ConflictResultBag();
        bag.Add(new ConflictResult
        {
            RuleCode = "X",
            RuleName = "X",
            Category = ConflictCategory.Faculty,
            Severity = ConflictSeverity.Critical,
            Description = "Critical",
            WhyOccurred = "Why",
            Recommendation = new ConflictRecommendation { SuggestedResolution = "Move" }
        });
        var summary = bag.BuildSummary(1, 1, 1, null, DateTime.UtcNow, "Test");
        Assert.Equal(1, summary.CriticalCount);
        Assert.False(summary.BlocksEditing);
    }

    private static IReadOnlyList<IConflictRule> CreateAllRules() =>
    [
        new FacultyDoubleBookingRule(),
        new FacultyAvailabilityRule(),
        new FacultyPreferenceRule(),
        new FacultyMaximumContinuousClassesRule(),
        new FacultyBreakViolationRule(),
        new FacultyCrossCampusTravelRule(),
        new FacultyLunchViolationRule(),
        new FacultyWorkingDayViolationRule(),
        new RoomDoubleBookingRule(),
        new RoomCapacityExceededRule(),
        new RoomWrongFeatureRule(),
        new RoomWrongTypeRule(),
        new RoomUnavailableRule(),
        new RoomMaintenanceConflictRule(),
        new RoomLabRequirementRule(),
        new StudentGroupOverlapRule(),
        new StudentSemesterOverlapRule(),
        new StudentDuplicateSubjectRule(),
        new StudentElectiveOverlapRule(),
        new StudentBatchConflictRule(),
        new StudentPracticalConflictRule(),
        new StudentTutorialConflictRule(),
        new CalendarHolidayRule(),
        new CalendarWorkingDayRule(),
        new CalendarSemesterRule(),
        new CalendarAcademicYearRule(),
        new CalendarClosedCampusRule(),
        new CalendarHolidayTypeRule(),
    ];

    private static TimetableEntry Entry(
        int id,
        int staffId,
        int roomId,
        byte day,
        int slot,
        int groupId = 1,
        int subjectId = 1,
        int courseId = 1,
        int semesterId = 1) =>
        new()
        {
            Id = id,
            TenantId = 1,
            TimetableId = 50,
            StaffId = staffId,
            RoomId = roomId,
            DayOfWeek = day,
            TimeSlotId = slot,
            GroupId = groupId,
            SubjectId = subjectId,
            CourseId = courseId,
            SemesterId = semesterId,
            DepartmentId = 1,
            SubjectAllocationId = 1
        };

    private static TimeSlot Slot(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            SlotKind = SlotKind.Period,
            PeriodNumber = 1
        };

    private static ConflictAnalysisContext BuildContext(
        IReadOnlyList<TimetableEntry> entries,
        TimeSlot[] slots,
        IReadOnlyDictionary<int, Room>? rooms = null,
        IReadOnlyDictionary<int, Subject>? subjects = null,
        IReadOnlyDictionary<byte, WorkingDay>? workingDays = null) =>
        new()
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 50,
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
            WorkingDays = workingDays ?? new Dictionary<byte, WorkingDay>(),
            Holidays = [],
            AcademicYear = new AcademicYear { Id = 1, Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) },
            StaffNames = new Dictionary<int, string> { [10] = "Dr Test" },
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>()
        };
}
