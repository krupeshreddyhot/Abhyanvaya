using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class ConflictRecommendationDto
{
    public string SuggestedResolution { get; init; } = "";
    public string? NavigationPath { get; init; }
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
}

public sealed class ConflictResultDto
{
    public string RuleCode { get; init; } = "";
    public string RuleName { get; init; } = "";
    public ConflictCategory Category { get; init; }
    public ConflictSeverity Severity { get; init; }
    public string Description { get; init; } = "";
    public string WhyOccurred { get; init; } = "";
    public ConflictRecommendationDto Recommendation { get; init; } = new();
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public int? RelatedEntryId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public int? StaffId { get; init; }
    public string? StaffName { get; init; }
    public int? RoomId { get; init; }
    public string? RoomName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? SubjectId { get; init; }
}

public sealed class ConflictSummaryDto
{
    public int RunId { get; init; }
    public int? TimetableId { get; init; }
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public string Status { get; init; } = "";
    public string TriggerSource { get; init; } = "";
    public int TotalConflicts { get; init; }
    public int FacultyCount { get; init; }
    public int RoomCount { get; init; }
    public int StudentCount { get; init; }
    public int CalendarCount { get; init; }
    public int CriticalCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InformationCount { get; init; }
    public bool BlocksEditing => false;
}

public sealed class ConflictAnalysisReportDto
{
    public ConflictSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ConflictResultDto> Conflicts { get; init; } = [];
}

public sealed class ConflictWorkspaceQuery
{
    public int? TimetableId { get; init; }
    public int? AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? StaffId { get; init; }
    public int? RoomId { get; init; }
    public ConflictCategory? Category { get; init; }
    public ConflictSeverity? Severity { get; init; }
    public string? Search { get; init; }
    public bool UseLatestRun { get; init; } = true;
    public bool Reanalyze { get; init; }
}

public sealed class ConflictWorkspaceDto
{
    public ConflictSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ConflictResultDto> Conflicts { get; init; } = [];
    public IReadOnlyDictionary<string, int> GroupedByRule { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> GroupedByCategory { get; init; } = new Dictionary<string, int>();
}

public sealed class HeatMapCellDto
{
    public byte DayOfWeek { get; init; }
    public int TimeSlotId { get; init; }
    public string? TimeSlotName { get; init; }
    public int LoadCount { get; init; }
    public string Colour { get; init; } = "Green";
    public ConflictSeverity MaxSeverity { get; init; } = ConflictSeverity.Information;
}

public sealed class HeatMapDto
{
    public string Kind { get; init; } = "";
    public int? EntityId { get; init; }
    public string? EntityName { get; init; }
    public int AcademicYearId { get; init; }
    public int? TimetableId { get; init; }
    public IReadOnlyList<HeatMapCellDto> Cells { get; init; } = [];
    public IReadOnlyDictionary<string, int> LoadDistribution { get; init; } = new Dictionary<string, int>();
}

public sealed class ConflictDashboardDto
{
    public ConflictSummaryDto LatestSummary { get; init; } = new();
    public int FacultyConflicts { get; init; }
    public int RoomConflicts { get; init; }
    public int StudentConflicts { get; init; }
    public int CalendarConflicts { get; init; }
    public string ValidationStatus { get; init; } = "Unknown";
    public IReadOnlyDictionary<string, int> ConflictCategories { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ConflictTrendPointDto> WarningTrends { get; init; } = [];
    public IReadOnlyList<HeatMapDto> HeatMaps { get; init; } = [];
}

public sealed class ConflictTrendPointDto
{
    public DateTime DateUtc { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public int CriticalCount { get; init; }
    public int TotalConflicts { get; init; }
}

public sealed class RunConflictDetectionRequest
{
    public int? TimetableId { get; init; }
    public int? AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public string TriggerSource { get; init; } = "Manual";
}

public sealed class AttendanceSessionResolutionDto
{
    public string Mode { get; init; } = "Legacy";
    public bool HasTimetable { get; init; }
    public string Message { get; init; } = "";
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? SubjectId { get; init; }
    public int? PeriodNumber { get; init; }
    public int? TimeSlotId { get; init; }
    public int? RoomId { get; init; }
    public string? SubjectName { get; init; }
    public string? RoomName { get; init; }
    public DateOnly? AttendanceDate { get; init; }
}
