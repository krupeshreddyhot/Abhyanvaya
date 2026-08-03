namespace Abhyanvaya.Application.DTOs.Faculty;

public sealed class WorkspacePreferenceDto
{
    public int Id { get; init; }
    public int StaffId { get; init; }
    public int UserId { get; init; }
    public string LandingPage { get; init; } = "home";
    public string DashboardLayout { get; init; } = "comfortable";
    public string DefaultTimetableView { get; init; } = "Today";
    public IReadOnlyList<string> FavoriteQuickActions { get; init; } = [];
    public string ThemePreference { get; init; } = "system";
    public IReadOnlyDictionary<string, bool> NotificationPreferences { get; init; } =
        new Dictionary<string, bool>();
    public bool OneHandedMode { get; init; }
    public bool HighContrast { get; init; }
    public DateTime? UpdatedUtc { get; init; }
}

public sealed class UpdateWorkspacePreferenceRequest
{
    public string? LandingPage { get; set; }
    public string? DashboardLayout { get; set; }
    public string? DefaultTimetableView { get; set; }
    public IReadOnlyList<string>? FavoriteQuickActions { get; set; }
    public string? ThemePreference { get; set; }
    public IReadOnlyDictionary<string, bool>? NotificationPreferences { get; set; }
    public bool? OneHandedMode { get; set; }
    public bool? HighContrast { get; set; }
}

public sealed class FacultyTimelineDto
{
    public DateOnly Date { get; init; }
    public IReadOnlyList<FacultyTimelineItemDto> Items { get; init; } = [];
    public bool ReusedTodaysSchedule => true;
}

public sealed class FacultyTimelineItemDto
{
    public string Kind { get; init; } = "Class"; // Class | Break
    public string Status { get; init; } = "Upcoming";
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string Label { get; init; } = "";
    public string? SubjectName { get; init; }
    public string? RoomName { get; init; }
    public string? BuildingName { get; init; }
    public string AttendanceStatus { get; init; } = "NotStarted";
    public bool AiReviewPending { get; init; }
    public FacultyClassDto? Class { get; init; }
}

public sealed class ClassroomNavigationDto
{
    public int RoomId { get; init; }
    public string RoomName { get; init; } = "";
    public string RoomCode { get; init; } = "";
    public int Capacity { get; init; }
    public string RoomType { get; init; } = "";
    public IReadOnlyList<string> Features { get; init; } = [];
    public bool AccessibilityFriendly { get; init; }
    public string CampusName { get; init; } = "";
    public string BuildingName { get; init; } = "";
    public string FloorName { get; init; } = "";
    public int FloorLevel { get; init; }
    public int? WalkingEstimateMinutes { get; init; }
    public string DirectionsPlaceholder { get; init; } = "Directions (future) — GIS not integrated in AI31.5.";
    public bool UsesGis => false;
}

public sealed class FacultyAttendanceProductivityDto
{
    public int PendingAttendance { get; init; }
    public int RemainingClasses { get; init; }
    public decimal AttendanceCompletionPercent { get; init; }
    public int AiPendingReviews { get; init; }
    public int MissedAttendance { get; init; }
    public int LateAttendance { get; init; }
    public string? QuickResumePath { get; init; }
    public bool ReusesAttendanceApis => true;
}

public sealed class FacultyProductivityDashboardDto
{
    public int ClassesToday { get; init; }
    public int AttendanceCompleted { get; init; }
    public decimal AttendanceRate { get; init; }
    public int AiUsage { get; init; }
    public decimal? RecognitionAccuracy { get; init; }
    public IReadOnlyList<FacultyChartPointDto> WeeklyWorkload { get; init; } = [];
    public IReadOnlyList<FacultyChartPointDto> MonthlyWorkload { get; init; } = [];
    public IReadOnlyList<FacultyChartPointDto> RoomUtilization { get; init; } = [];
    public bool ReusesExistingAnalytics => true;
}

public sealed class FacultyChartPointDto
{
    public string Label { get; init; } = "";
    public decimal Value { get; init; }
}

public sealed class FacultyCalendarExportDto
{
    public string Format { get; init; } = "ICS";
    public string FileName { get; init; } = "faculty-calendar.ics";
    public string ContentType { get; init; } = "text/calendar";
    public string Content { get; init; } = "";
    public string OutlookSubscriptionHint { get; init; } = "";
    public string GoogleSubscriptionHint { get; init; } = "";
    public bool ExportOnly => true;
    public bool TwoWaySync => false;
}

public sealed class FacultySearchResultDto
{
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string NavigationPath { get; init; } = "";
    public string? EntityKey { get; init; }
}

public sealed class FacultySearchResponseDto
{
    public string Query { get; init; } = "";
    public IReadOnlyList<FacultySearchResultDto> Results { get; init; } = [];
    public bool UsesElasticsearch => false;
}

public sealed class FacultySmartNotificationsDto
{
    public IReadOnlyList<FacultyScheduleNotificationDto> Items { get; init; } = [];
    public bool UsesSignalR => true;
    public bool UsesPolling => false;
}
