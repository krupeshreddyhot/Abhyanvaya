namespace Abhyanvaya.Application.DTOs.Dashboards;

// --- Widget framework (AI31.6.6) ---

public sealed class DashboardWidgetDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Kind { get; init; } = "Kpi"; // Kpi | Chart | Timeline | Notification | Action | Status
    public string Category { get; init; } = "";
    public decimal? Value { get; init; }
    public string? DisplayValue { get; init; }
    /// <summary>Sessions | Items | Issues | Suggestions | % | min — AI31.7.5 rich KPI unit.</summary>
    public string? Unit { get; init; }
    /// <summary>Green | Yellow | Orange | Red | Info — severity / health color token.</summary>
    public string? Status { get; init; }
    /// <summary>Healthy | Warning | Critical | Information — business-facing status label (AI31.7.5).</summary>
    public string? StatusLabel { get; init; }
    public string? Path { get; init; }
    public string? ReportPath { get; init; }
    public string? Tooltip { get; init; }
    public DateTime? LastUpdatedUtc { get; init; }
    /// <summary>up | down | flat — when historical comparison exists.</summary>
    public string? Trend { get; init; }
    /// <summary>Human comparison text, e.g. "+4 since yesterday".</summary>
    public string? Comparison { get; init; }
    public string? SuggestedAction { get; init; }
    public string? EstimatedImpact { get; init; }
    public string? ActionLabel { get; init; }
    /// <summary>Visual group within a section (e.g. Running Sessions, Recognition).</summary>
    public string? Group { get; init; }
    /// <summary>AI31.8 — short contextual explanation under the primary value.</summary>
    public string? Explanation { get; init; }
    public bool Pinned { get; init; }
    public string? RequiredPermission { get; init; }
    public bool Configurable { get; init; } = true;
    public bool Visible { get; init; } = true;
    public int SortOrder { get; init; }
}

// --- Faculty Command Center (AI31.6.1–4) ---

public sealed class FacultyCommandCenterDto
{
    public DateOnly Date { get; init; }
    public string Mode { get; init; } = "Legacy";
    public bool HasTimetable { get; init; }
    public string Message { get; init; } = "";
    public FacultyCommandClassCardDto? CurrentClass { get; init; }
    public FacultyCommandClassCardDto? NextClass { get; init; }
    public IReadOnlyList<FacultyCommandClassCardDto> TodaysClasses { get; init; } = [];
    public int RemainingClasses { get; init; }
    public int TodaysStudents { get; init; }
    public int AttendancePending { get; init; }
    public int RecoveryQueue { get; init; }
    public FacultyKpiBundleDto Kpis { get; init; } = new();
    public FacultyInsightsPanelDto Insights { get; init; } = new();
    public IReadOnlyList<FacultyActivityEventDto> ActivityPreview { get; init; } = [];
    public IReadOnlyList<DashboardWidgetDto> Widgets { get; init; } = [];
    public IReadOnlyList<FacultyCommandQuickActionDto> QuickActions { get; init; } = [];
    public DashboardPreferenceDto Preferences { get; init; } = new();
    public DateTime GeneratedUtc { get; init; }
    public bool DoesNotModifyAttendanceApis => true;
    public bool DoesNotModifyAttendanceSessionResolver => true;
}

public sealed class FacultyCommandClassCardDto
{
    public string Status { get; init; } = "Upcoming";
    public string? SubjectName { get; init; }
    public string? RoomName { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string AttendanceStatus { get; init; } = "NotStarted";
    public int? StudentCount { get; init; }
    public Guid? AttendanceSessionId { get; init; }
    public string? TakeAttendancePath { get; init; }
}

public sealed class FacultyCommandQuickActionDto
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Primary { get; init; }
}

public sealed class FacultyKpiBundleDto
{
    public int TodaysClasses { get; init; }
    public int CompletedClasses { get; init; }
    public int RemainingClasses { get; init; }
    public int TodaysStudents { get; init; }
    public int AttendanceCompleted { get; init; }
    public int PendingAttendance { get; init; }
    public int RecoverySessions { get; init; }
    public int RecognitionReviews { get; init; }
    public double? AverageCompletionMinutes { get; init; }
    public double? AttendancePercent { get; init; }
    public bool ReusesExistingAnalytics => true;
}

public sealed class FacultyInsightsPanelDto
{
    public IReadOnlyList<InsightItemDto> Items { get; init; } = [];
    public bool NeverGeneratesAiContent => true;
    public bool ComposesExistingData => true;
    public bool SupportsSignalR => true;
}

public sealed class InsightItemDto
{
    public string Code { get; init; } = "";
    public string Kind { get; init; } = "Info"; // Trend | Alert | Reminder | Review | Schedule
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Path { get; init; }
    public string Severity { get; init; } = "Info"; // Info | Warning | Critical
}

public sealed class FacultyActivityTimelineDto
{
    public string Range { get; init; } = "Today"; // Today | Week | Month
    public IReadOnlyList<FacultyActivityEventDto> Events { get; init; } = [];
    public bool NewestFirst => true;
    public bool ReusesAuditHistory => true;
}

public sealed class FacultyActivityEventDto
{
    public string EventId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime OccurredUtc { get; init; }
    public string? Path { get; init; }
}

