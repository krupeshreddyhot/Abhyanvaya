using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 3A — ConflictEngine and SoftValidation share room-capacity semantics.
/// </summary>
public sealed class AiSchedCapPrompt3ARoomCapacityAlignmentTests
{
    private readonly IRoomCapacityEvaluator _evaluator = RoomCapacityEvaluator.Instance;
    private readonly IPlacementSizeResolver _placement = PlacementSizeResolver.Instance;

    [Theory]
    [InlineData(50, 10, 45, false)] // exactly at effective
    [InlineData(50, 10, 44, false)] // below
    [InlineData(50, 10, 46, true)]  // above
    [InlineData(50, 0, 50, false)]  // zero margin exact
    [InlineData(50, 0, 51, true)]   // zero margin above
    [InlineData(40, 10, 38, true)]  // Phase2B5-style: effective 36, placement 38
    public void Evaluator_margin_matrix(int roomCap, decimal margin, int placementSize, bool expectExceeded)
    {
        var placement = PlacementSizeResolution.From(PlacementSizeSource.SubjectExpectedCapacity, placementSize);
        var eval = _evaluator.Evaluate(roomCap, margin, placement);
        Assert.True(eval.IsEvaluable);
        Assert.Equal(expectExceeded, eval.IsExceeded);
        Assert.Equal(roomCap * (1m - margin / 100m), eval.EffectiveCapacity);
    }

    [Fact]
    public void Evaluator_zero_PlacementSize_not_exceeded_when_room_positive()
    {
        var placement = PlacementSizeResolution.From(PlacementSizeSource.ResolvedStudentCount, 0);
        var eval = _evaluator.Evaluate(10, 0, placement);
        Assert.True(eval.IsEvaluable);
        Assert.False(eval.IsExceeded);
    }

    [Fact]
    public void Evaluator_unset_PlacementSize_not_evaluable()
    {
        var eval = _evaluator.Evaluate(50, 10, PlacementSizeResolution.Unset);
        Assert.False(eval.IsEvaluable);
        Assert.False(eval.IsExceeded);
    }

    [Theory]
    [InlineData(50, 10, 45, false)]
    [InlineData(50, 10, 46, true)]
    [InlineData(50, 0, 50, false)]
    [InlineData(50, 0, 51, true)]
    [InlineData(10, 0, 0, false)]
    public async Task ConflictEngine_and_SoftPath_agree_on_ROOM_CAPACITY(
        int roomCap,
        decimal margin,
        int placementSize,
        bool expectConflict)
    {
        var placement = PlacementSizeResolution.From(PlacementSizeSource.SubjectExpectedCapacity, placementSize);
        var softExceeded = _evaluator.Evaluate(roomCap, margin, placement).IsExceeded;

        var entry = new TimetableEntry
        {
            Id = 1,
            TenantId = 1,
            TimetableId = 50,
            StaffId = 10,
            RoomId = 5,
            DayOfWeek = 1,
            TimeSlotId = 100,
            GroupId = 1,
            SubjectId = 9,
            CourseId = 1,
            SemesterId = 1,
            DepartmentId = 1,
            SubjectAllocationId = 1
        };
        var ctx = new ConflictAnalysisContext
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 50,
            Thresholds = new ConflictRuleThresholds { RoomCapacityMarginPercent = margin },
            Entries = [entry],
            TimeSlots = new Dictionary<int, TimeSlot>
            {
                [100] = new TimeSlot
                {
                    Id = 100,
                    Name = "P1",
                    StartTime = TimeSpan.FromHours(9),
                    EndTime = TimeSpan.FromHours(10),
                    SlotKind = SlotKind.Period,
                    PeriodNumber = 1
                }
            },
            Rooms = new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R", FloorId = 1, Capacity = roomCap } },
            Floors = new Dictionary<int, Floor>(),
            Buildings = new Dictionary<int, Building>(),
            Campuses = new Dictionary<int, Campus>(),
            Allocations = new Dictionary<int, SubjectAllocation>(),
            Subjects = new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = placementSize } },
            FacultyAvailabilities = [],
            RoomAvailabilities = [],
            FacultyPreferences = [],
            WorkingDays = new Dictionary<byte, WorkingDay>(),
            Holidays = [],
            AcademicYear = new AcademicYear
            {
                Id = 1,
                Name = "2026",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            },
            StaffNames = new Dictionary<int, string>(),
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>(),
            PlacementSizeResolver = _placement,
            RoomCapacityEvaluator = _evaluator
        };

        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        var engineExceeded = bag.Items.Any(i => i.RuleCode == "ROOM_CAPACITY");

        Assert.Equal(expectConflict, softExceeded);
        Assert.Equal(expectConflict, engineExceeded);
        Assert.Equal(softExceeded, engineExceeded);
    }

    [Fact]
    public async Task Both_paths_skip_when_PlacementSize_unset()
    {
        var placement = PlacementSizeResolution.Unset;
        Assert.False(_evaluator.Evaluate(50, 10, placement).IsExceeded);

        var entry = new TimetableEntry
        {
            Id = 1,
            TenantId = 1,
            TimetableId = 50,
            StaffId = 10,
            RoomId = 5,
            DayOfWeek = 1,
            TimeSlotId = 100,
            GroupId = 1,
            SubjectId = 9,
            CourseId = 1,
            SemesterId = 1,
            DepartmentId = 1,
            SubjectAllocationId = 1
        };
        var ctx = new ConflictAnalysisContext
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 50,
            Thresholds = new ConflictRuleThresholds { RoomCapacityMarginPercent = 10 },
            Entries = [entry],
            TimeSlots = new Dictionary<int, TimeSlot>
            {
                [100] = new TimeSlot
                {
                    Id = 100,
                    Name = "P1",
                    StartTime = TimeSpan.FromHours(9),
                    EndTime = TimeSpan.FromHours(10),
                    SlotKind = SlotKind.Period,
                    PeriodNumber = 1
                }
            },
            Rooms = new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R", FloorId = 1, Capacity = 50 } },
            Floors = new Dictionary<int, Floor>(),
            Buildings = new Dictionary<int, Building>(),
            Campuses = new Dictionary<int, Campus>(),
            Allocations = new Dictionary<int, SubjectAllocation>(),
            Subjects = new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = null } },
            FacultyAvailabilities = [],
            RoomAvailabilities = [],
            FacultyPreferences = [],
            WorkingDays = new Dictionary<byte, WorkingDay>(),
            Holidays = [],
            AcademicYear = new AcademicYear
            {
                Id = 1,
                Name = "2026",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            },
            StaffNames = new Dictionary<int, string>(),
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>(),
            PlacementSizeResolver = _placement,
            RoomCapacityEvaluator = _evaluator
        };

        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.DoesNotContain(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
    }
}
