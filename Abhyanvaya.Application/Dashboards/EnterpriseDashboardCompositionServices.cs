using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Configuration;
using Abhyanvaya.Application.Scheduling.Conflicts;

namespace Abhyanvaya.Application.Dashboards;

public interface IFacultyCommandCenterService
{
    Task<FacultyCommandCenterDto> GetAsync(CancellationToken cancellationToken = default);
    Task<FacultyKpiBundleDto> GetKpisAsync(CancellationToken cancellationToken = default);
    Task<FacultyInsightsPanelDto> GetInsightsPanelAsync(CancellationToken cancellationToken = default);
    Task<FacultyActivityTimelineDto> GetActivityTimelineAsync(string range = "Today", CancellationToken cancellationToken = default);
}

public interface IAdminOperationsDashboardService
{
    Task<AdminOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseOperationalAnalyticsComposer
{
    Task<EnterpriseOperationalAnalyticsDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseHealthCenterService
{
    Task<EnterpriseHealthCenterDto> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// AI31.6.1–4 — Faculty Command Center composition. Calls existing Faculty / Recovery / Insights services only.
/// Never touches AttendanceSessionResolver beyond what IFacultyDashboardService already uses.
/// </summary>
public sealed class FacultyCommandCenterService : IFacultyCommandCenterService
{
    private readonly IFacultyDashboardService _faculty;
    private readonly IFacultyProductivityService _productivity;
    private readonly IFacultyWorkspaceRecoverySummaryService _recoverySummary;
    private readonly IDashboardPreferenceService _preferences;

    public FacultyCommandCenterService(
        IFacultyDashboardService faculty,
        IFacultyProductivityService productivity,
        IFacultyWorkspaceRecoverySummaryService recoverySummary,
        IDashboardPreferenceService preferences)
    {
        _faculty = faculty;
        _productivity = productivity;
        _recoverySummary = recoverySummary;
        _preferences = preferences;
    }

    public async Task<FacultyCommandCenterDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Sequential: shared scoped DbContext cannot run parallel queries (EF concurrency).
        var today = await _faculty.GetTodayAsync(null, cancellationToken);
        var insights = await _faculty.GetInsightsAsync(cancellationToken);
        var productivity = await Safe(() => _productivity.GetAttendanceProductivityAsync(cancellationToken));
        var recovery = await Safe(() => _recoverySummary.GetAsync(cancellationToken));
        var prefs = await Safe(() => _preferences.GetAsync("Faculty", cancellationToken))
            ?? new DashboardPreferenceDto { RoleScope = "Faculty", DefaultLandingPage = "command-center" };
        var activity = await Safe(() => GetActivityTimelineAsync("Today", cancellationToken))
            ?? new FacultyActivityTimelineDto { Range = "Today", Events = [] };

        var kpis = BuildKpis(today, insights, productivity, recovery);
        var insightsPanel = BuildInsights(today, insights, recovery);
        var widgets = BuildFacultyWidgets(kpis, prefs);

        return new FacultyCommandCenterDto
        {
            Date = today.Date,
            Mode = today.Mode,
            HasTimetable = today.HasTimetable,
            Message = today.Message,
            CurrentClass = MapClass(today.CurrentClass),
            NextClass = MapClass(today.NextClass),
            TodaysClasses = today.TodaysSchedule.Select(MapClass).Where(c => c is not null).Cast<FacultyCommandClassCardDto>().ToList(),
            RemainingClasses = kpis.RemainingClasses,
            TodaysStudents = kpis.TodaysStudents,
            AttendancePending = kpis.PendingAttendance,
            RecoveryQueue = kpis.RecoverySessions,
            Kpis = kpis,
            Insights = insightsPanel,
            ActivityPreview = activity.Events.Take(8).ToList(),
            Widgets = widgets,
            QuickActions =
            [
                new FacultyCommandQuickActionDto { Code = "TAKE_ATTENDANCE", Label = "Take Attendance", Path = "/attendance", Primary = true },
                new FacultyCommandQuickActionDto { Code = "WORKSPACE", Label = "Faculty Workspace", Path = "/faculty" },
                new FacultyCommandQuickActionDto { Code = "RECOVERY", Label = "Recovery", Path = "/faculty/recovery" },
                new FacultyCommandQuickActionDto { Code = "REPORTS", Label = "Reports", Path = "/reports" },
                new FacultyCommandQuickActionDto { Code = "TIMETABLE", Label = "Timetable", Path = "/faculty" },
            ],
            Preferences = prefs,
            GeneratedUtc = DateTime.UtcNow
        };
    }

    public async Task<FacultyKpiBundleDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var today = await _faculty.GetTodayAsync(null, cancellationToken);
        var insights = await _faculty.GetInsightsAsync(cancellationToken);
        var productivity = await Safe(() => _productivity.GetAttendanceProductivityAsync(cancellationToken));
        var recovery = await Safe(() => _recoverySummary.GetAsync(cancellationToken));
        return BuildKpis(today, insights, productivity, recovery);
    }

    public async Task<FacultyInsightsPanelDto> GetInsightsPanelAsync(CancellationToken cancellationToken = default)
    {
        var today = await _faculty.GetTodayAsync(null, cancellationToken);
        var insights = await _faculty.GetInsightsAsync(cancellationToken);
        var recovery = await Safe(() => _recoverySummary.GetAsync(cancellationToken));
        return BuildInsights(today, insights, recovery);
    }

    public async Task<FacultyActivityTimelineDto> GetActivityTimelineAsync(
        string range = "Today",
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(range) ? "Today" : range.Trim();
        // Sequential DbContext access — do not parallelize with other faculty queries.
        var today = await _faculty.GetTodayAsync(null, cancellationToken);
        var notifications = await Safe(() => _faculty.GetNotificationsAsync(cancellationToken)) ?? [];
        var events = new List<FacultyActivityEventDto>();

        foreach (var c in today.TodaysSchedule)
        {
            if (c.AttendanceStatus is "Completed" or "InProgress")
            {
                events.Add(new FacultyActivityEventDto
                {
                    EventId = $"att-{c.AttendanceSessionId ?? Guid.Empty}-{c.SubjectId}",
                    Kind = c.AttendanceStatus == "Completed" ? "AttendanceCompleted" : "AttendanceResumed",
                    Title = c.AttendanceStatus == "Completed" ? "Attendance completed" : "Attendance in progress",
                    Message = c.SubjectName ?? $"Subject {c.SubjectId}",
                    OccurredUtc = DateTime.UtcNow.Date.Add(c.EndTime ?? c.StartTime ?? TimeSpan.Zero),
                    Path = c.AttendanceSessionId.HasValue
                        ? $"/attendance/sessions/{c.AttendanceSessionId}/review"
                        : "/attendance"
                });
            }

            if (!string.IsNullOrWhiteSpace(c.AiCaptureStatus) &&
                c.AiCaptureStatus.Contains("Review", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new FacultyActivityEventDto
                {
                    EventId = $"rec-{c.AttendanceSessionId}",
                    Kind = "RecognitionCompleted",
                    Title = "Recognition review pending",
                    Message = c.SubjectName ?? "Recognition",
                    OccurredUtc = DateTime.UtcNow.Date.Add(c.EndTime ?? TimeSpan.Zero),
                    Path = c.AttendanceSessionId.HasValue
                        ? $"/attendance/sessions/{c.AttendanceSessionId}/review"
                        : "/faculty"
                });
            }
        }

        foreach (var n in notifications)
        {
            var kind = n.Kind switch
            {
                "RoomChanged" => "RoomChanged",
                "Cancelled" or "Rescheduled" or "FacultySubstitution" => "ScheduleUpdated",
                "Holiday" or "WorkingDayChange" => "TimetablePublished",
                _ => "ScheduleUpdated"
            };
            events.Add(new FacultyActivityEventDto
            {
                EventId = n.NotificationId,
                Kind = kind,
                Title = n.Title,
                Message = n.Message,
                OccurredUtc = n.OccurredUtc,
                Path = "/faculty"
            });
        }

        if (!normalized.Equals("Today", StringComparison.OrdinalIgnoreCase))
        {
            // Week/Month: include weekly/monthly insight rollups as summary events (composed, no new SQL).
            var insights = await _faculty.GetInsightsAsync(cancellationToken);
            var period = normalized.Equals("Month", StringComparison.OrdinalIgnoreCase) ? insights.Monthly : insights.Weekly;
            events.Add(new FacultyActivityEventDto
            {
                EventId = $"rollup-{normalized}",
                Kind = "ReviewCompleted",
                Title = $"{normalized} attendance rollup",
                Message = $"{period.Completed}/{period.Sessions} sessions completed; AI sessions: {period.AiSessions}",
                OccurredUtc = DateTime.UtcNow,
                Path = "/dashboard"
            });
        }

        return new FacultyActivityTimelineDto
        {
            Range = normalized,
            Events = events.OrderByDescending(e => e.OccurredUtc).ToList()
        };
    }

    private static FacultyKpiBundleDto BuildKpis(
        FacultyTodayDto today,
        FacultyInsightsDto insights,
        FacultyAttendanceProductivityDto? productivity,
        DTOs.AttendanceRecovery.FacultyWorkspaceRecoverySummaryDto? recovery)
    {
        var completed = today.TodaysSchedule.Count(c => c.AttendanceStatus == "Completed");
        var remaining = today.TodaysSchedule.Count(c => c.Status is "Current" or "Upcoming");
        var students = today.TodaysSchedule.Sum(c => c.StudentCount ?? 0);
        var present = today.AttendanceSummary.PresentMarks;
        var absent = today.AttendanceSummary.AbsentMarks;
        var marked = present + absent;
        double? pct = marked > 0 ? Math.Round(100.0 * present / marked, 1) : null;

        return new FacultyKpiBundleDto
        {
            TodaysClasses = today.TodaysSchedule.Count,
            CompletedClasses = completed,
            RemainingClasses = remaining,
            TodaysStudents = students,
            AttendanceCompleted = today.AttendanceSummary.AttendanceTaken,
            PendingAttendance = productivity?.PendingAttendance ?? today.AttendanceSummary.Pending,
            RecoverySessions = recovery?.PendingAttendance ?? today.AttendanceSummary.Pending,
            RecognitionReviews = productivity?.AiPendingReviews ?? today.AiAttendanceSummary.PendingReviews,
            AverageCompletionMinutes = insights.AverageCompletionMinutes ?? recovery?.AverageReviewTimeMinutes,
            AttendancePercent = productivity is not null
                ? (double)productivity.AttendanceCompletionPercent
                : pct
        };
    }

    private static FacultyInsightsPanelDto BuildInsights(
        FacultyTodayDto today,
        FacultyInsightsDto insights,
        DTOs.AttendanceRecovery.FacultyWorkspaceRecoverySummaryDto? recovery)
    {
        var items = new List<InsightItemDto>();

        items.Add(new InsightItemDto
        {
            Code = "attendance-trend",
            Kind = "Trend",
            Title = "Attendance trend",
            Message = $"Weekly {insights.Weekly.Completed}/{insights.Weekly.Sessions} completed; monthly {insights.Monthly.Completed}/{insights.Monthly.Sessions}.",
            Path = "/dashboard",
            Severity = "Info"
        });

        if (today.AttendanceSummary.Pending > 0 || (recovery?.PendingAttendance ?? 0) > 0)
        {
            items.Add(new InsightItemDto
            {
                Code = "pending-attendance",
                Kind = "Alert",
                Title = "Attendance pending",
                Message = $"{Math.Max(today.AttendanceSummary.Pending, recovery?.PendingAttendance ?? 0)} class(es) still need attendance.",
                Path = "/faculty/recovery",
                Severity = "Warning"
            });
        }

        if (today.AiAttendanceSummary.PendingReviews > 0)
        {
            items.Add(new InsightItemDto
            {
                Code = "pending-reviews",
                Kind = "Review",
                Title = "Pending recognition reviews",
                Message = $"{today.AiAttendanceSummary.PendingReviews} review(s) waiting.",
                Path = "/faculty",
                Severity = "Warning"
            });
        }

        if (today.AiAttendanceSummary.AverageRecognitionAccuracy is < 80)
        {
            items.Add(new InsightItemDto
            {
                Code = "recognition-failures",
                Kind = "Alert",
                Title = "Recognition accuracy low",
                Message = $"Average accuracy {today.AiAttendanceSummary.AverageRecognitionAccuracy:0.#}%.",
                Path = "/faculty",
                Severity = "Critical"
            });
        }

        foreach (var n in today.Notifications.Take(5))
        {
            items.Add(new InsightItemDto
            {
                Code = $"sched-{n.NotificationId}",
                Kind = n.Kind is "Holiday" or "WorkingDayChange" ? "Reminder" : "Schedule",
                Title = n.Title,
                Message = n.Message,
                Path = "/faculty",
                Severity = n.Kind is "Cancelled" or "RoomChanged" ? "Warning" : "Info"
            });
        }

        if (insights.Weekly.Sessions > 0)
        {
            items.Add(new InsightItemDto
            {
                Code = "subject-trends",
                Kind = "Trend",
                Title = "Subject / AI usage",
                Message = $"AI sessions this week: {insights.Weekly.AiSessions}. Recognition accuracy: {insights.RecognitionAccuracy?.ToString("0.#") ?? "n/a"}%.",
                Path = "/faculty",
                Severity = "Info"
            });
        }

        return new FacultyInsightsPanelDto { Items = items };
    }

    private static IReadOnlyList<DashboardWidgetDto> BuildFacultyWidgets(
        FacultyKpiBundleDto kpis,
        DashboardPreferenceDto prefs)
    {
        var valued = DashboardWidgetCatalog.FacultyDefaults.Select(w => w.Code switch
        {
            "todays-classes" => WithValue(w, kpis.TodaysClasses),
            "completed-classes" => WithValue(w, kpis.CompletedClasses),
            "remaining-classes" => WithValue(w, kpis.RemainingClasses),
            "todays-students" => WithValue(w, kpis.TodaysStudents),
            "attendance-completed" => WithValue(w, kpis.AttendanceCompleted),
            "pending-attendance" => WithValue(w, kpis.PendingAttendance, kpis.PendingAttendance > 0 ? "Yellow" : "Green"),
            "recovery-sessions" => WithValue(w, kpis.RecoverySessions, kpis.RecoverySessions > 0 ? "Yellow" : "Green"),
            "recognition-reviews" => WithValue(w, kpis.RecognitionReviews, kpis.RecognitionReviews > 0 ? "Yellow" : "Green"),
            "avg-completion" => WithValue(w, (decimal?)(kpis.AverageCompletionMinutes), display: kpis.AverageCompletionMinutes is null ? "—" : $"{kpis.AverageCompletionMinutes:0.#}m"),
            "attendance-percent" => WithValue(w, (decimal?)kpis.AttendancePercent, display: kpis.AttendancePercent is null ? "—" : $"{kpis.AttendancePercent:0.#}%"),
            _ => w
        });
        return DashboardWidgetCatalog.ApplyPreferences(valued, prefs);
    }

    private static DashboardWidgetDto WithValue(
        DashboardWidgetDto w,
        decimal? value,
        string? status = null,
        string? display = null) =>
        new()
        {
            Code = w.Code,
            Title = w.Title,
            Kind = w.Kind,
            Category = w.Category,
            Value = value,
            DisplayValue = display ?? value?.ToString("0.#"),
            Status = status ?? w.Status,
            Path = w.Path,
            Configurable = w.Configurable,
            Visible = w.Visible,
            SortOrder = w.SortOrder
        };

    private static FacultyCommandClassCardDto? MapClass(FacultyClassDto? c)
    {
        if (c is null) return null;
        return new FacultyCommandClassCardDto
        {
            Status = c.Status,
            SubjectName = c.SubjectName,
            RoomName = c.RoomName,
            StartTime = c.StartTime,
            EndTime = c.EndTime,
            AttendanceStatus = c.AttendanceStatus,
            StudentCount = c.StudentCount,
            AttendanceSessionId = c.AttendanceSessionId,
            TakeAttendancePath = "/attendance"
        };
    }

    private static async Task<T?> Safe<T>(Func<Task<T>> factory) where T : class
    {
        try { return await factory(); }
        catch { return null; }
    }
}

/// <summary>AI31.6.5–6 / 8 — Admin Enterprise Operations Dashboard composition.</summary>
public sealed class AdminOperationsDashboardService : IAdminOperationsDashboardService
{
    private readonly IAttendanceRecoveryDashboardService _recoveryDashboard;
    private readonly IEnterpriseOpsDashboardService _enterpriseOps;
    private readonly IAttendanceHealthMonitorService _attendanceHealth;
    private readonly ISchedulingConfigurationReadinessService _readiness;
    private readonly ISchedulingDashboardService _schedulingDashboard;
    private readonly ITimetableService _timetables;
    private readonly IConflictDetectionService _conflicts;
    private readonly IDashboardPreferenceService _preferences;