// --- Admin Enterprise Operations (AI31.6.5–6, 8) ---

public sealed class AdminOperationsDashboardDto
{
    public AdminSectionDto Academic { get; init; } = new();
    public AdminSectionDto Attendance { get; init; } = new();
    public AdminSectionDto Scheduling { get; init; } = new();
    public AdminSectionDto Faculty { get; init; } = new();
    public AdminSectionDto Student { get; init; } = new();
    public AdminSectionDto Recovery { get; init; } = new();
    public AdminSectionDto AiServices { get; init; } = new();
    public AdminSectionDto PlatformHealth { get; init; } = new();
    public IReadOnlyList<DashboardWidgetDto> Widgets { get; init; } = [];
    public IReadOnlyList<OperationalChartSeriesDto> Charts { get; init; } = [];
    public DashboardPreferenceDto Preferences { get; init; } = new();
    public DateTime GeneratedUtc { get; init; }
    public bool CompositionOnly => true;
}

public sealed class AdminSectionDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<DashboardWidgetDto> Cards { get; init; } = [];
    public IReadOnlyList<QuickLinkDto> QuickLinks { get; init; } = [];
}

public sealed class QuickLinkDto
{
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
}

public sealed class OperationalChartSeriesDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<ChartPointDto> Points { get; init; } = [];
}

public sealed class ChartPointDto
{
    public string Label { get; init; } = "";
    public decimal Value { get; init; }
}

public sealed class EnterpriseOperationalAnalyticsDto
{
    public IReadOnlyList<OperationalChartSeriesDto> Series { get; init; } = [];
    public IReadOnlyList<DepartmentComparisonDto> DepartmentComparison { get; init; } = [];
    public bool SupportsExcelExport => true;
    public bool SupportsPdfExport => true;
    public bool ReusesExistingAnalytics => true;
    public DateTime GeneratedUtc { get; init; }
}

public sealed class DepartmentComparisonDto
{
    public string DepartmentName { get; init; } = "";
    public int PendingSessions { get; init; }
    public int Completed { get; init; }
    public double? AverageCompletionMinutes { get; init; }
}

// --- Notifications (AI31.6.7) ---

public sealed class EnterpriseNotificationCenterDto
{
    public IReadOnlyList<EnterpriseNotificationItemDto> Items { get; init; } = [];
    public int UnreadCount { get; init; }
    public bool UsesSignalR => true;
    public bool NoPolling => true;
}

public sealed class EnterpriseNotificationItemDto
{
    public string NotificationId { get; init; } = "";
    public string Source { get; init; } = ""; // Scheduling | Attendance | Recovery | Optimization | Governance | System
    public string Category { get; init; } = "";
    public string Priority { get; init; } = "Normal"; // Low | Normal | High | Critical
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime OccurredUtc { get; init; }
    public bool IsUnread { get; init; } = true;
    public bool IsPinned { get; init; }
    public bool IsDismissed { get; init; }
    public bool IsArchived { get; init; }
    public string? Path { get; init; }
}

public sealed class NotificationStateUpdateRequest
{
    public string NotificationId { get; init; } = "";
    public bool? IsRead { get; init; }
    public bool? IsPinned { get; init; }
    public bool? IsDismissed { get; init; }
    public bool? IsArchived { get; init; }
}

// --- Preferences (AI31.6.9) ---

public sealed class DashboardPreferenceDto
{
    public int Id { get; init; }
    public string RoleScope { get; init; } = "Faculty";
    public string DefaultLandingPage { get; init; } = "command-center";
    public bool CompactMode { get; init; }
    public IReadOnlyList<string> HiddenWidgets { get; init; } = [];
    public IReadOnlyList<string> WidgetOrder { get; init; } = [];
    public IReadOnlyList<string> PinnedWidgets { get; init; } = [];
    public DashboardFilterRequest? Filters { get; init; }
    public int RefreshIntervalSeconds { get; init; } = 60;
    public bool HighContrast { get; init; }
    public bool DatabasePersisted => true;
}

public sealed class UpdateDashboardPreferenceRequest
{
    public string? RoleScope { get; set; }
    public string? DefaultLandingPage { get; set; }
    public bool? CompactMode { get; set; }
    public IReadOnlyList<string>? HiddenWidgets { get; set; }
    public IReadOnlyList<string>? WidgetOrder { get; set; }
    public IReadOnlyList<string>? PinnedWidgets { get; set; }
    public DashboardFilterRequest? Filters { get; set; }
    public int? RefreshIntervalSeconds { get; set; }
    public bool? HighContrast { get; set; }
    public bool? RestoreDefaults { get; set; }
}

// --- Health Center (AI31.6.10) ---

public sealed class EnterpriseHealthCenterDto
{
    public string OverallStatus { get; init; } = "Green"; // Green | Yellow | Red
    public IReadOnlyList<HealthTrafficLightDto> Components { get; init; } = [];
    public bool ReadOnly => true;
    public bool ReusesExistingHealthServices => true;
    public DateTime GeneratedUtc { get; init; }
}

public sealed class HealthTrafficLightDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "Green";
    public string Message { get; init; } = "";
}
