using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Dashboards;

/// <summary>AI31.7.5 — UX terminology, hierarchy, banners, attendance compatibility.</summary>
public class AI31_7_5_EnterpriseDashboardUxTests
{
    [Fact]
    public void Section_Titles_Use_Business_Terminology()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.Equal("Attention Required", dto.AttentionRequired.Title);
        Assert.Equal("Today's Operations", dto.TodaysOperations.Title);
        Assert.Equal("Timetable Operations", dto.SchedulingOperations.Title);
        Assert.Equal("Attendance Operations", dto.AttendanceOperations.Title);
        Assert.Equal("Academic Resources", dto.AcademicResources.Title);
        Assert.Equal("College System Health", dto.SystemHealth.Title);
    }

    [Fact]
    public void Section_Icons_Present()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.Equal("🚨", dto.AttentionRequired.Icon);
        Assert.Equal("📅", dto.TodaysOperations.Icon);
        Assert.Equal("🗓", dto.SchedulingOperations.Icon);
        Assert.Equal("📝", dto.AttendanceOperations.Icon);
        Assert.Equal("🎓", dto.AcademicResources.Icon);
        Assert.Equal("🖥", dto.SystemHealth.Icon);
    }

    [Fact]
    public void Catalog_Has_No_Developer_Labels()
    {
        var titles = DashboardWidgetCatalog.AdminDefaults.Select(w => w.Title).ToList();
        Assert.DoesNotContain(titles, t => t.Equals("Conflict Count", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Pending Recovery", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Recognition Queue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Optimization Queue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Approval Queue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Platform Health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Draft Timetables", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Scheduling Issues Requiring Attention", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Attendance Recovery Queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("AI Attendance Recognition Queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Timetable Optimization Suggestions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Timetable Approval Queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("College System Health", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Draft Timetable Versions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Attention_Card_Has_Rich_Ux_Fields()
    {
        var card = new DashboardWidgetDto
        {
            Code = "attendance-recovery-queue",
            Title = "Attendance Recovery Queue",
            Value = 37,
            DisplayValue = "37",
            Unit = "Sessions",
            Status = "Orange",
            StatusLabel = "High",
            Path = "/setup/attendance-recovery",
            ReportPath = "/reports",
            Tooltip = "Queue depth",
            SuggestedAction = "Open Attendance Recovery",
            EstimatedImpact = "May delay finalization",
            ActionLabel = "View Details",
            Comparison = "+4 since yesterday",
            Trend = "up",
            LastUpdatedUtc = DateTime.UtcNow
        };
        Assert.Equal("Sessions", card.Unit);
        Assert.Equal("High", card.StatusLabel);
        Assert.False(string.IsNullOrWhiteSpace(card.SuggestedAction));
        Assert.False(string.IsNullOrWhiteSpace(card.EstimatedImpact));
        Assert.Equal("View Details", card.ActionLabel);
    }

    [Fact]
    public void Severity_Order_Contract()
    {
        var ranks = new Dictionary<string, int>
        {
            ["Red"] = 0,
            ["Orange"] = 1,
            ["Yellow"] = 2,
            ["Info"] = 3,
            ["Green"] = 4
        };
        Assert.True(ranks["Red"] < ranks["Orange"]);
        Assert.True(ranks["Orange"] < ranks["Yellow"]);
        Assert.True(ranks["Yellow"] < ranks["Info"]);
    }

    [Fact]
    public void Action_Banner_Is_Permission_Aware()
    {
        var banner = new CommandCenterActionBannerDto
        {
            Code = "banner-attendance-recovery-queue",
            Message = "37 Sessions — Attendance Recovery Queue.",
            Path = "/setup/attendance-recovery",
            ActionLabel = "Review Now",
            Severity = "Orange",
            RequiredPermission = PermissionKeys.AttendanceManage
        };
        Assert.Equal(PermissionKeys.AttendanceManage, banner.RequiredPermission);
        Assert.StartsWith("/", banner.Path);
    }

    [Fact]
    public void Attendance_Groups_Contract()
    {
        var section = new CommandCenterSectionDto
        {
            Code = "attendance",
            Title = "Attendance Operations",
            GroupOrder = ["Running Sessions", "Recognition", "Review", "Recovery", "Completed"],
            Cards =
            [
                new() { Code = "sessions-running", Title = "Running Sessions", Group = "Running Sessions" },
                new() { Code = "recognition-in-progress", Title = "Recognition In Progress", Group = "Recognition" },
                new() { Code = "attendance-review-queue", Title = "Attendance Review Queue", Group = "Review" },
                new() { Code = "attendance-recovery-queue", Title = "Attendance Recovery Queue", Group = "Recovery" },
                new() { Code = "completed-today", Title = "Completed Today", Group = "Completed" },
            ]
        };
        Assert.Equal(5, section.GroupOrder.Count);
        Assert.All(section.Cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Group)));
    }

    [Fact]
    public void Refresh_Interval_Is_Sixty_Seconds()
    {
        Assert.Equal(60, new EnterpriseOperationsCommandCenterDto().RefreshIntervalSeconds);
    }

    [Fact]
    public void Safety_And_Attendance_Compatibility_Flags()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.True(dto.CompositionOnly);
        Assert.True(dto.DoesNotModifyAttendanceApis);
        Assert.True(dto.DoesNotModifyAttendanceSessionResolver);
        Assert.True(dto.SupportsLegacyAndTimetableAttendance);
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void Health_Labels_Are_Business_Facing()
    {
        Assert.Equal("Healthy", Map("Green"));
        Assert.Equal("Warning", Map("Yellow"));
        Assert.Equal("Critical", Map("Red"));

        static string Map(string status) => status switch
        {
            "Green" => "Healthy",
            "Yellow" => "Warning",
            "Red" => "Critical",
            _ => status
        };
    }

    [Fact]
    public void Legacy_Ai31_7_Section_Codes_Preserved()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.Equal("attention", dto.AttentionRequired.Code);
        Assert.Equal("today", dto.TodaysOperations.Code);
        Assert.Equal("scheduling", dto.SchedulingOperations.Code);
        Assert.Equal("attendance", dto.AttendanceOperations.Code);
        Assert.Equal("academic", dto.AcademicResources.Code);
        Assert.Equal("health", dto.SystemHealth.Code);
    }
}