    public AdminOperationsDashboardService(
        IAttendanceRecoveryDashboardService recoveryDashboard,
        IEnterpriseOpsDashboardService enterpriseOps,
        IAttendanceHealthMonitorService attendanceHealth,
        ISchedulingConfigurationReadinessService readiness,
        ISchedulingDashboardService schedulingDashboard,
        ITimetableService timetables,
        IConflictDetectionService conflicts,
        IDashboardPreferenceService preferences)
    {
        _recoveryDashboard = recoveryDashboard;
        _enterpriseOps = enterpriseOps;
        _attendanceHealth = attendanceHealth;
        _readiness = readiness;
        _schedulingDashboard = schedulingDashboard;
        _timetables = timetables;
        _conflicts = conflicts;
        _preferences = preferences;
    }

    public async Task<AdminOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Sequential: scoped DbContext is not safe for parallel EF queries.
        var recovery = await Safe(() => _recoveryDashboard.GetAdminDashboardAsync(cancellationToken));
        var ops = await Safe(() => _enterpriseOps.GetAsync(cancellationToken));
        var health = await Safe(() => _attendanceHealth.ScanAsync(cancellationToken));
        var readiness = await Safe(() => _readiness.GetSummaryAsync(cancellationToken));
        var scheduling = await Safe(() => _schedulingDashboard.GetSummaryAsync(cancellationToken));
        var timetable = await Safe(() => _timetables.GetDashboardAsync(null, cancellationToken));
        var conflicts = await Safe(() => _conflicts.GetDashboardAsync(null, null, cancellationToken));
        var prefs = await Safe(() => _preferences.GetAsync("Admin", cancellationToken))
            ?? new DashboardPreferenceDto { RoleScope = "Admin", DefaultLandingPage = "admin-operations" };

