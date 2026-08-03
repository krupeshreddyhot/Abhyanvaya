using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class CompareScheduleVersionsRequest
{
    public int LeftVersionId { get; init; }
    public int RightVersionId { get; init; }
    public int? DepartmentId { get; init; }
    public string? Search { get; init; }
    public VersionDifferenceKind? KindFilter { get; init; }
    public VersionDifferenceCategory? CategoryFilter { get; init; }
}

public sealed class ComparisonSummaryDto
{
    public int Added { get; init; }
    public int Modified { get; init; }
    public int Removed { get; init; }
    public int FacultyChanges { get; init; }
    public int RoomChanges { get; init; }
    public int SubjectChanges { get; init; }
    public int PeriodChanges { get; init; }
    public int TimeSlotChanges { get; init; }
}

public sealed class VersionDifferenceDto
{
    public VersionDifferenceKind Kind { get; init; }
    public VersionDifferenceCategory Category { get; init; }
    public string Summary { get; init; } = null!;
    public int? LeftEntryId { get; init; }
    public int? RightEntryId { get; init; }
    public int? LeftTimetableId { get; init; }
    public int? RightTimetableId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public int? SubjectId { get; init; }
    public string? SubjectName { get; init; }
    public int? StaffId { get; init; }
    public string? StaffName { get; init; }
    public int? RoomId { get; init; }
    public string? RoomName { get; init; }
    public string? LeftValue { get; init; }
    public string? RightValue { get; init; }
    public IReadOnlyList<string> ChangedFields { get; init; } = [];
}

public sealed class VersionComparisonDto
{
    public int LeftVersionId { get; init; }
    public string LeftVersionName { get; init; } = null!;
    public ScheduleVersionStatus LeftStatus { get; init; }
    public int RightVersionId { get; init; }
    public string RightVersionName { get; init; } = null!;
    public ScheduleVersionStatus RightStatus { get; init; }
    public ComparisonSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<VersionDifferenceDto> Differences { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<VersionDifferenceDto>> Grouped { get; init; }
        = new Dictionary<string, IReadOnlyList<VersionDifferenceDto>>();
}
