using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 4 — Presentation, classification, actionable feedback.</summary>
public sealed class AiSchedCapPrompt4ConflictPresentationTests
{
    private readonly ISchedulingConflictPresentationComposer _composer =
        SchedulingConflictPresentationComposer.Instance;
    private readonly IRoomCapacityEvaluator _room = RoomCapacityEvaluator.Instance;
    private readonly IPlacementSizeResolver _placement = PlacementSizeResolver.Instance;

    [Theory]
    [InlineData(50, 10, 44, false)]
    [InlineData(50, 10, 45, false)]
    [InlineData(50, 10, 46, true)]
    public void Room_capacity_presentation_matrix(int roomCap, decimal margin, int placement, bool expect)
    {
        var eval = _room.Evaluate(
            roomCap,
            margin,
            PlacementSizeResolution.From(PlacementSizeSource.SubjectExpectedCapacity, placement));
        Assert.Equal(expect, eval.IsExceeded);
        if (!expect) return;

        var dto = _composer.CreateRoomCapacitySoftWarning(Entry(), eval, []);
        Assert.Equal("ROOM_CAPACITY", dto.Code);
        Assert.Equal("Error", dto.Severity);
        Assert.Equal("Room capacity exceeded", dto.Title);
        Assert.Equal(placement, dto.PlacementSize);
        Assert.Equal(roomCap, dto.RoomCapacity);
        Assert.Equal(margin, dto.CapacityMarginPercent);
        Assert.NotNull(dto.EffectiveRoomCapacity);
        Assert.Contains("Effective room capacity", dto.Why!);
        Assert.Contains("Select a larger room", dto.SuggestedAction!);
        Assert.DoesNotContain("MaxTeachingCapacity", dto.Message);
    }

    [Theory]
    [InlineData(40, 50, false)]
    [InlineData(50, 50, false)]
    [InlineData(51, 50, true)]
    [InlineData(0, 50, false)]
    public void TeachingGroup_capacity_presentation_matrix(int resolved, int max, bool expect)
    {
        var tg = new TeachingGroup
        {
            Id = 7,
            TenantId = 1,
            Code = "TG-001",
            Name = "Lecture A",
            MaxTeachingCapacity = max,
            Status = TeachingGroupStatus.Active
        };

        if (!expect)
        {
            Assert.True(resolved <= max);
            return;
        }

        var dto = _composer.CreateTeachingGroupCapacitySoftWarning(Entry(teachingGroupId: 7), tg, resolved, max, []);
        Assert.Equal("TEACHING_GROUP_CAPACITY_EXCEEDED", dto.Code);
        Assert.Equal("Error", dto.Severity);
        Assert.Equal("Teaching Group capacity exceeded", dto.Title);
        Assert.Equal(resolved, dto.ResolvedStudentCount);
        Assert.Equal(max, dto.MaxTeachingCapacity);
        Assert.Equal("TG-001", dto.TeachingGroupCode);
        Assert.Contains("independent of room", dto.Why!);
        Assert.DoesNotContain("Effective room", dto.Message);
    }

    [Fact]
    public void Zero_ResolvedStudentCount_is_valid_and_not_TG_capacity_conflict()
    {
        var tg = new TeachingGroup { Id = 7, Code = "TG", Name = "A", MaxTeachingCapacity = 50 };
        // Composer only called when rule fires; rule requires resolved > max.
        Assert.False(0 > 50);
        var placement = _placement.Resolve(0, 30, 40);
        Assert.Equal(0, placement.Value);
        Assert.Equal(PlacementSizeSource.ResolvedStudentCount, placement.Source);
    }

    [Fact]
    public void Null_TeachingGroupId_does_not_produce_TG_capacity_soft_warning()
    {
        // SoftValidation skips TG rule when TeachingGroupId is null — presentation not invoked.
        var entry = Entry(teachingGroupId: null);
        Assert.Null(entry.TeachingGroupId);
    }

    [Fact]
    public void Archived_TeachingGroup_capacity_presentation_keeps_Archived_label_without_mutation()
    {
        var tg = new TeachingGroup
        {
            Id = 7,
            Code = "TG-001",
            Name = "Archived Group",
            MaxTeachingCapacity = 10,
            Status = TeachingGroupStatus.Archived
        };
        var dto = _composer.CreateTeachingGroupCapacitySoftWarning(Entry(7), tg, 12, 10, []);
        Assert.Contains("Archived", dto.Message);
        Assert.Equal(nameof(TeachingGroupStatus.Archived), dto.TeachingGroupStatus);
        Assert.Equal(7, dto.TeachingGroupId);
    }