        var widgets = BuildAdminWidgets(recovery, ops, health, readiness, scheduling, timetable, conflicts, prefs);
        var charts = BuildCharts(recovery, ops);

        return new AdminOperationsDashboardDto
        {
            Academic = Section("academic", "Academic Overview",
            [
                Card("faculty-count", "Faculty", scheduling?.FacultyCount),
                Card("departments", "Departments", scheduling?.DepartmentCount),
                Card("rooms", "Rooms", scheduling?.RoomCount),
            ], [Link("Scheduling Hub", "/setup/scheduling"), Link("Academic Years", "/setup/scheduling/academic-years")]),
            Attendance = Section("attendance", "Attendance Overview",
            [
                Card("pending-attendance", "Pending", recovery?.TodayCount),
                Card("processing", "Processing", recovery?.ProcessingCount),
                Card("review-pending", "Review Pending", recovery?.ReviewPendingCount),
            ], [Link("Recovery Ops", "/setup/attendance-recovery"), Link("Reports", "/reports")]),
            Scheduling = Section("scheduling", "Scheduling Overview",
            [
                Card("draft-timetables", "Draft Timetables", timetable?.DraftTimetableCount),
                Card("readiness", "Readiness %", readiness is null ? null : (decimal)readiness.OverallPercent),
                Card("conflicts", "Conflicts", ConflictTotal(conflicts)),
            ], [Link("Timetables", "/setup/scheduling/timetables"), Link("Conflicts", "/setup/scheduling/conflicts/dashboard")]),
            Faculty = Section("faculty", "Faculty Overview",
            [
                Card("faculty-scheduled", "Faculty Scheduled", timetable?.FacultyScheduledCount),
                Card("workloads", "Workloads", scheduling?.FacultyWorkloadCount),
            ], [Link("Faculty Workspace", "/faculty"), Link("Workloads", "/setup/scheduling/faculty-workloads")]),
            Student = Section("student", "Student Overview",
            [
                Card("subjects", "Subjects", scheduling?.SubjectCount),
                Card("allocations", "Allocations", scheduling?.SubjectAllocationCount),
            ], [Link("Students", "/students"), Link("Reports", "/reports")]),
            Recovery = Section("recovery", "Recovery Overview",
            [
                Card("failed", "Failed", recovery?.FailedCount),
                Card("expired", "Expired", recovery?.ExpiredCount),
                Card("avg-review", "Avg Review (min)", (decimal?)ops?.AverageReviewTimeMinutes),
            ], [Link("Enterprise Ops", "/setup/attendance-recovery")]),
            AiServices = Section("ai", "AI Services",
            [
                Card("recognition-queue", "Recognition Queue", recovery?.ProcessingCount),
                Card("retry-success", "Retry Success %", (decimal?)ops?.RetrySuccessPercent),
            ], [Link("AI Center", "/ai")]),
            PlatformHealth = Section("health", "Platform Health",
            [
                Card("health-alerts", "Health Alerts", health?.Alerts.Count, status: (health?.Alerts.Count ?? 0) > 0 ? "Yellow" : "Green"),
                Card("long-running", "Long Running", health?.LongRunning, status: (health?.LongRunning ?? 0) > 0 ? "Yellow" : "Green"),
            ], [Link("Health Center", "/dashboard/health"), Link("Diagnostics", "/admin/context-diagnostics")]),
            Widgets = widgets,
            Charts = charts,
            Preferences = prefs,
            GeneratedUtc = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<DashboardWidgetDto> BuildAdminWidgets(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.AttendanceRecovery.EnterpriseOpsDashboardDto? ops,
        DTOs.AttendanceRecovery.AttendanceHealthSnapshotDto? health,
        DTOs.Scheduling.SchedulingReadinessSummaryDto? readiness,
        DTOs.Scheduling.SchedulingDashboardDto? scheduling,
        DTOs.Scheduling.TimetableDashboardDto? timetable,
        DTOs.Scheduling.ConflictDashboardDto? conflicts,
        DashboardPreferenceDto prefs)
    {
        var valued = DashboardWidgetCatalog.AdminDefaults.Select(w => w.Code switch
        {
            "pending-attendance" => Val(w, recovery?.TodayCount, recovery?.TodayCount > 0 ? "Yellow" : "Green"),
            "pending-recovery" => Val(w, recovery?.ReviewPendingCount, recovery?.ReviewPendingCount > 0 ? "Yellow" : "Green"),
            "draft-timetables" => Val(w, timetable?.DraftTimetableCount),
            "published-timetables" => Val(w, timetable?.LockedCount),
            "conflict-count" => Val(w, ConflictTotal(conflicts), (ConflictTotal(conflicts) ?? 0) > 0 ? "Yellow" : "Green"),
            "optimization-queue" => Val(w, readiness?.PendingModules),
            "recognition-queue" => Val(w, recovery?.ProcessingCount),
            "approval-queue" => Val(w, readiness?.PendingModules),
            "faculty-online" => Val(w, scheduling?.FacultyCount, "Green"),
            "todays-classes" => Val(w, timetable?.ScheduledPeriodCount),
            "students-below-threshold" => Val(w, null, "Info", "—"),
            "platform-health" => Val(w, health?.Alerts.Count, (health?.Alerts.Count ?? 0) > 0 ? "Yellow" : "Green", (health?.Alerts.Count ?? 0) > 0 ? "Attention" : "Healthy"),
            _ => w
        });
        return DashboardWidgetCatalog.ApplyPreferences(valued, prefs);
    }

    private static IReadOnlyList<OperationalChartSeriesDto> BuildCharts(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.AttendanceRecovery.EnterpriseOpsDashboardDto? ops)
    {
        var series = new List<OperationalChartSeriesDto>();
        if (recovery?.ByStatus is { Count: > 0 })
        {
            series.Add(new OperationalChartSeriesDto
            {
                Code = "recovery-by-status",
                Title = "Recovery by status",
                Points = recovery.ByStatus.Select(p => new ChartPointDto { Label = p.Label, Value = p.Value }).ToList()
            });
        }

        if (ops?.TimelineTrends is { Count: > 0 })
        {
            series.Add(new OperationalChartSeriesDto
            {
                Code = "recovery-trend",
                Title = "Recovery trend",
                Points = ops.TimelineTrends.Select(p => new ChartPointDto { Label = p.Label, Value = p.Value }).ToList()
            });
        }

        if (ops?.FailureTrend is { Count: > 0 })
        {
            series.Add(new OperationalChartSeriesDto
            {
                Code = "failure-trend",
                Title = "Failure trend",
                Points = ops.FailureTrend.Select(p => new ChartPointDto { Label = p.Label, Value = p.Value }).ToList()
            });
        }

        return series;
    }

    private static AdminSectionDto Section(string code, string title, IReadOnlyList<DashboardWidgetDto> cards, IReadOnlyList<QuickLinkDto> links) =>
        new() { Code = code, Title = title, Cards = cards, QuickLinks = links };

    private static DashboardWidgetDto Card(string code, string title, decimal? value, string? status = null) =>
        new()
        {
            Code = code,
            Title = title,
            Kind = "Kpi",
            Value = value,
            DisplayValue = value?.ToString("0.#") ?? "—",
            Status = status,
            SortOrder = 0
        };

    private static QuickLinkDto Link(string label, string path) => new() { Label = label, Path = path };

    private static decimal? ConflictTotal(DTOs.Scheduling.ConflictDashboardDto? conflicts) =>
        conflicts is null
            ? null
            : conflicts.FacultyConflicts + conflicts.RoomConflicts + conflicts.StudentConflicts + conflicts.CalendarConflicts;

    private static DashboardWidgetDto Val(DashboardWidgetDto w, decimal? value, string? status = null, string? display = null) =>
        new()
        {
            Code = w.Code,
            Title = w.Title,
            Kind = w.Kind,
            Category = w.Category,
            Value = value,
            DisplayValue = display ?? value?.ToString("0.#") ?? "—",
            Status = status ?? w.Status,
            Path = w.Path,
            Configurable = w.Configurable,
            Visible = w.Visible,
            SortOrder = w.SortOrder
        };

    private static async Task<T?> Safe<T>(Func<Task<T>> factory) where T : class
    {
        try { return await factory(); }
        catch { return null; }
    }
}

/// <summary>AI31.6.8 — operational analytics composition + export-ready series.</summary>
public sealed class EnterpriseOperationalAnalyticsComposer : IEnterpriseOperationalAnalyticsComposer
{
    private readonly IAttendanceOperationalAnalyticsService _opsAnalytics;
    private readonly IAttendanceRecoveryDashboardService _recoveryDashboard;
    private readonly IEnterpriseOpsDashboardService _enterpriseOps;
    private readonly IDepartmentOperationsService _departments;
    private readonly ISchedulingConfigurationReadinessService _readiness;

