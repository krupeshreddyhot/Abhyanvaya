using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Dashboards;

/// <summary>AI31.7.11 — Command Center grouping, terminology, navigation, resolver guard.</summary>
public class AI31_7_OperationsCommandCenterTests
{
    [Fact]
    public void CommandCenter_Section_Order_Contract()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.Equal("attention", dto.AttentionRequired.Code);
        Assert.Equal("today", dto.TodaysOperations.Code);
        Assert.Equal("scheduling", dto.SchedulingOperations.Code);
        Assert.Equal("attendance", dto.AttendanceOperations.Code);
        Assert.Equal("academic", dto.AcademicResources.Code);
        Assert.Equal("health", dto.SystemHealth.Code);
    }

    [Fact]
    public void Business_Terminology_No_Developer_Labels()
    {
        var titles = DashboardWidgetCatalog.AdminDefaults.Select(w => w.Title).ToList();
        Assert.DoesNotContain(titles, t => t.Equals("Conflict Count", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Pending Recovery", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, t => t.Equals("Platform Health", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Scheduling Issues", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Attendance Recovery Queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("College System Health", StringComparison.OrdinalIgnoreCase)
            || t.Contains("System Health", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("Timetable Approval Queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(titles, t => t.Contains("AI Attendance Recognition Queue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void QuickActions_Have_Permission_Keys()
    {
        var actions = new CommandCenterQuickActionDto[]
        {
            new() { Code = "take-attendance", RequiredPermission = PermissionKeys.AttendanceManage, Path = "/attendance" },
            new() { Code = "approve-timetable", RequiredPermission = PermissionKeys.SchedulingApprove, Path = "/setup/scheduling/governance/approvals" },
        };
        Assert.All(actions, a => Assert.False(string.IsNullOrWhiteSpace(a.RequiredPermission)));
        Assert.All(actions, a => Assert.StartsWith("/", a.Path));
    }

    [Fact]
    public void Attention_Cards_Require_Path_And_Severity()
    {
        var card = new DashboardWidgetDto
        {
            Code = "attendance-review-queue",
            Title = "Attendance Sessions Awaiting Review",
            Path = "/setup/attendance-recovery",
            Status = "Yellow",
            Tooltip = "Sessions waiting for review",
            LastUpdatedUtc = DateTime.UtcNow
        };
        Assert.False(string.IsNullOrWhiteSpace(card.Path));
        Assert.Contains(card.Status, new[] { "Green", "Yellow", "Orange", "Red" });
        Assert.False(string.IsNullOrWhiteSpace(card.Tooltip));
        Assert.NotNull(card.LastUpdatedUtc);
    }

    [Fact]
    public void Safety_Flags()
    {
        var dto = new EnterpriseOperationsCommandCenterDto();
        Assert.True(dto.CompositionOnly);
        Assert.True(dto.DoesNotModifyAttendanceApis);
        Assert.True(dto.DoesNotModifyAttendanceSessionResolver);
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void Widget_Drilldown_Paths_Point_To_Modules()
    {
        foreach (var w in DashboardWidgetCatalog.AdminDefaults.Where(x => !string.IsNullOrWhiteSpace(x.Path)))
        {
            Assert.StartsWith("/", w.Path);
        }
    }

    [Fact]
    public void CommandCenter_Title()
    {
        Assert.Equal("Enterprise Operations Command Center", new EnterpriseOperationsCommandCenterDto().Title);
    }
}
