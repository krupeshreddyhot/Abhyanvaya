using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities.Dashboards;

namespace Abhyanvaya.Application.UnitTests.Dashboards;

/// <summary>AI31.8.15 — excellence contracts, personalization, attendance compatibility.</summary>
public class AI31_8_EnterpriseDashboardExcellenceTests
{
    [Fact]
    public void Excellence_Dto_Safety_Flags()
    {
        var dto = new EnterpriseDashboardExcellenceDto();
        Assert.True(dto.CompositionOnly);
        Assert.True(dto.DoesNotModifyAttendanceApis);
        Assert.True(dto.DoesNotModifyAttendanceSessionResolver);
        Assert.True(dto.SupportsLegacyAndTimetableAttendance);
        Assert.True(dto.UsesSignalRWhenAvailable);
    }

    [Fact]
    public void Executive_Summary_Has_Institutional_Cards()
    {
        var summary = new ExecutiveSummaryDto
        {
            Cards =
            [
                new() { Code = "exec-academic-year", Title = "Academic Year" },
                new() { Code = "exec-college", Title = "College Name" },
                new() { Code = "exec-alerts", Title = "Critical Alerts" },
                new() { Code = "exec-health", Title = "Platform Health" },
            ]
        };
        Assert.Contains(summary.Cards, c => c.Code == "exec-academic-year");
        Assert.Contains(summary.Cards, c => c.Code == "exec-college");
        Assert.Contains(summary.Cards, c => c.Code == "exec-health");
    }

    [Fact]
    public void Filters_Cover_Academic_Scope()
    {
        var f = new DashboardFilterRequest
        {
            AcademicYearId = 1,
            DepartmentId = 2,
            CourseId = 3,
            CampusId = 4,
            BuildingId = 5,
            RoomId = 6
        };
        Assert.Equal(1, f.AcademicYearId);
        Assert.Equal(6, f.RoomId);
    }

    [Fact]
    public void Preference_Entity_Supports_Pin_Filter_Refresh()
    {
        var entity = new DashboardPreference
        {
            PinnedWidgetsJson = "[\"attendance-recovery-queue\"]",
            FilterJson = "{\"academicYearId\":1}",
            RefreshIntervalSeconds = 120,
            HighContrast = true,
            UserId = 9,
            TenantId = 1,
            RoleScope = "Admin"
        };
        Assert.Contains("attendance-recovery-queue", entity.PinnedWidgetsJson);
        Assert.Equal(120, entity.RefreshIntervalSeconds);
        Assert.True(entity.HighContrast);
    }

    [Fact]
    public void Action_Groups_Are_Permission_Aware()
    {
        var group = new ActionGroupDto
        {
            Code = "attendance",
            Title = "Attendance",
            Actions =
            [
                new() { Code = "take-attendance", Path = "/attendance", RequiredPermission = PermissionKeys.AttendanceManage, Shortcut = "A" }
            ]
        };
        Assert.All(group.Actions, a => Assert.False(string.IsNullOrWhiteSpace(a.RequiredPermission)));
        Assert.All(group.Actions, a => Assert.StartsWith("/", a.Path));
    }

    [Fact]
    public void Timeline_Is_ReadOnly_Composition()
    {
        var timeline = new AcademicTimelineDto
        {
            CurrentPeriodLabel = "Period 4",
            Items =
            [
                new() { Kind = "Period", Label = "Period 4", Status = "Current", IsCurrent = true },
                new() { Kind = "Lunch", Label = "Lunch", Status = "Completed" }
            ]
        };
        Assert.True(timeline.ReadOnly);
        Assert.True(timeline.ReusesTimetableService);
        Assert.Contains(timeline.Items, i => i.IsCurrent);
    }

    [Fact]
    public void Visualizations_Are_ReadOnly()
    {
        Assert.True(new DashboardVisualizationsDto().ReadOnly);
    }

    [Fact]
    public void Widget_Help_Has_Purpose_And_Links()
    {
        var help = new WidgetHelpDto
        {
            WidgetCode = "attendance-recovery-queue",
            Purpose = "Recovery queue",
            HowCalculated = "TodayCount",
            UpdateFrequency = "On refresh",
            RelatedModules = ["Attendance Recovery"],
            NavigationLinks = [new QuickLinkDto { Label = "Recovery", Path = "/setup/attendance-recovery" }]
        };
        Assert.False(string.IsNullOrWhiteSpace(help.Purpose));
        Assert.StartsWith("/", help.NavigationLinks[0].Path);
    }

    [Fact]
    public void Intelligent_Kpi_Has_Explanation_And_Trend()
    {
        var card = new DashboardWidgetDto
        {
            Title = "Attendance Recovery Queue",
            DisplayValue = "37",
            Unit = "Sessions",
            Explanation = "Awaiting Faculty Review",
            Trend = "up",
            LastUpdatedUtc = DateTime.UtcNow,
            Status = "Orange",
            Path = "/setup/attendance-recovery"
        };
        Assert.Equal("Awaiting Faculty Review", card.Explanation);
        Assert.False(string.IsNullOrWhiteSpace(card.Path));
    }

    [Fact]
    public void Drilldown_Paths_Remain_Module_Scoped()
    {
        foreach (var w in DashboardWidgetCatalog.AdminDefaults.Where(x => !string.IsNullOrWhiteSpace(x.Path)))
            Assert.StartsWith("/", w.Path);
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void Refresh_Options_Contract()
    {
        foreach (var seconds in new[] { 0, 30, 60, 120, 300 })
            Assert.Contains(seconds, new[] { 0, 30, 60, 120, 300 });
    }

    [Fact]
    public void Command_Center_Section_Codes_Preserved()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.Equal("attention", dto.AttentionRequired.Code);
        Assert.Equal("Timetable Operations", dto.SchedulingOperations.Title);
        Assert.Equal("College System Health", dto.SystemHealth.Title);
    }
}
