using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 3 — PlacementSize matrix, TG capacity matrix, room integration, tenant isolation.
/// </summary>
public sealed class AiSchedCapPrompt3PlacementSizeAndCapacityTests
{
    private readonly IPlacementSizeResolver _resolver = PlacementSizeResolver.Instance;

    public static TheoryData<int?, int?, int?, int?, PlacementSizeSource> PlacementSizeMatrix => new()
    {
        // Assigned TG matrices (Prompt 3 §14)
        { 0, 30, 40, 0, PlacementSizeSource.ResolvedStudentCount },
        { 25, 30, 40, 25, PlacementSizeSource.ResolvedStudentCount },
        { 55, 30, 40, 55, PlacementSizeSource.ResolvedStudentCount },
        { null, 30, 40, 30, PlacementSizeSource.ExpectedStudentCount },
        { null, 0, 40, 40, PlacementSizeSource.SubjectExpectedCapacity },
        { null, null, 40, 40, PlacementSizeSource.SubjectExpectedCapacity },
        { null, null, null, null, PlacementSizeSource.Unset },
    };

    [Theory]
    [MemberData(nameof(PlacementSizeMatrix))]
    public void PlacementSize_resolution_matrix(
        int? resolved,
        int? expected,
        int? subjectCap,
        int? expectedValue,
        PlacementSizeSource expectedSource)
    {
        var result = _resolver.Resolve(resolved, expected, subjectCap);
        Assert.Equal(expectedSource, result.Source);
        if (expectedSource == PlacementSizeSource.Unset)
        {
            Assert.False(result.HasValue);
        }
        else
        {
            Assert.True(result.HasValue);
            Assert.Equal(expectedValue, result.Value);
        }
    }

    [Fact]
    public void Resolved_zero_does_not_fall_through_to_Expected()
    {
        var result = _resolver.Resolve(resolvedStudentCount: 0, expectedStudentCount: 30, subjectExpectedCapacity: 40);
        Assert.Equal(PlacementSizeSource.ResolvedStudentCount, result.Source);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Expected_zero_and_negative_are_unset()
    {
        Assert.Equal(PlacementSizeSource.SubjectExpectedCapacity,
            _resolver.Resolve(null, 0, 40).Source);
        Assert.Equal(PlacementSizeSource.SubjectExpectedCapacity,
            _resolver.Resolve(null, -1, 40).Source);
        Assert.Equal(PlacementSizeSource.Unset,
            _resolver.Resolve(null, 0, 0).Source);
    }

    [Theory]
    [InlineData(null, 40, false)] // no max
    [InlineData(50, 40, false)]   // within
    [InlineData(50, 50, false)]   // exactly at
    [InlineData(50, 51, true)]    // exceeds
    [InlineData(50, 0, false)]    // zero students
    public async Task TeachingGroupCapacity_matrix(int? maxCap, int resolved, bool expectConflict)
    {
        var tg = new TeachingGroup
        {
            Id = 7,
            TenantId = 1,
            Code = "TG-A",
            Name = "A",
            MaxTeachingCapacity = maxCap,
            ExpectedStudentCount = 30
        };
        var entry = Entry(1, teachingGroupId: 7, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Capacity = 100, FloorId = 1, Name = "R" } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int> { [7] = resolved });

        var bag = new ConflictResultBag();
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);

