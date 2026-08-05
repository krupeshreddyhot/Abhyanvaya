using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Configuration;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.Dashboards;

public interface IOperationsCommandCenterService
{
    Task<EnterpriseOperationsCommandCenterDto> GetAsync(
        DashboardFilterRequest? filters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AI31.7 / AI31.7.5 — Enterprise Operations Command Center (UX composition).
/// Composes AI22 recovery, AI30 scheduling/governance/conflicts, AI31.6 health/preferences.
/// Sequential DbContext access; no repositories; no AttendanceSessionResolver / Attendance API changes.
/// </summary>
public sealed class OperationsCommandCenterService : IOperationsCommandCenterService
{
    private readonly IAttendanceRecoveryDashboardService _recoveryDashboard;
    private readonly IAttendanceOperationalAnalyticsService _opsAnalytics;
    private readonly IEnterpriseOpsDashboardService _enterpriseOps;
    private readonly IAttendanceHealthMonitorService _attendanceHealth;
    private readonly IEnterpriseHealthCenterService _healthCenter;
    private readonly ISchedulingDashboardService _schedulingDashboard;
    private readonly ITimetableService _timetables;
    private readonly ITimetableGovernanceDashboardService _governance;
    private readonly IConflictDetectionService _conflicts;
    private readonly ISchedulingConfigurationReadinessService _readiness;
    private readonly IDashboardPreferenceService _preferences;
    private readonly IEnterpriseNotificationCenterService _notifications;

    public OperationsCommandCenterService(
        IAttendanceRecoveryDashboardService recoveryDashboard,
        IAttendanceOperationalAnalyticsService opsAnalytics,
        IEnterpriseOpsDashboardService enterpriseOps,
        IAttendanceHealthMonitorService attendanceHealth,
        IEnterpriseHealthCenterService healthCenter,
        ISchedulingDashboardService schedulingDashboard,
        ITimetableService timetables,
        ITimetableGovernanceDashboardService governance,
        IConflictDetectionService conflicts,
        ISchedulingConfigurationReadinessService readiness,
        IDashboardPreferenceService preferences,
        IEnterpriseNotificationCenterService notifications)
    {
        _recoveryDashboard = recoveryDashboard;
        _opsAnalytics = opsAnalytics;
        _enterpriseOps = enterpriseOps;
        _attendanceHealth = attendanceHealth;
        _healthCenter = healthCenter;
        _schedulingDashboard = schedulingDashboard;
        _timetables = timetables;
        _governance = governance;
        _conflicts = conflicts;
        _readiness = readiness;
        _preferences = preferences;
        _notifications = notifications;
    }

    public async Task<EnterpriseOperationsCommandCenterDto> GetAsync(
        DashboardFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var yearId = filters?.AcademicYearId;

        var recovery = await Safe(() => _recoveryDashboard.GetAdminDashboardAsync(cancellationToken));
        var analytics = await Safe(() => _opsAnalytics.GetAsync(cancellationToken));
        var ops = await Safe(() => _enterpriseOps.GetAsync(cancellationToken));
        var attendanceHealth = await Safe(() => _attendanceHealth.ScanAsync(cancellationToken));
        var health = await Safe(() => _healthCenter.GetAsync(cancellationToken));
        var scheduling = await Safe(() => _schedulingDashboard.GetSummaryAsync(cancellationToken));
        var timetable = await Safe(() => _timetables.GetDashboardAsync(yearId, cancellationToken));
        var governance = await Safe(() => _governance.GetDashboardAsync(yearId, cancellationToken));
        var conflicts = await Safe(() => _conflicts.GetDashboardAsync(yearId, null, cancellationToken));
        var readiness = await Safe(() => _readiness.GetSummaryAsync(cancellationToken));
        var notifications = await Safe(() => _notifications.GetAsync(cancellationToken));
        var prefs = await Safe(() => _preferences.GetAsync("Admin", cancellationToken))
            ?? new DashboardPreferenceDto { RoleScope = "Admin", DefaultLandingPage = "admin-operations" };

        var conflictTotal = ConflictTotal(conflicts);
        var attention = BuildAttention(recovery, governance, conflictTotal, attendanceHealth, notifications, now);
        var today = BuildToday(recovery, analytics, timetable, scheduling, now);
        var schedulingOps = BuildTimetableOperations(scheduling, timetable, governance, conflictTotal, readiness, now);
        var attendanceOps = BuildAttendance(recovery, analytics, ops, now);
        var academic = BuildAcademic(scheduling, now);
        var systemHealth = BuildSystemHealth(health, now);
        var banners = BuildActionBanners(attention.Cards);

        return new EnterpriseOperationsCommandCenterDto
        {
            AttentionRequired = attention,
            TodaysOperations = today,
            SchedulingOperations = schedulingOps,
            AttendanceOperations = attendanceOps,
            AcademicResources = academic,
            SystemHealth = systemHealth,
            ActionBanners = banners,
            QuickActions = BuildQuickActions(),
            Preferences = prefs,
            GeneratedUtc = now,
            RefreshIntervalSeconds = 60
        };
    }

    private static CommandCenterSectionDto BuildAttention(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.Scheduling.TimetableGovernanceDashboardDto? governance,
        decimal? conflictTotal,
        DTOs.AttendanceRecovery.AttendanceHealthSnapshotDto? attendanceHealth,
        EnterpriseNotificationCenterDto? notifications,
        DateTime now)
    {
        var cards = new List<DashboardWidgetDto>
        {
            Attention(
                "attendance-recovery-queue",
                "Attendance Recovery Queue",
                recovery?.TodayCount,
                "Sessions",
                "/setup/attendance-recovery",
                "/reports",
                "Attendance sessions waiting in the recovery queue for faculty or admin action.",
                "Open Attendance Recovery",
                "May delay same-day attendance finalization.",
                ComparisonDelta(recovery?.TodayCount, recovery?.YesterdayCount),
                TrendFromDelta(recovery?.TodayCount, recovery?.YesterdayCount),
                now,
                Severity(recovery?.TodayCount, warn: 1, orange: 5, red: 20)),
            Attention(
                "timetable-approval-queue",
                "Timetable Approval Queue",
                governance?.ApprovalQueueCount,
                "Items",
                "/setup/scheduling/governance/approvals",
                null,
                "Timetable versions waiting for approval before they can be published.",
                "Approve Timetable",
                "Blocks publishing until approvals complete.",
                null,
                null,
                now,
                Severity(governance?.ApprovalQueueCount, warn: 1, orange: 3, red: 10)),
            Attention(
                "scheduling-issues",
                "Scheduling Issues Requiring Attention",
                conflictTotal,
                "Issues",
                "/setup/scheduling/conflicts/workspace",
                "/setup/scheduling/conflicts/dashboard",
                "Open faculty, room, student, or calendar scheduling issues that need resolution.",
                "Open Scheduling Issues",
                "May affect room/faculty availability for classes.",
                null,
                null,
                now,
                Severity((int?)conflictTotal, warn: 1, orange: 5, red: 15)),
            Attention(
                "ai-recognition-queue",
                "AI Attendance Recognition Queue",
                recovery?.ReviewPendingCount,
                "Sessions",
                "/setup/attendance-recovery",
                null,
                "AI attendance recognition results awaiting human review.",
                "Review Recognition",
                "Students may remain unmarked until review completes.",
                null,
                null,
                now,
                Severity(recovery?.ReviewPendingCount, warn: 1, orange: 5, red: 15)),
            Attention(
                "optimization-suggestions",
                "Timetable Optimization Suggestions",
                governance?.PendingReviewsCount,
                "Suggestions",
                "/setup/scheduling/optimization/workspace",
                null,
                "Timetable optimization suggestions ready for review.",
                "Review Suggestions",
                "Optional improvements — does not change published schedules until approved.",
                null,
                null,
                now,
                Severity(governance?.PendingReviewsCount, warn: 1, orange: 3, red: 8)),
            Attention(
                "critical-system-alerts",
                "Critical System Alerts",
                (attendanceHealth?.Alerts.Count ?? 0) + (notifications?.UnreadCount ?? 0),
                "Alerts",
                "/dashboard/health",
                "/dashboard/notifications",
                "Critical operational and college system alerts requiring attention.",
                "Open Health Center",
                "May indicate service degradation affecting attendance or scheduling.",
                null,
                null,
                now,
                Severity((attendanceHealth?.Alerts.Count ?? 0) + (notifications?.UnreadCount ?? 0), warn: 1, orange: 3, red: 5)),
        };

        cards = cards
            .OrderBy(c => SeverityRank(c.Status))
            .ThenByDescending(c => c.Value ?? 0)
            .Select((c, i) => CloneWithOrder(c, i))
            .ToList();

        return new CommandCenterSectionDto
        {
            Code = "attention",
            Title = "Attention Required",
            Icon = "🚨",
            Subtitle = "Sorted by severity — Critical, High, Medium, then Information.",
            Cards = cards,
            QuickLinks =
            [
                Link("Attendance Recovery", "/setup/attendance-recovery"),
                Link("Timetable Approval Queue", "/setup/scheduling/governance/approvals"),
                Link("Scheduling Issues", "/setup/scheduling/conflicts/workspace"),
                Link("Notifications", "/dashboard/notifications"),
            ]
        };
    }

    private static CommandCenterSectionDto BuildToday(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.AttendanceRecovery.AttendanceOperationalAnalyticsDto? analytics,
        DTOs.Scheduling.TimetableDashboardDto? timetable,
        DTOs.Scheduling.SchedulingDashboardDto? scheduling,
        DateTime now)
    {
        var local = DateTime.Now;
        var completed = analytics?.SessionsCompleted;
        var started = analytics?.SessionsStarted;
        double? completionPct = started is > 0 && completed.HasValue
            ? Math.Round(100.0 * completed.Value / started.Value, 1)
            : null;
        var remaining = timetable?.ScheduledPeriodCount is int total && completed.HasValue
            ? Math.Max(0, total - completed.Value)
            : (int?)null;
        var running = recovery?.ProcessingCount;

        return new CommandCenterSectionDto
        {
            Code = "today",
            Title = "Today's Operations",
            Icon = "📅",
            Subtitle = "Live college operations snapshot (refreshes every 60 seconds).",
            Cards =
            [
                Kpi("current-time", "Current Time", null, "/dashboard", now,
                    display: local.ToString("HH:mm"),
                    unit: "Local",
                    tooltip: "College local clock for operators.",
                    actionLabel: "Open Module"),
                Kpi("current-academic-period", "Current Academic Period", null, "/setup/scheduling/timetables", now,
                    display: EstimatePeriodLabel(local.TimeOfDay),
                    unit: "Period",
                    tooltip: "Indicative period from local clock. AttendanceSessionResolver remains the only mode selector for attendance."),
                Kpi("classes-running-now", "Current Running Classes", running, "/setup/attendance-recovery", now,
                    unit: "Classes",
                    status: Severity(running, 1, 10, 30),
                    tooltip: "Attendance sessions currently processing / in progress."),
                Kpi("classes-remaining-today", "Remaining Classes Today", remaining, "/setup/scheduling/timetables/dashboard", now,
                    unit: "Classes",
                    trend: remaining is > 0 ? "flat" : "up",
                    tooltip: "Scheduled periods minus completed attendance sessions today (composed)."),
                Kpi("faculty-teaching-now", "Faculty Currently Teaching",
                    running ?? timetable?.FacultyScheduledCount, "/setup/staff", now,
                    unit: "Faculty",
                    tooltip: "Faculty associated with currently active attendance / scheduled load."),
                Kpi("students-present-today", "Students Present Today", null, "/reports", now,
                    display: "—",
                    unit: "Students",
                    reportPath: "/reports",
                    tooltip: "Present counts available via Reports; composition does not invent attendance totals."),
                Kpi("attendance-completion-today", "Attendance Completion Today", (decimal?)completionPct, "/setup/attendance-recovery", now,
                    display: completionPct is null ? "—" : $"{completionPct:0.#}%",
                    unit: "%",
                    trend: completionPct is >= 80 ? "up" : completionPct is null ? null : "down",
                    tooltip: "Completed attendance sessions vs started today."),
                Kpi("current-attendance-rate", "Current Attendance Rate", (decimal?)completionPct, "/reports", now,
                    display: completionPct is null ? "—" : $"{completionPct:0.#}%",
                    unit: "%",
                    reportPath: "/reports",
                    tooltip: "Same-day attendance completion rate (composed from operational analytics)."),
                Kpi("todays-events", "Today's Events", scheduling?.HolidayTypeCatalogCount, "/setup/scheduling", now,
                    unit: "Events",
                    tooltip: "Configured academic event / holiday catalog signals for today."),
                Kpi("todays-holidays", "Today's Holidays", scheduling?.HolidayCount, "/setup/scheduling/holidays", now,
                    unit: "Holidays",
                    tooltip: "Holiday records available in the current academic scheduling scope."),
                Kpi("rooms-occupied", "Rooms Occupied", timetable?.RoomsScheduledCount, "/setup/scheduling/rooms", now,
                    unit: "Rooms",
                    tooltip: "Rooms with scheduled periods in published/locked operational timetables."),
            ],
            QuickLinks =
            [
                Link("Attendance Recovery", "/setup/attendance-recovery"),
                Link("Reports", "/reports"),
                Link("Timetables", "/setup/scheduling/timetables"),
            ]
        };
    }

    private static CommandCenterSectionDto BuildTimetableOperations(
        DTOs.Scheduling.SchedulingDashboardDto? scheduling,
        DTOs.Scheduling.TimetableDashboardDto? timetable,
        DTOs.Scheduling.TimetableGovernanceDashboardDto? governance,
        decimal? conflictTotal,
        DTOs.Scheduling.SchedulingReadinessSummaryDto? readiness,
        DateTime now)
    {
        return new CommandCenterSectionDto
        {
            Code = "scheduling",
            Title = "Timetable Operations",
            Icon = "🗓",
            Subtitle = "Governance, publishing, scheduling issues, and optimization — reuse only.",
            Cards =
            [
                Kpi("active-academic-year", "Active Academic Year", scheduling?.AcademicYearCount, "/setup/scheduling/academic-years", now,
                    display: scheduling is null ? "—" : $"{scheduling.AcademicYearCount} active",
                    unit: "Years",
                    tooltip: "Active academic year records in the college catalog."),
                Kpi("active-timetable-version", "Active Timetable Version",
                    governance?.PublishedVersionCount ?? timetable?.LockedCount, "/setup/scheduling/governance/versions", now,
                    unit: "Versions",
                    tooltip: "Published timetable / schedule version count for the current scope."),
                Kpi("draft-timetable-versions", "Draft Timetable Versions",
                    governance?.DraftVersionCount ?? timetable?.DraftTimetableCount, "/setup/scheduling/timetables", now,
                    unit: "Versions",
                    tooltip: "Draft timetable versions still under design (not used for faculty operational attendance lists)."),
                Kpi("pending-timetable-approval", "Pending Timetable Approval", governance?.ApprovalQueueCount,
                    "/setup/scheduling/governance/approvals", now,
                    unit: "Items",
                    status: Severity(governance?.ApprovalQueueCount, 1, 3, 10),
                    tooltip: "Timetable versions awaiting approval."),
                Kpi("scheduling-issues", "Scheduling Issues", conflictTotal, "/setup/scheduling/conflicts/dashboard", now,
                    unit: "Issues",
                    status: Severity((int?)conflictTotal, 1, 5, 15),
                    tooltip: "Faculty/room/student/calendar scheduling issues requiring attention."),
                Kpi("optimization-suggestions", "Timetable Optimization Suggestions",
                    governance?.PendingReviewsCount ?? readiness?.PendingModules, "/setup/scheduling/optimization/dashboard", now,
                    unit: "Suggestions",
                    tooltip: "Optimization suggestions awaiting review."),
                Kpi("last-published", "Last Published", governance?.RecentlyPublishedCount, "/setup/scheduling/governance/publishing", now,
                    display: governance is null ? "—" : $"{governance.RecentlyPublishedCount} recent",
                    unit: "Publishes",
                    tooltip: "Recent timetable publish activity."),
                Kpi("current-schedule-version", "Current Schedule Version", governance?.PublishedVersionCount,
                    "/setup/scheduling/governance/versions", now,
                    unit: "Versions",
                    tooltip: "Current published schedule version count (governance)."),
            ],
            QuickLinks =
            [
                Link("Scheduling Hub", "/setup/scheduling"),
                Link("Timetable Approval Queue", "/setup/scheduling/governance/approvals"),
                Link("Scheduling Issues", "/setup/scheduling/conflicts/workspace"),
                Link("Optimization Workspace", "/setup/scheduling/optimization/workspace"),
                Link("Governance Dashboard", "/setup/scheduling/governance/dashboard"),
            ]
        };
    }

    private static CommandCenterSectionDto BuildAttendance(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.AttendanceRecovery.AttendanceOperationalAnalyticsDto? analytics,
        DTOs.AttendanceRecovery.EnterpriseOpsDashboardDto? ops,
        DateTime now)
    {
        var success = analytics is null ? (decimal?)null : (decimal)Math.Max(0, 100 - analytics.FailurePercent);
        return new CommandCenterSectionDto
        {
            Code = "attendance",
            Title = "Attendance Operations",
            Icon = "📝",
            Subtitle = "Grouped by Running Sessions → Recognition → Review → Recovery → Completed. Both Legacy and Timetable attendance modes remain supported; AttendanceSessionResolver selects the mode.",
            GroupOrder = ["Running Sessions", "Recognition", "Review", "Recovery", "Completed"],
            Cards =
            [
                Kpi("sessions-running", "Running Sessions", recovery?.ProcessingCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Running Sessions",
                    tooltip: "Attendance sessions currently running / processing."),
                Kpi("recognition-in-progress", "Recognition In Progress", recovery?.ProcessingCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Recognition",
                    tooltip: "AI attendance recognition currently in progress."),
                Kpi("recognition-failed", "Recognition Failed", recovery?.FailedCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Recognition",
                    status: Severity(recovery?.FailedCount, 1, 3, 10),
                    tooltip: "Recognition attempts that failed and may need recovery."),
                Kpi("recognition-success", "Recognition Success", success, "/setup/attendance-recovery", now,
                    display: success is null ? "—" : $"{success:0.#}%",
                    unit: "%", group: "Recognition",
                    tooltip: "Recognition success rate from operational analytics."),
                Kpi("attendance-review-queue", "Attendance Review Queue", recovery?.ReviewPendingCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Review",
                    status: Severity(recovery?.ReviewPendingCount, 1, 5, 15),
                    comparison: ComparisonDelta(recovery?.ReviewPendingCount, null),
                    tooltip: "Sessions awaiting faculty/admin review."),
                Kpi("attendance-recovery-queue", "Attendance Recovery Queue", recovery?.TodayCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Recovery",
                    status: Severity(recovery?.TodayCount, 1, 5, 20),
                    comparison: ComparisonDelta(recovery?.TodayCount, recovery?.YesterdayCount),
                    trend: TrendFromDelta(recovery?.TodayCount, recovery?.YesterdayCount),
                    tooltip: "Today's attendance recovery queue depth."),
                Kpi("faculty-pending-finalization", "Faculty Pending Finalization", recovery?.FinalizationPendingCount, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Recovery",
                    status: Severity(recovery?.FinalizationPendingCount, 1, 5, 15),
                    tooltip: "Sessions waiting for faculty finalization."),
                Kpi("completed-today", "Completed Today", analytics?.SessionsCompleted, "/setup/attendance-recovery", now,
                    unit: "Sessions", group: "Completed",
                    trend: "up",
                    tooltip: "Attendance sessions completed today."),
                Kpi("avg-processing-time", "Average Processing Time",
                    (decimal?)(analytics?.AverageRecognitionMinutes ?? ops?.AverageReviewTimeMinutes), "/setup/attendance-recovery", now,
                    display: FormatMinutes(analytics?.AverageRecognitionMinutes ?? ops?.AverageReviewTimeMinutes),
                    unit: "min", group: "Completed",
                    tooltip: "Average recognition / review processing time."),
                Kpi("attendance-sla", "Attendance SLA", null, "/setup/attendance-recovery", now,
                    display: ops?.AverageReviewTimeMinutes is null ? "—" : $"{ops.AverageReviewTimeMinutes:0.#} min avg",
                    unit: "min", group: "Completed",
                    tooltip: "Operational review-time signal used as attendance SLA indicator."),
            ],
            QuickLinks = [Link("Attendance Recovery Dashboard", "/setup/attendance-recovery"), Link("Take Attendance", "/attendance")]
        };
    }

    private static CommandCenterSectionDto BuildAcademic(DTOs.Scheduling.SchedulingDashboardDto? scheduling, DateTime now) =>
        new()
        {
            Code = "academic",
            Title = "Academic Resources",
            Icon = "🎓",
            Subtitle = "Catalog resources — each card opens the related Catalog module.",
            Cards =
            [
                Kpi("students", "Students", null, "/students", now, display: "—", unit: "Students",
                    reportPath: "/reports", trend: "flat",
                    tooltip: "Student roster lives in Catalog / Students; open module for details."),
                Kpi("faculty", "Faculty", scheduling?.FacultyCount, "/setup/staff", now, unit: "Faculty",
                    trend: scheduling?.FacultyCount is > 0 ? "flat" : null,
                    tooltip: "Faculty / staff records in the college catalog."),
                Kpi("departments", "Departments", scheduling?.DepartmentCount, "/setup/departments", now, unit: "Departments",
                    tooltip: "Academic departments."),
                Kpi("courses", "Courses", null, "/setup/courses", now, display: "—", unit: "Courses",
                    tooltip: "Open Courses in Catalog."),
                Kpi("groups", "Groups", null, "/setup/groups", now, display: "—", unit: "Groups",
                    tooltip: "Open Groups in Catalog."),
                Kpi("subjects", "Subjects", scheduling?.SubjectCount, "/setup/subjects", now, unit: "Subjects",
                    tooltip: "Subjects in the college catalog."),
                Kpi("buildings", "Buildings", scheduling?.BuildingCount, "/setup/scheduling/campuses", now, unit: "Buildings",
                    tooltip: "Campus buildings."),
                Kpi("rooms", "Rooms", scheduling?.RoomCount, "/setup/scheduling/rooms", now, unit: "Rooms",
                    tooltip: "Teaching rooms."),
                Kpi("laboratories", "Laboratories", null, "/setup/scheduling/rooms", now, display: "—", unit: "Labs",
                    tooltip: "Laboratory rooms are managed under Rooms (filter in Catalog)."),
                Kpi("room-utilization", "Room Utilization", scheduling?.TotalRoomCapacity, "/setup/scheduling/rooms", now,
                    display: scheduling is null ? "—" : $"Cap {scheduling.TotalRoomCapacity}",
                    unit: "Capacity",
                    trend: scheduling?.RoomFeatureCoveragePercent is >= 50 ? "up" : "flat",
                    comparison: scheduling is null ? null : $"Feature coverage {scheduling.RoomFeatureCoveragePercent:0.#}%",
                    tooltip: "Room capacity / feature coverage composed from scheduling dashboard."),
                Kpi("faculty-allocation", "Faculty Allocation", scheduling?.FacultyWorkloadCount, "/setup/scheduling/faculty-workloads", now,
                    unit: "Allocations",
                    tooltip: "Faculty workload allocations."),
            ],
            QuickLinks =
            [
                Link("Catalog", "/setup"),
                Link("Departments", "/setup/departments"),
                Link("Staff", "/setup/staff"),
                Link("Rooms", "/setup/scheduling/rooms"),
                Link("Students", "/students"),
            ]
        };

    private static CommandCenterSectionDto BuildSystemHealth(EnterpriseHealthCenterDto? health, DateTime now)
    {
        var components = health?.Components ?? [];
        DashboardWidgetDto HealthCard(string code, string title, string match) =>
            HealthKpi(code, title, MatchComponent(components, match), health?.OverallStatus, now);

        return new CommandCenterSectionDto
        {
            Code = "health",
            Title = "College System Health",
            Icon = "🖥",
            Subtitle = "Business status labels: Healthy, Warning, Critical. Open Health Center for details.",
            Cards =
            [
                HealthKpi("api-status", "API", null, health?.OverallStatus ?? "Green", now,
                    message: "API composition endpoint responding for Command Center."),
                HealthCard("database-status", "Database", "database"),
                HealthCard("signalr-status", "SignalR", "signalr"),
                HealthCard("recognition-engine", "Recognition Engine", "recognition"),
                HealthCard("notification-service", "Notification Service", "signalr"),
                HealthCard("background-jobs", "Background Jobs", "background"),
                HealthCard("storage-status", "Storage", "storage"),
                HealthCard("scheduler-status", "Scheduler", "scheduling"),
                Kpi("last-heartbeat", "Last Heartbeat", null, "/dashboard/health", now,
                    display: (health?.GeneratedUtc ?? now).ToLocalTime().ToString("HH:mm:ss"),
                    unit: "Local",
                    tooltip: "Last health composition heartbeat."),
                Kpi("last-incident", "Last Incident", null, "/dashboard/health", now,
                    display: components.Any(c => c.Status is "Red" or "Yellow")
                        ? components.First(c => c.Status is "Red" or "Yellow").Title
                        : "None",
                    tooltip: "Most recent non-healthy component signal."),
                Kpi("uptime", "Uptime", null, "/dashboard/health", now,
                    display: HealthLabel(health?.OverallStatus ?? "Green"),
                    status: health?.OverallStatus ?? "Green",
                    statusLabel: HealthLabel(health?.OverallStatus ?? "Green"),
                    tooltip: "Overall college system health from composition monitor."),
            ],
            QuickLinks = [Link("Health Center", "/dashboard/health")]
        };
    }

    private static IReadOnlyList<CommandCenterActionBannerDto> BuildActionBanners(IReadOnlyList<DashboardWidgetDto> attentionCards)
    {
        var banners = new List<CommandCenterActionBannerDto>();
        foreach (var card in attentionCards.Where(c => (c.Value ?? 0) > 0 && c.Status is "Red" or "Orange" or "Yellow"))
        {
            banners.Add(new CommandCenterActionBannerDto
            {
                Code = $"banner-{card.Code}",
                Message = $"{card.DisplayValue ?? card.Value?.ToString("0") ?? "0"} {card.Unit ?? "items"} — {card.Title}.",
                Path = card.Path ?? "/dashboard",
                ActionLabel = card.SuggestedAction ?? card.ActionLabel ?? "View Details",
                Severity = card.Status ?? "Yellow",
                RequiredPermission = card.Code.Contains("timetable", StringComparison.OrdinalIgnoreCase)
                    || card.Code.Contains("scheduling", StringComparison.OrdinalIgnoreCase)
                    || card.Code.Contains("optimization", StringComparison.OrdinalIgnoreCase)
                        ? PermissionKeys.SchedulingManage
                        : card.Code.Contains("attendance", StringComparison.OrdinalIgnoreCase)
                          || card.Code.Contains("recognition", StringComparison.OrdinalIgnoreCase)
                            ? PermissionKeys.AttendanceManage
                            : PermissionKeys.DashboardView
            });
        }

        return banners.Take(5).ToList();
    }

    private static IReadOnlyList<CommandCenterQuickActionDto> BuildQuickActions() =>
    [
        new() { Code = "take-attendance", Label = "Take Attendance", Path = "/attendance", Shortcut = "A", RequiredPermission = PermissionKeys.AttendanceManage, Primary = true },
        new() { Code = "review-attendance", Label = "Review Attendance", Path = "/setup/attendance-recovery", Shortcut = "R", RequiredPermission = PermissionKeys.AttendanceManage },
        new() { Code = "attendance-recovery", Label = "Attendance Recovery", Path = "/setup/attendance-recovery", Shortcut = "V", RequiredPermission = PermissionKeys.AttendanceManage },
        new() { Code = "create-timetable", Label = "Create Timetable", Path = "/setup/scheduling/timetables", Shortcut = "T", RequiredPermission = PermissionKeys.SchedulingTimetableManage },
        new() { Code = "approve-timetable", Label = "Approve Timetable", Path = "/setup/scheduling/governance/approvals", Shortcut = "P", RequiredPermission = PermissionKeys.SchedulingApprove },
        new() { Code = "run-optimization", Label = "Run Optimization", Path = "/setup/scheduling/optimization/workspace", Shortcut = "O", RequiredPermission = PermissionKeys.SchedulingManage },
        new() { Code = "reports", Label = "Reports", Path = "/reports", Shortcut = "G", RequiredPermission = PermissionKeys.ReportsView },
        new() { Code = "notifications", Label = "Notifications", Path = "/dashboard/notifications", Shortcut = "N", RequiredPermission = PermissionKeys.DashboardView },
    ];

    private static DashboardWidgetDto Attention(
        string code,
        string title,
        decimal? value,
        string unit,
        string path,
        string? reportPath,
        string tooltip,
        string suggestedAction,
        string estimatedImpact,
        string? comparison,
        string? trend,
        DateTime now,
        string severity) =>
        new()
        {
            Code = code,
            Title = title,
            Kind = "Kpi",
            Category = "Attention",
            Value = value,
            DisplayValue = value?.ToString("0") ?? "0",
            Unit = unit,
            Status = severity,
            StatusLabel = SeverityBusinessLabel(severity),
            Path = path,
            ReportPath = reportPath,
            Tooltip = tooltip,
            LastUpdatedUtc = now,
            Trend = trend,
            Comparison = comparison,
            SuggestedAction = suggestedAction,
            EstimatedImpact = estimatedImpact,
            ActionLabel = "View Details",
            Configurable = true,
            Visible = true,
            SortOrder = 0
        };

    private static DashboardWidgetDto Kpi(
        string code,
        string title,
        decimal? value,
        string path,
        DateTime now,
        string? display = null,
        string? status = null,
        string? statusLabel = null,
        string? trend = null,
        string? unit = null,
        string? comparison = null,
        string? group = null,
        string? tooltip = null,
        string? reportPath = null,
        string? actionLabel = null) =>
        new()
        {
            Code = code,
            Title = title,
            Kind = "Kpi",
            Category = "Operations",
            Value = value,
            DisplayValue = display ?? value?.ToString("0.#") ?? "—",
            Unit = unit,
            Status = status ?? "Green",
            StatusLabel = statusLabel ?? (status is null ? null : SeverityBusinessLabel(status)),
            Path = path,
            ReportPath = reportPath,
            Tooltip = tooltip ?? title,
            LastUpdatedUtc = now,
            Trend = trend,
            Comparison = comparison,
            Group = group,
            ActionLabel = actionLabel ?? "View Details",
            Configurable = true,
            Visible = true,
            SortOrder = 0
        };

    private static DashboardWidgetDto HealthKpi(
        string code,
        string title,
        HealthTrafficLightDto? component,
        string? fallbackStatus,
        DateTime now,
        string? message = null)
    {
        var status = component?.Status ?? fallbackStatus ?? "Yellow";
        return new DashboardWidgetDto
        {
            Code = code,
            Title = title,
            Kind = "Status",
            Category = "Health",
            DisplayValue = HealthLabel(status),
            Status = status,
            StatusLabel = HealthLabel(status),
            Path = "/dashboard/health",
            Tooltip = message ?? component?.Message ?? title,
            LastUpdatedUtc = now,
            ActionLabel = "Open Health Center",
            EstimatedImpact = status == "Green" ? "No action required." : "Investigate in Health Center.",
            Configurable = true,
            Visible = true,
            SortOrder = 0
        };
    }

    private static HealthTrafficLightDto? MatchComponent(IReadOnlyList<HealthTrafficLightDto> components, string match) =>
        components.FirstOrDefault(c => c.Code.Contains(match, StringComparison.OrdinalIgnoreCase)
                                       || c.Title.Contains(match, StringComparison.OrdinalIgnoreCase));

    private static QuickLinkDto Link(string label, string path) => new() { Label = label, Path = path };

    private static string Severity(int? count, int warn, int orange, int red)
    {
        var n = count ?? 0;
        if (n >= red) return "Red";
        if (n >= orange) return "Orange";
        if (n >= warn) return "Yellow";
        return "Green";
    }

    private static string Severity(decimal? count, int warn, int orange, int red) =>
        Severity((int?)count, warn, orange, red);

    private static int SeverityRank(string? status) => status switch
    {
        "Red" => 0,
        "Orange" => 1,
        "Yellow" => 2,
        "Info" => 3,
        "Green" => 4,
        _ => 5
    };

    private static string SeverityBusinessLabel(string? status) => status switch
    {
        "Red" => "Critical",
        "Orange" => "High",
        "Yellow" => "Medium",
        "Info" => "Information",
        "Green" => "Information",
        _ => "Information"
    };

    private static string HealthLabel(string? status) => status switch
    {
        "Green" => "Healthy",
        "Yellow" => "Warning",
        "Orange" => "Warning",
        "Red" => "Critical",
        _ => status ?? "—"
    };

    private static decimal? ConflictTotal(DTOs.Scheduling.ConflictDashboardDto? conflicts) =>
        conflicts is null
            ? null
            : conflicts.FacultyConflicts + conflicts.RoomConflicts + conflicts.StudentConflicts + conflicts.CalendarConflicts;

    private static string? ComparisonDelta(int? current, int? previous)
    {
        if (current is null || previous is null) return null;
        var delta = current.Value - previous.Value;
        if (delta == 0) return "No change vs yesterday";
        return delta > 0 ? $"+{delta} since yesterday" : $"{delta} since yesterday";
    }

    private static string? TrendFromDelta(int? current, int? previous)
    {
        if (current is null || previous is null) return null;
        if (current > previous) return "up";
        if (current < previous) return "down";
        return "flat";
    }

    private static string EstimatePeriodLabel(TimeSpan localTime)
    {
        // Composition-only indicative label for operators; not used by attendance mode selection.
        var minutes = localTime.TotalMinutes;
        if (minutes < 9 * 60) return "Before periods";
        if (minutes < 9 * 60 + 50) return "Period 1";
        if (minutes < 10 * 60 + 40) return "Period 2";
        if (minutes < 11 * 60 + 30) return "Period 3";
        if (minutes < 12 * 60 + 20) return "Period 4";
        if (minutes < 13 * 60 + 10) return "Period 5";
        if (minutes < 14 * 60 + 20) return "Period 6";
        return "After periods";
    }

    private static string FormatMinutes(double? minutes) =>
        minutes is null ? "—" : minutes < 1 ? $"{minutes * 60:0} s" : $"{minutes:0.#} min";

    private static DashboardWidgetDto CloneWithOrder(DashboardWidgetDto c, int order) =>
        new()
        {
            Code = c.Code,
            Title = c.Title,
            Kind = c.Kind,
            Category = c.Category,
            Value = c.Value,
            DisplayValue = c.DisplayValue,
            Unit = c.Unit,
            Status = c.Status,
            StatusLabel = c.StatusLabel,
            Path = c.Path,
            ReportPath = c.ReportPath,
            Tooltip = c.Tooltip,
            LastUpdatedUtc = c.LastUpdatedUtc,
            Trend = c.Trend,
            Comparison = c.Comparison,
            SuggestedAction = c.SuggestedAction,
            EstimatedImpact = c.EstimatedImpact,
            ActionLabel = c.ActionLabel,
            Group = c.Group,
            RequiredPermission = c.RequiredPermission,
            Configurable = c.Configurable,
            Visible = c.Visible,
            SortOrder = order
        };

    private static async Task<T?> Safe<T>(Func<Task<T>> factory) where T : class
    {
        try { return await factory(); }
        catch { return null; }
    }
}
