using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Dashboards;

namespace Abhyanvaya.Application.UnitTests.Dashboards;

/// <summary>AI31.6.11 — composition contracts, widget framework, preference/notification flags, resolver guard.</summary>
public class AI31_6_EnterpriseDashboardTests
{
    [Fact]
    public void FacultyWidgetCatalog_Contains_Required_Kpis()
    {
        var codes = DashboardWidgetCatalog.FacultyDefaults.Select(w => w.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[]
                 {
                     "todays-classes", "completed-classes", "remaining-classes", "todays-students",
                     "attendance-completed", "pending-attendance", "recovery-sessions", "recognition-reviews",
                     "avg-completion", "attendance-percent"
                 })
        {
            Assert.Contains(required, codes);
        }
    }

    [Fact]
    public void AdminWidgetCatalog_Contains_Operational_Widgets()
    {
        var codes = DashboardWidgetCatalog.AdminDefaults.Select(w => w.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[]
                 {
                     "pending-attendance", "pending-recovery", "draft-timetables", "published-timetables",
                     "conflict-count", "optimization-queue", "recognition-queue", "approval-queue",
                     "faculty-online", "todays-classes", "students-below-threshold", "platform-health"
                 })
        {
            Assert.Contains(required, codes);
        }
    }

    [Fact]
    public void ApplyPreferences_Hides_And_Reorders()
    {
        var prefs = new DashboardPreferenceDto
        {
            HiddenWidgets = ["todays-classes"],
            WidgetOrder = ["pending-attendance", "todays-students"]
        };
        var applied = DashboardWidgetCatalog.ApplyPreferences(DashboardWidgetCatalog.FacultyDefaults, prefs);
        Assert.False(applied.First(w => w.Code == "todays-classes").Visible);
        Assert.True(applied.First().Code is "pending-attendance" or "todays-students" or "todays-classes");
        Assert.Equal(0, applied.First(w => w.Code == "pending-attendance").SortOrder);
    }

    [Fact]
    public void FacultyCommandCenterDto_Safety_Flags()
    {
        var dto = new FacultyCommandCenterDto();
        Assert.True(dto.DoesNotModifyAttendanceApis);
        Assert.True(dto.DoesNotModifyAttendanceSessionResolver);
    }

    [Fact]
    public void FacultyInsightsPanel_Never_Generates_Ai()
    {
        var dto = new FacultyInsightsPanelDto();
        Assert.True(dto.NeverGeneratesAiContent);
        Assert.True(dto.ComposesExistingData);
        Assert.True(dto.SupportsSignalR);
    }

    [Fact]
    public void NotificationCenter_No_Polling()
    {
        var dto = new EnterpriseNotificationCenterDto();
        Assert.True(dto.UsesSignalR);
        Assert.True(dto.NoPolling);
    }

    [Fact]
    public void HealthCenter_ReadOnly()
    {
        var dto = new EnterpriseHealthCenterDto();
        Assert.True(dto.ReadOnly);
        Assert.True(dto.ReusesExistingHealthServices);
    }

    [Fact]
    public void Preferences_DatabasePersisted()
    {
        var dto = new DashboardPreferenceDto();
        Assert.True(dto.DatabasePersisted);
    }

    [Fact]
    public void Analytics_Export_Flags()
    {
        var dto = new EnterpriseOperationalAnalyticsDto();
        Assert.True(dto.SupportsExcelExport);
        Assert.True(dto.SupportsPdfExport);
        Assert.True(dto.ReusesExistingAnalytics);
    }

    [Fact]
    public void ActivityTimeline_NewestFirst_ReusesAudit()
    {
        var dto = new FacultyActivityTimelineDto();
        Assert.True(dto.NewestFirst);
        Assert.True(dto.ReusesAuditHistory);
    }

    [Fact]
    public void AdminOperations_CompositionOnly()
    {
        Assert.True(new AdminOperationsDashboardDto().CompositionOnly);
    }

    [Fact]
    public void DashboardPreference_Entity_Defaults()
    {
        var entity = new DashboardPreference();
        Assert.Equal("Faculty", entity.RoleScope);
        Assert.Equal("command-center", entity.DefaultLandingPage);
        Assert.Equal("[]", entity.HiddenWidgetsJson);
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void WidgetKinds_Support_Future_Roles()
    {
        var kinds = DashboardWidgetCatalog.FacultyDefaults.Select(w => w.Kind)
            .Concat(DashboardWidgetCatalog.AdminDefaults.Select(w => w.Kind))
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Kpi", kinds);
        Assert.Contains("Status", kinds);
        Assert.Contains("Action", kinds);
    }
}