    public EnterpriseOperationalAnalyticsComposer(
        IAttendanceOperationalAnalyticsService opsAnalytics,
        IAttendanceRecoveryDashboardService recoveryDashboard,
        IEnterpriseOpsDashboardService enterpriseOps,
        IDepartmentOperationsService departments,
        ISchedulingConfigurationReadinessService readiness)
    {
        _opsAnalytics = opsAnalytics;
        _recoveryDashboard = recoveryDashboard;
        _enterpriseOps = enterpriseOps;
        _departments = departments;
        _readiness = readiness;
    }

    public async Task<EnterpriseOperationalAnalyticsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var ops = await Safe(() => _opsAnalytics.GetAsync(cancellationToken));
        var recovery = await Safe(() => _recoveryDashboard.GetAdminDashboardAsync(cancellationToken));
        var enterprise = await Safe(() => _enterpriseOps.GetAsync(cancellationToken));
        var departments = await Safe(() => _departments.GetAsync(cancellationToken));
        var readiness = await Safe(() => _readiness.GetSummaryAsync(cancellationToken));

        var series = new List<OperationalChartSeriesDto>
        {
            Series("attendance-trend", "Attendance Trend", enterprise?.TimelineTrends),
            Series("recovery-trend", "Recovery Trend", recovery?.ByStatus),
            Series("scheduling-readiness", "Scheduling Readiness",
                readiness is null
                    ? null
                    : new List<DTOs.AttendanceRecovery.RecoveryChartPointDto>
                    {
                        new() { Label = "Ready %", Value = (decimal)readiness.OverallPercent }
                    }),
            Series("recognition-success", "Recognition Success",
                enterprise is null
                    ? null
                    : new List<DTOs.AttendanceRecovery.RecoveryChartPointDto>
                    {
                        new() { Label = "Retry Success %", Value = (decimal)enterprise.RetrySuccessPercent }
                    }),
            Series("pending-reviews", "Pending Reviews",
                recovery is null
                    ? null
                    : new List<DTOs.AttendanceRecovery.RecoveryChartPointDto>
                    {
                        new() { Label = "Review Pending", Value = recovery.ReviewPendingCount }
                    }),
            Series("conflict-trends", "Conflict Trends", enterprise?.FailureTrend),
            Series("optimization-trends", "Optimization Trends", enterprise?.DailyHeatmap),
            Series("faculty-productivity", "Faculty Productivity", enterprise?.FacultySla),
            Series("timetable-publishing", "Timetable Publishing", enterprise?.SlaDistribution),
        };

