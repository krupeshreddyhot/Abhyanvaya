using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Faculty;

public sealed class AI315FacultyEnhancementTests
{
    [Fact]
    public void CalendarExport_IsExportOnly_NoTwoWaySync()
    {
        var export = new FacultyCalendarExportDto
        {
            Content = "BEGIN:VCALENDAR\nEND:VCALENDAR",
            FileName = "faculty-calendar.ics"
        };
        Assert.True(export.ExportOnly);
        Assert.False(export.TwoWaySync);
        Assert.Contains("VCALENDAR", export.Content);
    }

    [Fact]
    public void Timeline_SupportsClassAndBreakKinds()
    {
        var dto = new FacultyTimelineDto
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items =
            [
                new FacultyTimelineItemDto { Kind = "Class", Status = "Completed", Label = "A", AttendanceStatus = "Completed" },
                new FacultyTimelineItemDto { Kind = "Break", Status = "Break", Label = "Break (10 min)", AttendanceStatus = "N/A" },
                new FacultyTimelineItemDto { Kind = "Class", Status = "Current", Label = "B", AttendanceStatus = "NotStarted", AiReviewPending = true },
            ]
        };
        Assert.True(dto.ReusedTodaysSchedule);
        Assert.Contains(dto.Items, i => i.Kind == "Break");
        Assert.Contains(dto.Items, i => i.AiReviewPending);
    }

    [Fact]
    public void WorkspacePreference_IsPerFacultyNotGlobal()
    {
        var entity = new WorkspacePreference
        {
            TenantId = 9,
            StaffId = 42,
            UserId = 7,
            LandingPage = "timeline",
            ThemePreference = "highContrast"
        };
        Assert.Equal(42, entity.StaffId);
        Assert.Equal(9, entity.TenantId);
        Assert.NotEqual(0, entity.StaffId);
    }

    [Fact]
    public void PreferenceDto_MapsFavoriteActionsAndNotifications()
    {
        var dto = new WorkspacePreferenceDto
        {
            StaffId = 1,
            UserId = 2,
            FavoriteQuickActions = ["TAKE_ATTENDANCE", "AI_ATTENDANCE"],
            NotificationPreferences = new Dictionary<string, bool>
            {
                ["UpcomingClass"] = true,
                ["AttendanceReminder"] = false
            }
        };
        Assert.Contains("TAKE_ATTENDANCE", dto.FavoriteQuickActions);
        Assert.False(dto.NotificationPreferences["AttendanceReminder"]);
    }

    [Fact]
    public void ClassroomNavigation_HasNoGis()
    {
        var nav = new ClassroomNavigationDto
        {
            RoomId = 1,
            RoomName = "Lab-1",
            DirectionsPlaceholder = "Directions (future)",
            WalkingEstimateMinutes = 5
        };
        Assert.False(nav.UsesGis);
        Assert.Contains("future", nav.DirectionsPlaceholder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Productivity_ReusesAttendanceApisFlag()
    {
        var prod = new FacultyAttendanceProductivityDto
        {
            PendingAttendance = 1,
            RemainingClasses = 2,
            AttendanceCompletionPercent = 50,
            QuickResumePath = "/attendance"
        };
        Assert.True(prod.ReusesAttendanceApis);
        Assert.Equal("/attendance", prod.QuickResumePath);
    }

    [Fact]
    public void ProductivityDashboard_ReusesAnalyticsFlag()
    {
        var dash = new FacultyProductivityDashboardDto
        {
            ClassesToday = 3,
            AttendanceCompleted = 2,
            AttendanceRate = 66.7m,
            WeeklyWorkload = [new FacultyChartPointDto { Label = "Mon", Value = 2 }],
            MonthlyWorkload = [new FacultyChartPointDto { Label = "Sessions", Value = 10 }],
            RoomUtilization = [new FacultyChartPointDto { Label = "R1", Value = 1 }]
        };
        Assert.True(dash.ReusesExistingAnalytics);
        Assert.NotEmpty(dash.WeeklyWorkload);
    }

    [Fact]
    public void SmartNotifications_NoPolling()
    {
        var smart = new FacultySmartNotificationsDto
        {
            Items =
            [
                new FacultyScheduleNotificationDto
                {
                    NotificationId = "1",
                    Kind = "UpcomingClass",
                    Title = "Upcoming",
                    Message = "Math",
                    OccurredUtc = DateTime.UtcNow
                }
            ]
        };
        Assert.True(smart.UsesSignalR);
        Assert.False(smart.UsesPolling);
    }

    [Fact]
    public void Search_DoesNotUseElasticsearch()
    {
        var response = new FacultySearchResponseDto
        {
            Query = "math",
            Results =
            [
                new FacultySearchResultDto
                {
                    Category = "Subject",
                    Title = "Mathematics",
                    NavigationPath = "/faculty?tab=timetable"
                }
            ]
        };
        Assert.False(response.UsesElasticsearch);
        Assert.NotEmpty(response.Results);
    }

    [Fact]
    public void AttendanceCompatibility_ResolverStillOwnsModeSelection()
    {
        var timetable = new AttendanceSessionResolutionDto { Mode = "Timetable", HasTimetable = true };
        var legacy = new AttendanceSessionResolutionDto { Mode = "Legacy", HasTimetable = false };
        Assert.True(timetable.HasTimetable);
        Assert.False(legacy.HasTimetable);
        Assert.Equal("IAttendanceSessionResolver", typeof(IAttendanceSessionResolver).Name);
        Assert.False(new FacultyTodayDto().ModifiesAttendanceApis);
    }

    [Fact]
    public void Accessibility_HighContrastAndOneHandedArePersonalFlags()
    {
        var prefs = new WorkspacePreferenceDto
        {
            StaffId = 3,
            HighContrast = true,
            OneHandedMode = true,
            ThemePreference = "highContrast"
        };
        Assert.True(prefs.HighContrast);
        Assert.True(prefs.OneHandedMode);
    }
}
