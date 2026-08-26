using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

/// <summary>Immutable snapshot of timetable + related masters for rule evaluation.</summary>
public sealed class ConflictAnalysisContext
{
    public required int TenantId { get; init; }
    public required int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? TimetableId { get; init; }
    /// <summary>Configurable thresholds (DB overrides appsettings). Detection rules unchanged.</summary>
    public ConflictRuleThresholds Thresholds { get; init; } = ConflictRuleThresholds.Defaults;
    public required IReadOnlyList<TimetableEntry> Entries { get; init; }
    public required IReadOnlyDictionary<int, TimeSlot> TimeSlots { get; init; }
    public required IReadOnlyDictionary<int, Room> Rooms { get; init; }
    public required IReadOnlyDictionary<int, Floor> Floors { get; init; }
    public required IReadOnlyDictionary<int, Building> Buildings { get; init; }
    public required IReadOnlyDictionary<int, SubjectAllocation> Allocations { get; init; }
    public required IReadOnlyDictionary<int, Subject> Subjects { get; init; }
    public required IReadOnlyList<FacultyAvailability> FacultyAvailabilities { get; init; }
    public required IReadOnlyList<RoomAvailability> RoomAvailabilities { get; init; }
    public required IReadOnlyList<FacultyTeachingPreference> FacultyPreferences { get; init; }
    public required IReadOnlyDictionary<byte, WorkingDay> WorkingDays { get; init; }
    public required IReadOnlyList<Holiday> Holidays { get; init; }
    public required IReadOnlyDictionary<int, Campus> Campuses { get; init; }
    public required AcademicYear? AcademicYear { get; init; }
    public required IReadOnlyDictionary<int, string> StaffNames { get; init; }
    public required IReadOnlyList<RoomFeatureAssignment> RoomFeatureAssignments { get; init; }
    public required IReadOnlyDictionary<int, SubjectDeliveryType> DeliveryTypes { get; init; }

    /// <summary>AI-SCHED-CAP Prompt 3 — TeachingGroups referenced by entries (tenant-scoped load).</summary>
    public IReadOnlyDictionary<int, TeachingGroup> TeachingGroups { get; init; } =
        new Dictionary<int, TeachingGroup>();

    /// <summary>
    /// AI-SCHED-CAP Prompt 3 — Successfully resolved membership counts keyed by TeachingGroupId.
    /// Absence of a key means ResolvedStudentCount is unavailable (not zero).
    /// </summary>
    public IReadOnlyDictionary<int, int> ResolvedStudentCountsByTeachingGroupId { get; init; } =
        new Dictionary<int, int>();

    public IPlacementSizeResolver PlacementSizeResolver { get; init; } = Capacity.PlacementSizeResolver.Instance;

    /// <summary>AI-SCHED-CAP Prompt 3A — shared room-fit evaluator (margin-aware).</summary>
    public IRoomCapacityEvaluator RoomCapacityEvaluator { get; init; } = Capacity.RoomCapacityEvaluator.Instance;

    public PlacementSizeResolution ResolvePlacementSize(TimetableEntry entry)
    {
        int? resolved = null;
        int? expected = null;
        if (entry.TeachingGroupId is int tgId)
        {
            if (ResolvedStudentCountsByTeachingGroupId.TryGetValue(tgId, out var count))
                resolved = count;
            if (TeachingGroups.TryGetValue(tgId, out var tg))
                expected = tg.ExpectedStudentCount;
        }

        int? subjectCap = Subjects.TryGetValue(entry.SubjectId, out var subject)
            ? subject.ExpectedCapacity
            : null;

        return PlacementSizeResolver.Resolve(resolved, expected, subjectCap);
    }

    public string Nav(TimetableEntry entry) =>
        $"/setup/scheduling/timetables/{entry.TimetableId}?entryId={entry.Id}&day={entry.DayOfWeek}&slot={entry.TimeSlotId}";

    public ConflictResult Create(
        IConflictRule rule,
        ConflictSeverity severity,
        string description,
        string why,
        string suggestion,
        TimetableEntry entry,
        int? relatedEntryId = null)
    {
        return new ConflictResult
        {
            RuleCode = rule.RuleCode,
            RuleName = rule.RuleName,
            Category = rule.Category,
            Severity = severity,
            Description = description,
            WhyOccurred = why,
            Recommendation = new ConflictRecommendation
            {
                SuggestedResolution = suggestion,
                NavigationPath = Nav(entry),
                TimetableId = entry.TimetableId,
                TimetableEntryId = entry.Id,
                DayOfWeek = entry.DayOfWeek,
                TimeSlotId = entry.TimeSlotId
            },
            TimetableId = entry.TimetableId,
            TimetableEntryId = entry.Id,
            RelatedEntryId = relatedEntryId,
            DayOfWeek = entry.DayOfWeek,
            TimeSlotId = entry.TimeSlotId,
            StaffId = entry.StaffId,
            RoomId = entry.RoomId,
            DepartmentId = entry.DepartmentId,
            CourseId = entry.CourseId,
            GroupId = entry.GroupId,
            SemesterId = entry.SemesterId,
            SubjectId = entry.SubjectId
        };
    }

    public int? CampusIdForRoom(int roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return null;
        if (!Floors.TryGetValue(room.FloorId, out var floor)) return null;
        if (!Buildings.TryGetValue(floor.BuildingId, out var building)) return null;
        return building.CampusId;
    }

    public static RoomType[] LabRoomTypes { get; } =
    [
        RoomType.ComputerLab,
        RoomType.ScienceLab,
        RoomType.CommerceLab
    ];
}