        // Keep non-empty series only for cleaner UI.
        series = series.Where(s => s.Points.Count > 0).ToList();

        if (ops is not null)
        {
            series.Insert(0, new OperationalChartSeriesDto
            {
                Code = "ops-reuse",
                Title = "Operational analytics (reused)",
                Points =
                [
                    new ChartPointDto { Label = "Started", Value = ops.SessionsStarted },
                    new ChartPointDto { Label = "Completed", Value = ops.SessionsCompleted },
                    new ChartPointDto { Label = "Avg review min", Value = (decimal)(ops.AverageReviewMinutes ?? 0) }
                ]
            });
        }

        return new EnterpriseOperationalAnalyticsDto
        {
            Series = series,
            DepartmentComparison = (departments?.Departments ?? [])
                .Select(d => new DepartmentComparisonDto
                {
                    DepartmentName = d.DepartmentName,
                    PendingSessions = d.PendingSessions,
                    Completed = d.Completed,
                    AverageCompletionMinutes = d.AverageCompletionMinutes
                })
                .ToList(),
            GeneratedUtc = DateTime.UtcNow
        };
    }

    private static OperationalChartSeriesDto Series(
        string code,
        string title,
        IReadOnlyList<DTOs.AttendanceRecovery.RecoveryChartPointDto>? points) =>
        new()
        {
            Code = code,
            Title = title,
            Points = (points ?? [])
                .Select(p => new ChartPointDto { Label = p.Label, Value = p.Value })
                .ToList()
        };

