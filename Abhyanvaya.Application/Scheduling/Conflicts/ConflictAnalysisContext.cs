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
