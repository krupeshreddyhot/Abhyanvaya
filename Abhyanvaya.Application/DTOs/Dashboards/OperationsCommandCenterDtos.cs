namespace Abhyanvaya.Application.DTOs.Dashboards;

/// <summary>AI31.7 / AI31.7.5 — Enterprise Operations Command Center (composition-only UX).</summary>
public sealed class EnterpriseOperationsCommandCenterDto
{
    public string Title { get; init; } = "Enterprise Operations Command Center";
    public string Subtitle { get; init; } =
        "College operations overview — Attention Required first, then live today, timetable, attendance, resources, and college system health.";
    public int RefreshIntervalSeconds { get; init; } = 60;
    public CommandCenterSectionDto AttentionRequired { get; init; } = new() { Code = "attention", Title = "Attention Required", Icon = "🚨" };
    public CommandCenterSectionDto TodaysOperations { get; init; } = new() { Code = "today", Title = "Today's Operations", Icon = "📅" };
    public CommandCenterSectionDto SchedulingOperations { get; init; } = new() { Code = "scheduling", Title = "Timetable Operations", Icon = "🗓" };
    public CommandCenterSectionDto AttendanceOperations { get; init; } = new() { Code = "attendance", Title = "Attendance Operations", Icon = "📝" };
    public CommandCenterSectionDto AcademicResources { get; init; } = new() { Code = "academic", Title = "Academic Resources", Icon = "🎓" };
    public CommandCenterSectionDto SystemHealth { get; init; } = new() { Code = "health", Title = "College System Health", Icon = "🖥" };
    public IReadOnlyList<CommandCenterActionBannerDto> ActionBanners { get; init; } = [];
    public IReadOnlyList<CommandCenterQuickActionDto> QuickActions { get; init; } = [];
    public DashboardPreferenceDto Preferences { get; init; } = new();
    public DateTime GeneratedUtc { get; init; }
    public bool CompositionOnly => true;
    public bool DoesNotModifyAttendanceApis => true;
    public bool DoesNotModifyAttendanceSessionResolver => true;
    public bool SupportsLegacyAndTimetableAttendance => true;
}

public sealed class CommandCenterSectionDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Icon { get; init; }
    public string? Subtitle { get; init; }
    public bool CollapsedByDefault { get; init; }
    public IReadOnlyList<DashboardWidgetDto> Cards { get; init; } = [];
    public IReadOnlyList<string> GroupOrder { get; init; } = [];
    public IReadOnlyList<QuickLinkDto> QuickLinks { get; init; } = [];
}

public sealed class CommandCenterQuickActionDto
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
    public string? Shortcut { get; init; }
    public string? RequiredPermission { get; init; }
    public bool Primary { get; init; }
}

/// <summary>AI31.7.5 — dismissible, permission-aware guided action banners.</summary>
public sealed class CommandCenterActionBannerDto
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Path { get; init; } = "";
    public string ActionLabel { get; init; } = "View Details";
    public string Severity { get; init; } = "Yellow";
    public string? RequiredPermission { get; init; }
}