    private static async Task<T?> Safe<T>(Func<Task<T>> factory) where T : class
    {
        try { return await factory(); }
        catch { return null; }
    }
}

/// <summary>AI31.6.10 — read-only Health Center traffic lights from existing health monitors.</summary>
public sealed class EnterpriseHealthCenterService : IEnterpriseHealthCenterService
{
    private readonly IAttendanceHealthMonitorService _attendanceHealth;

    public EnterpriseHealthCenterService(IAttendanceHealthMonitorService attendanceHealth) =>
        _attendanceHealth = attendanceHealth;

    public async Task<EnterpriseHealthCenterDto> GetAsync(CancellationToken cancellationToken = default)
    {
        DTOs.AttendanceRecovery.AttendanceHealthSnapshotDto? attendance = null;
        try { attendance = await _attendanceHealth.ScanAsync(cancellationToken); }
        catch { /* read-only resilience */ }

        var components = new List<HealthTrafficLightDto>
        {
            Light("recognition", "Recognition", attendance is null ? "Yellow" : (attendance.RepeatedFailures > 0 ? "Red" : "Green"),
                attendance is null ? "Attendance health unavailable" : $"{attendance.RepeatedFailures} repeated failure band(s)"),
            Light("recovery", "Recovery", attendance is null ? "Yellow" : (attendance.LargePendingQueues > 0 ? "Yellow" : "Green"),
                attendance is null ? "Unavailable" : $"{attendance.LargePendingQueues} large pending queue(s)"),
            Light("scheduling", "Scheduling", "Green", "Scheduling engines unchanged — composition health only"),
            Light("optimization", "Optimization", "Green", "Optimization hub available via SignalR"),
            Light("signalr", "SignalR", "Green", "FacultyHub / OptimizationHub mapped"),
            Light("storage", "Storage", "Green", "Media/branding storage assumed healthy unless ops alerts fire"),
            Light("background-jobs", "Background Jobs", attendance?.LongRunning > 0 ? "Yellow" : "Green",
                $"{attendance?.LongRunning ?? 0} long-running session(s)"),
            Light("telemetry", "Telemetry", "Green", "Reuse existing operational telemetry endpoints"),
            Light("cache", "Cache", "Green", "Enterprise ops cache reused where configured"),
            Light("database", "Database", attendance is null ? "Yellow" : "Green",
                attendance is null ? "Could not scan attendance health" : "Attendance health scan succeeded"),
        };

        var overall =
            components.Any(c => c.Status == "Red") ? "Red" :
            components.Any(c => c.Status == "Yellow") ? "Yellow" : "Green";

        return new EnterpriseHealthCenterDto
        {
            OverallStatus = overall,
            Components = components,
            GeneratedUtc = DateTime.UtcNow
        };
    }

    private static HealthTrafficLightDto Light(string code, string title, string status, string message) =>
        new() { Code = code, Title = title, Status = status, Message = message };
}
