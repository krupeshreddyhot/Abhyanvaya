using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// AI31.5 — per-faculty workspace preferences. Tenant-aware, auditable via BaseEntity.
/// Not application-wide settings.
/// </summary>
public class WorkspacePreference : BaseEntity
{
    public int StaffId { get; set; }
    public int UserId { get; set; }
    /// <summary>home | class | timetable | insights | notifications | productivity | timeline</summary>
    public string LandingPage { get; set; } = "home";
    /// <summary>compact | comfortable | focus</summary>
    public string DashboardLayout { get; set; } = "comfortable";
    /// <summary>Today | Week | Month | Agenda</summary>
    public string DefaultTimetableView { get; set; } = "Today";
    public string FavoriteQuickActionsCsv { get; set; } = "TAKE_ATTENDANCE,AI_ATTENDANCE,TIMETABLE";
    /// <summary>system | light | dark | highContrast</summary>
    public string ThemePreference { get; set; } = "system";
    /// <summary>JSON map of notification kinds → enabled</summary>
    public string NotificationPreferencesJson { get; set; } =
        """{"UpcomingClass":true,"AttendanceReminder":true,"AiReviewPending":true,"RoomChanged":true,"FacultySubstitution":true,"HolidayUpdate":true,"WorkingDayChange":true}""";
    public bool OneHandedMode { get; set; }
    public bool HighContrast { get; set; }
}