        if (expectConflict)
            Assert.Contains(bag.Items, i => i.RuleCode == TeachingGroupCapacityExceededRule.Code);
        else
            Assert.DoesNotContain(bag.Items, i => i.RuleCode == TeachingGroupCapacityExceededRule.Code);
    }

    [Fact]
    public async Task TeachingGroupCapacity_skips_when_Max_is_zero_invalid()
    {
        var tg = new TeachingGroup { Id = 7, TenantId = 1, Code = "TG", Name = "A", MaxTeachingCapacity = 0 };
        var ctx = BuildContext(
            [Entry(1, teachingGroupId: 7)],
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int> { [7] = 99 });
        var bag = new ConflictResultBag();
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.Empty(bag.Items);
    }

    [Fact]
    public async Task TeachingGroupCapacity_skips_when_Resolved_unavailable()
    {
        var tg = new TeachingGroup { Id = 7, TenantId = 1, Code = "TG", Name = "A", MaxTeachingCapacity = 10 };
        var ctx = BuildContext(
            [Entry(1, teachingGroupId: 7)],
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int>()); // missing key = unavailable
        var bag = new ConflictResultBag();
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.Empty(bag.Items);
    }

    [Fact]
    public async Task RoomCapacity_uses_PlacementSize_Resolved_including_zero()
    {
        var tg = new TeachingGroup { Id = 7, TenantId = 1, Code = "TG", Name = "A", ExpectedStudentCount = 30 };
        var entry = Entry(1, teachingGroupId: 7, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Capacity = 10, FloorId = 1, Name = "R" } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int> { [7] = 0 });

        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        // PlacementSize=0 ≤ room 10 → no ROOM_CAPACITY
        Assert.DoesNotContain(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
    }

    [Fact]
    public async Task RoomCapacity_and_TeachingGroupCapacity_fire_independently()
    {
        var tg = new TeachingGroup
        {
            Id = 7,
            TenantId = 1,
            Code = "TG",
            Name = "A",
            ExpectedStudentCount = 30,
            MaxTeachingCapacity = 50
        };
        var entry = Entry(1, teachingGroupId: 7, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Capacity = 40, FloorId = 1, Name = "R" } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = tg },
            resolvedCounts: new Dictionary<int, int> { [7] = 55 });

        var bag = new ConflictResultBag();
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);

        Assert.Contains(bag.Items, i => i.RuleCode == TeachingGroupCapacityExceededRule.Code);
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
    }

    [Fact]
    public async Task Legacy_null_TeachingGroupId_uses_Subject_ExpectedCapacity_for_room()
    {
        var entry = Entry(1, teachingGroupId: null, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Capacity = 25, FloorId = 1, Name = "R" } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } });

        Assert.Equal(PlacementSizeSource.SubjectExpectedCapacity, ctx.ResolvePlacementSize(entry).Source);
        Assert.Equal(40, ctx.ResolvePlacementSize(entry).Value);

        var bag = new ConflictResultBag();
        await new RoomCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.Contains(bag.Items, i => i.RuleCode == "ROOM_CAPACITY");
        await new TeachingGroupCapacityExceededRule().AnalyzeAsync(ctx, bag);
        Assert.DoesNotContain(bag.Items, i => i.RuleCode == TeachingGroupCapacityExceededRule.Code);
    }

    [Fact]
    public void Tenant_isolation_other_tenant_TeachingGroup_does_not_influence_PlacementSize()
    {
        // ConflictAnalyzer only loads TeachingGroups where TenantId matches analysis tenant.
        // Simulate: entry references tgId=99 but context has no TG (cross-tenant filtered out).
        var entry = Entry(1, teachingGroupId: 99, subjectId: 9, roomId: 5);
        var ctx = BuildContext(
            [entry],
            rooms: new Dictionary<int, Room> { [5] = new Room { Id = 5, Capacity = 100, FloorId = 1, Name = "R" } },
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup>(), // other tenant TG not present
            resolvedCounts: new Dictionary<int, int>());

        var placement = ctx.ResolvePlacementSize(entry);
        Assert.Equal(PlacementSizeSource.SubjectExpectedCapacity, placement.Source);
        Assert.Equal(40, placement.Value);
        Assert.DoesNotContain(99, ctx.TeachingGroups.Keys);
    }

    [Fact]
    public void Assigned_TeachingGroup_only_is_used_never_another_TG_on_same_allocation()
    {
        var assigned = new TeachingGroup
        {
            Id = 7,
            TenantId = 1,
            Code = "ASSIGNED",
            Name = "Assigned",
            ExpectedStudentCount = 12,
            SubjectAllocationId = 1
        };
        var other = new TeachingGroup
        {
            Id = 8,
            TenantId = 1,
            Code = "OTHER",
            Name = "Other",
            ExpectedStudentCount = 99,
            SubjectAllocationId = 1
        };
        var entry = Entry(1, teachingGroupId: 7, subjectId: 9);
        var ctx = BuildContext(
            [entry],
            subjects: new Dictionary<int, Subject> { [9] = new Subject { Id = 9, ExpectedCapacity = 40 } },
            teachingGroups: new Dictionary<int, TeachingGroup> { [7] = assigned, [8] = other },
            resolvedCounts: new Dictionary<int, int> { [7] = 12, [8] = 99 });

        var placement = ctx.ResolvePlacementSize(entry);
        Assert.Equal(12, placement.Value);
        Assert.Equal(PlacementSizeSource.ResolvedStudentCount, placement.Source);
    }

    private static TimetableEntry Entry(
        int id,
        int? teachingGroupId = null,
        int subjectId = 1,
        int roomId = 1) =>
        new()
        {
            Id = id,
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
            StaffNames = new Dictionary<int, string> { [10] = "Dr Test" },
            RoomFeatureAssignments = [],
            DeliveryTypes = new Dictionary<int, SubjectDeliveryType>(),
            TeachingGroups = teachingGroups ?? new Dictionary<int, TeachingGroup>(),
            ResolvedStudentCountsByTeachingGroupId = resolvedCounts ?? new Dictionary<int, int>(),
            PlacementSizeResolver = PlacementSizeResolver.Instance
        };
}