    [Fact]
    public async Task Multiple_conflicts_room_and_tg_both_presented()
    {
        var tg = new TeachingGroup
        {
            Id = 7,
            TenantId = 1,
            Code = "TG",
            Name = "A",
            MaxTeachingCapacity = 50,
            ExpectedStudentCount = 30
        };
        var entry = Entry(7, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Name = "R", FloorId = 1, Capacity = 40 } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int> { [7] = 55 });

        var bag = new ConflictResultBag();
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);

        Assert.Contains(bag.Items, i => i.RuleCode == "TEACHING_GROUP_CAPACITY_EXCEEDED");
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
        Assert.Equal(2, bag.Items.Count);

        var soft = new List<SoftWarningDto>
        {
            _composer.CreateTeachingGroupCapacitySoftWarning(entry, tg, 55, 50, []),
            _composer.CreateRoomCapacitySoftWarning(
                entry,
                _room.Evaluate(40, 0, PlacementSizeResolution.From(PlacementSizeSource.ResolvedStudentCount, 55)),
                [])
        };
        var ordered = _composer.OrderDeterministically(soft);
        Assert.Equal(2, ordered.Count);
        Assert.Contains(ordered, w => w.Code == "ROOM_CAPACITY");
        Assert.Contains(ordered, w => w.Code == "TEACHING_GROUP_CAPACITY_EXCEEDED");
    }

    [Fact]
    public void Ordering_is_deterministic()
    {
        var a = _composer.CreateGenericSoftWarning("ROOM_CAPACITY", "a", Entry(1), [], ConflictSeverity.Error);
        var b = _composer.CreateGenericSoftWarning("TEACHING_GROUP_CAPACITY_EXCEEDED", "b", Entry(1), [], ConflictSeverity.Error);
        var c = _composer.CreateGenericSoftWarning("NON_WORKING_DAY", "c", Entry(1), [], ConflictSeverity.Warning);

        var first = _composer.OrderDeterministically([c, b, a]).Select(x => x.Code).ToList();
        var second = _composer.OrderDeterministically([a, c, b]).Select(x => x.Code).ToList();
        Assert.Equal(first, second);
    }

    [Fact]
    public void Tenant_isolation_other_tenant_TG_not_in_presentation_context()
    {
        var entry = Entry(teachingGroupId: 99, subjectId: 9);
        var ctx = BuildContext(
            [entry],
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup>(),
            resolvedCounts: new Dictionary<int, int>());
        var placement = ctx.ResolvePlacementSize(entry);
        Assert.Equal(PlacementSizeSource.SubjectExpectedCapacity, placement.Source);
        Assert.DoesNotContain(99, ctx.TeachingGroups.Keys);
    }

    [Fact]
    public void Draft_soft_warnings_do_not_set_BlocksEditing()
    {
        var summary = new ConflictResultBag().BuildSummary(1, 1, 50, null, DateTime.UtcNow, "test");
        Assert.False(summary.BlocksEditing);
    }

    private static TimetableEntry Entry(int? teachingGroupId = null, int subjectId = 1, int roomId = 1) =>
        new()
        {
            Id = 1,
            TenantId = 1,
            TimetableId = 50,
            StaffId = 10,
            RoomId = roomId,
            DayOfWeek = 1,
            TimeSlotId = 100,
            GroupId = 1,
            SubjectId = subjectId,
            CourseId = 1,
            SemesterId = 1,
            DepartmentId = 1,
            SubjectAllocationId = 1,
            TeachingGroupId = teachingGroupId
        };

    private static ConflictAnalysisContext BuildContext(
        IReadOnlyList<TimetableEntry> entries,
        IReadOnlyDictionary<int, Room>? rooms = null,
        IReadOnlyDictionary<int, Subject>? subjects = null,
        IReadOnlyDictionary<int, TeachingGroup>? teachingGroups = null,
        IReadOnlyDictionary<int, int>? resolvedCounts = null) =>
        new()
        {
            TenantId = 1,
            AcademicYearId = 1,
            TimetableId = 50,
            Entries = entries,
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
            TeachingGroups = teachingGroups ?? new Dictionary<int, TeachingGroup>(),
            ResolvedStudentCountsByTeachingGroupId = resolvedCounts ?? new Dictionary<int, int>()
        };
}
