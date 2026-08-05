namespace Abhyanvaya.Application.DTOs.Dashboards;

/// <summary>AI31.8 — global academic filters for Command Center excellence (composition-only).</summary>
public sealed class DashboardFilterRequest
{
    public int? AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public int? CampusId { get; init; }
    public int? BuildingId { get; init; }
    public int? RoomId { get; init; }
}

public sealed class EnterpriseDashboardExcellenceDto
{
    public string Title { get; init; } = "Enterprise Operations Command Center";
    public ExecutiveSummaryDto ExecutiveSummary { get; init; } = new();
    public DashboardFilterStateDto Filters { get; init; } = new();
    public EnterpriseOperationsCommandCenterDto CommandCenter { get; init; } = new();
    public AcademicTimelineDto AcademicTimeline { get; init; } = new();
    public DashboardVisualizationsDto Visualizations { get; init; } = new();
    public IReadOnlyList<WidgetHelpDto> WidgetHelp { get; init; } = [];
    public IReadOnlyList<ActionGroupDto> ActionGroups { get; init; } = [];
    public DashboardPreferenceDto Preferences { get; init; } = new();
    public int RefreshIntervalSeconds { get; init; } = 60;
    public DateTime GeneratedUtc { get; init; }
    public DateTime? NextRefreshUtc { get; init; }
    public bool CompositionOnly => true;
    public bool DoesNotModifyAttendanceApis => true;
    public bool DoesNotModifyAttendanceSessionResolver => true;
    public bool SupportsLegacyAndTimetableAttendance => true;
    public bool UsesSignalRWhenAvailable => true;
}

public sealed class ExecutiveSummaryDto
{
    public string? AcademicYear { get; init; }
    public string? CollegeName { get; init; }
    public string? CurrentSemester { get; init; }
    public DateOnly TodaysDate { get; init; }
    public string CurrentWorkingDay { get; init; } = "";
    public int? TotalScheduledClassesToday { get; init; }
    public string? OverallAttendanceToday { get; init; }
    public int? FacultyAvailableToday { get; init; }
    public int? ActiveStudents { get; init; }
    public int CriticalAlerts { get; init; }
    public string PlatformHealth { get; init; } = "Healthy";
    public string? PlatformHealthStatus { get; init; }
    public IReadOnlyList<DashboardWidgetDto> Cards { get; init; } = [];
}

public sealed class DashboardFilterStateDto
{
    public int? AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public int? CampusId { get; init; }
    public int? BuildingId { get; init; }
    public int? RoomId { get; init; }
    public IReadOnlyList<NamedOptionDto> AcademicYears { get; init; } = [];
    public IReadOnlyList<NamedOptionDto> Departments { get; init; } = [];
    public IReadOnlyList<NamedOptionDto> Courses { get; init; } = [];
    public IReadOnlyList<NamedOptionDto> Campuses { get; init; } = [];
    public IReadOnlyList<NamedOptionDto> Buildings { get; init; } = [];
    public IReadOnlyList<NamedOptionDto> Rooms { get; init; } = [];
}

public sealed class NamedOptionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public sealed class AcademicTimelineDto
{
    public string? CurrentPeriodLabel { get; init; }
    public TimeSpan CurrentTime { get; init; }
    public IReadOnlyList<AcademicTimelineItemDto> Items { get; init; } = [];
    public bool ReadOnly => true;
    public bool ReusesTimetableService => true;
}

public sealed class AcademicTimelineItemDto
{
    public string Kind { get; init; } = "Period"; // Period | Break | Lunch
    public string Label { get; init; } = "";
    public string Status { get; init; } = "Upcoming"; // Current | Upcoming | Completed | Break
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public int? FacultyOccupancy { get; init; }
    public int? RoomOccupancy { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class DashboardVisualizationsDto
{
    public OperationalChartSeriesDto? AttendanceHeatmap { get; init; }
    public OperationalChartSeriesDto? DepartmentHeatmap { get; init; }
    public OperationalChartSeriesDto? FacultyWorkloadHeatmap { get; init; }
    public OperationalChartSeriesDto? RoomUtilizationHeatmap { get; init; }
    public OperationalChartSeriesDto? WeeklyAttendanceTrend { get; init; }
    public OperationalChartSeriesDto? SchedulingCompletion { get; init; }
    public OperationalChartSeriesDto? ConflictTrend { get; init; }
    public bool ReadOnly => true;
}

public sealed class WidgetHelpDto
{
    public string WidgetCode { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string HowCalculated { get; init; } = "";
    public string UpdateFrequency { get; init; } = "On refresh / SignalR";
    public IReadOnlyList<string> RelatedModules { get; init; } = [];
    public IReadOnlyList<QuickLinkDto> NavigationLinks { get; init; } = [];
}

public sealed class ActionGroupDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<CommandCenterQuickActionDto> Actions { get; init; } = [];
}

public sealed class DashboardExportRequest
{
    public string Format { get; init; } = "excel"; // excel | pdf | csv | snapshot
    public DashboardFilterRequest? Filters { get; init; }
}

public sealed class DashboardExportResultDto
{
    public string FileName { get; init; } = "dashboard-export.xlsx";
    public string ContentType { get; init; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public byte[] Content { get; init; } = [];
}
