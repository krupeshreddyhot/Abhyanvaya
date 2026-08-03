using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class TimetableDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public int AcademicYearId { get; init; }
    public string? AcademicYearName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? TimeSlotSetId { get; init; }
    public string? TimeSlotSetName { get; init; }
    public int? ScheduleVersionId { get; init; }
    public TimetableStatus Status { get; init; }
    public string? Notes { get; init; }
    public int EntryCount { get; init; }
    public bool IsFrozen { get; init; }
    public DateTime? FrozenDate { get; init; }
    public int? FrozenBy { get; init; }
    public string? FreezeReason { get; init; }
    public DateTime? UnlockDate { get; init; }
    public int? UnlockedBy { get; init; }
    public string? UnlockReason { get; init; }
    public int? ArchiveReasonId { get; init; }
    public string? ArchiveReasonName { get; init; }
    public string? ArchiveComments { get; init; }
    public int? ArchivedBy { get; init; }
    public DateTime? ArchivedDate { get; init; }
    public int? ReferenceVersionId { get; init; }
}

public sealed class TimetableEntryDto
{
    public int Id { get; init; }
    public int TimetableId { get; init; }
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public string? TimeSlotName { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public int SubjectAllocationId { get; init; }
    public int StaffId { get; init; }
    public string? StaffName { get; init; }
    public int RoomId { get; init; }
    public string? RoomName { get; init; }
    public int DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int CourseId { get; init; }
    public string? CourseName { get; init; }
    public int GroupId { get; init; }
    public string? GroupName { get; init; }
    public int SemesterId { get; init; }
    public string? SemesterName { get; init; }
    public int SubjectId { get; init; }
    public string? SubjectName { get; init; }
    public string? Remarks { get; init; }
}

public sealed class CreateTimetableRequest
{
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? TimeSlotSetId { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateTimetableRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? TimeSlotSetId { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateTimetableEntryRequest
{
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public int SubjectAllocationId { get; init; }
    public int? RoomId { get; init; }
    public string? Remarks { get; init; }
}

public sealed class UpdateTimetableEntryRequest
{
    public int Id { get; init; }
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public int SubjectAllocationId { get; init; }
    public int? RoomId { get; init; }
    public string? Remarks { get; init; }
}

public sealed class UpsertTimetableEntryRequest
{
    public int? Id { get; init; }
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public int SubjectAllocationId { get; init; }
    public int? RoomId { get; init; }
    public string? Remarks { get; init; }
}

public sealed class BulkPasteEntriesRequest
{
    public IReadOnlyList<UpsertTimetableEntryRequest> Entries { get; init; } = [];
}

public sealed class MoveTimetableEntryRequest
{
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public int? RoomId { get; init; }
}

public sealed class CopyTimetableEntryRequest
{
    public byte TargetDayOfWeek { get; init; }
    public int TargetTimeSlotId { get; init; }
    public int? RoomId { get; init; }
}

public sealed class TimetableGridDto
{
    public TimetableDto Timetable { get; init; } = null!;
    public IReadOnlyList<TimetableEntryDto> Entries { get; init; } = [];
    public IReadOnlyList<TimeSlotDto> TimeSlots { get; init; } = [];
}

public sealed class TimetableProjectionDto
{
    public TimetableDto Timetable { get; init; } = null!;
    public IReadOnlyList<TimetableEntryDto> Entries { get; init; } = [];
}

public sealed class TimetableDashboardDto
{
    public int DraftTimetableCount { get; init; }
    public int LockedCount { get; init; }
    public int ScheduledPeriodCount { get; init; }
    public int DepartmentsWithTimetable { get; init; }
    public int FacultyScheduledCount { get; init; }
    public int RoomsScheduledCount { get; init; }
    public IReadOnlyList<NamedCountDto> DailyDistribution { get; init; } = [];
    public IReadOnlyList<NamedCountDto> FacultyLoad { get; init; } = [];
    public IReadOnlyList<NamedCountDto> RoomUsage { get; init; } = [];
}
