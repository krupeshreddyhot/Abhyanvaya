using Abhyanvaya.Application.DTOs.Dashboards;

namespace Abhyanvaya.Application.Dashboards;

/// <summary>
/// AI31.6.6 — reusable widget catalog for Faculty / Admin / future Principal / Student / Parent dashboards.
/// </summary>
public static class DashboardWidgetCatalog
{
    public static IReadOnlyList<DashboardWidgetDto> FacultyDefaults { get; } =
    [
        W("todays-classes", "Today's Classes", "Kpi", "Faculty", 1),
        W("completed-classes", "Completed Classes", "Kpi", "Faculty", 2),
        W("remaining-classes", "Remaining Classes", "Kpi", "Faculty", 3),
        W("todays-students", "Today's Students", "Kpi", "Faculty", 4),
        W("attendance-completed", "Attendance Completed", "Kpi", "Faculty", 5),
        W("pending-attendance", "Pending Attendance", "Kpi", "Faculty", 6, path: "/faculty/recovery"),
        W("recovery-sessions", "Recovery Sessions", "Kpi", "Faculty", 7, path: "/faculty/recovery"),
        W("recognition-reviews", "Recognition Reviews", "Kpi", "Faculty", 8),
        W("avg-completion", "Avg Completion Time", "Kpi", "Faculty", 9),
        W("attendance-percent", "Attendance %", "Kpi", "Faculty", 10),
        W("activity-timeline", "Activity Timeline", "Timeline", "Faculty", 11),
        W("insights-panel", "Insights", "Notification", "Faculty", 12),
        W("take-attendance", "Take Attendance", "Action", "Faculty", 13, path: "/attendance"),
    ];

    /// <summary>AI31.7 / AI31.7.5 — business terminology (no developer labels).</summary>
    public static IReadOnlyList<DashboardWidgetDto> AdminDefaults { get; } =
    [
        W("pending-attendance", "Attendance Sessions Pending", "Kpi", "Attendance", 1, path: "/setup/attendance-recovery"),
        W("pending-recovery", "Attendance Recovery Queue", "Kpi", "Recovery", 2, path: "/setup/attendance-recovery"),
        W("draft-timetables", "Draft Timetable Versions", "Kpi", "Scheduling", 3, path: "/setup/scheduling/timetables"),
        W("published-timetables", "Published Timetable Versions", "Kpi", "Scheduling", 4, path: "/setup/scheduling/governance/publishing"),
        W("conflict-count", "Scheduling Issues Requiring Attention", "Kpi", "Scheduling", 5, path: "/setup/scheduling/conflicts/dashboard"),
        W("optimization-queue", "Timetable Optimization Suggestions", "Kpi", "Scheduling", 6, path: "/setup/scheduling/optimization/dashboard"),
        W("recognition-queue", "AI Attendance Recognition Queue", "Kpi", "Attendance", 7, path: "/setup/attendance-recovery"),
        W("approval-queue", "Timetable Approval Queue", "Kpi", "Governance", 8, path: "/setup/scheduling/governance/approvals"),
        W("faculty-online", "Faculty Currently Teaching", "Status", "Faculty", 9),
        W("todays-classes", "Current Running Classes", "Kpi", "Academic", 10),
        W("students-below-threshold", "Students Below Attendance Requirement", "Kpi", "Student", 11, path: "/reports"),
        W("platform-health", "College System Health", "Status", "System", 12, path: "/dashboard/health"),
    ];

    public static IReadOnlyList<DashboardWidgetDto> ApplyPreferences(
        IEnumerable<DashboardWidgetDto> widgets,
        DashboardPreferenceDto prefs)
    {
        var hidden = prefs.HiddenWidgets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = prefs.WidgetOrder
            .Select((code, i) => (code, i))
            .ToDictionary(x => x.code, x => x.i, StringComparer.OrdinalIgnoreCase);

        return widgets
            .Select(w => new DashboardWidgetDto
            {
                Code = w.Code,
                Title = w.Title,
                Kind = w.Kind,
                Category = w.Category,
                Value = w.Value,
                DisplayValue = w.DisplayValue,
                Unit = w.Unit,
                Status = w.Status,
                StatusLabel = w.StatusLabel,
                Path = w.Path,
                ReportPath = w.ReportPath,
                Tooltip = w.Tooltip,
                LastUpdatedUtc = w.LastUpdatedUtc,
                Trend = w.Trend,
                Comparison = w.Comparison,
                SuggestedAction = w.SuggestedAction,
                EstimatedImpact = w.EstimatedImpact,
                ActionLabel = w.ActionLabel,
                Group = w.Group,
                RequiredPermission = w.RequiredPermission,
                Configurable = w.Configurable,
                Visible = !hidden.Contains(w.Code),
                SortOrder = order.TryGetValue(w.Code, out var idx) ? idx : w.SortOrder
            })
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DashboardWidgetDto W(
        string code,
        string title,
        string kind,
        string category,
        int order,
        string? path = null) =>
        new()
        {
            Code = code,
            Title = title,
            Kind = kind,
            Category = category,
            Path = path,
            SortOrder = order
        };
}
