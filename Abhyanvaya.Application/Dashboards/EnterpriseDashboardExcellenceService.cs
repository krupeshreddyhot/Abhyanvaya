using System.Text;
using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Authorization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Dashboards;

public interface IEnterpriseDashboardExcellenceService
{
    Task<EnterpriseDashboardExcellenceDto> GetAsync(
        DashboardFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<DashboardExportResultDto> ExportAsync(
        DashboardExportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AI31.8 — Enterprise Operations Dashboard Excellence (UX / composition only).
/// Reuses Command Center, recovery, analytics, scheduling, conflicts, preferences.
/// Does not modify AttendanceSessionResolver, Attendance APIs, or scheduling engines.
/// </summary>
public sealed class EnterpriseDashboardExcellenceService : IEnterpriseDashboardExcellenceService
{
    private readonly IOperationsCommandCenterService _commandCenter;
    private readonly IDashboardPreferenceService _preferences;
    private readonly IAttendanceRecoveryDashboardService _recovery;
    private readonly IAttendanceOperationalAnalyticsService _analytics;
    private readonly IEnterpriseOpsDashboardService _ops;
    private readonly IEnterpriseHealthCenterService _health;
    private readonly ISchedulingDashboardService _scheduling;
    private readonly ITimetableService _timetables;
    private readonly ITimetableGovernanceDashboardService _governance;
    private readonly IConflictDetectionService _conflicts;
    private readonly IEnterpriseOperationalAnalyticsComposer _enterpriseAnalytics;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseDashboardExcellenceService(
        IOperationsCommandCenterService commandCenter,
        IDashboardPreferenceService preferences,
        IAttendanceRecoveryDashboardService recovery,
        IAttendanceOperationalAnalyticsService analytics,
        IEnterpriseOpsDashboardService ops,
        IEnterpriseHealthCenterService health,
        ISchedulingDashboardService scheduling,
        ITimetableService timetables,
        ITimetableGovernanceDashboardService governance,
        IConflictDetectionService conflicts,
        IEnterpriseOperationalAnalyticsComposer enterpriseAnalytics,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _commandCenter = commandCenter;
        _preferences = preferences;
        _recovery = recovery;
        _analytics = analytics;
        _ops = ops;
        _health = health;
        _scheduling = scheduling;
        _timetables = timetables;
        _governance = governance;
        _conflicts = conflicts;
        _enterpriseAnalytics = enterpriseAnalytics;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EnterpriseDashboardExcellenceDto> GetAsync(
        DashboardFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        var prefs = await Safe(() => _preferences.GetAsync("Admin", cancellationToken))
            ?? new DashboardPreferenceDto { RoleScope = "Admin", DefaultLandingPage = "admin-operations" };
        var effectiveFilters = MergeFilters(filters, prefs.Filters);

        var commandCenter = await _commandCenter.GetAsync(effectiveFilters, cancellationToken);
        commandCenter = ApplyPersonalization(commandCenter, prefs);

        var recovery = await Safe(() => _recovery.GetAdminDashboardAsync(cancellationToken));
        var analytics = await Safe(() => _analytics.GetAsync(cancellationToken));
        var ops = await Safe(() => _ops.GetAsync(cancellationToken));
        var health = await Safe(() => _health.GetAsync(cancellationToken));
        var scheduling = await Safe(() => _scheduling.GetSummaryAsync(cancellationToken));
        var timetable = await Safe(() => _timetables.GetDashboardAsync(effectiveFilters.AcademicYearId, cancellationToken));
        var governance = await Safe(() => _governance.GetDashboardAsync(effectiveFilters.AcademicYearId, cancellationToken));
        var conflicts = await Safe(() => _conflicts.GetDashboardAsync(effectiveFilters.AcademicYearId, null, cancellationToken));
        var enterpriseAnalytics = await Safe(() => _enterpriseAnalytics.GetAsync(cancellationToken));

        var filterState = await BuildFilterStateAsync(effectiveFilters, cancellationToken);
        var executive = await BuildExecutiveSummaryAsync(
            recovery, analytics, ops, health, scheduling, timetable, governance, cancellationToken);
        var timeline = BuildTimeline(timetable);
        var viz = BuildVisualizations(enterpriseAnalytics, scheduling, timetable, conflicts, analytics);
        var refresh = prefs.RefreshIntervalSeconds;
        var now = DateTime.UtcNow;

        return new EnterpriseDashboardExcellenceDto
        {
            ExecutiveSummary = executive,
            Filters = filterState,
            CommandCenter = commandCenter,
            AcademicTimeline = timeline,
            Visualizations = viz,
            WidgetHelp = BuildWidgetHelp(),
            ActionGroups = BuildActionGroups(commandCenter.QuickActions),
            Preferences = prefs,
            RefreshIntervalSeconds = refresh,
            GeneratedUtc = now,
            NextRefreshUtc = refresh > 0 ? now.AddSeconds(refresh) : null
        };
    }

    public async Task<DashboardExportResultDto> ExportAsync(
        DashboardExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(request.Filters, cancellationToken);
        var format = (request.Format ?? "excel").Trim().ToLowerInvariant();

        return format switch
        {
            "csv" or "snapshot" => ExportCsv(dto),
            "pdf" => ExportPdfText(dto),
            _ => ExportExcel(dto)
        };
    }

    private async Task<ExecutiveSummaryDto> BuildExecutiveSummaryAsync(
        DTOs.AttendanceRecovery.AttendanceRecoveryDashboardDto? recovery,
        DTOs.AttendanceRecovery.AttendanceOperationalAnalyticsDto? analytics,
        DTOs.AttendanceRecovery.EnterpriseOpsDashboardDto? ops,
        EnterpriseHealthCenterDto? health,
        DTOs.Scheduling.SchedulingDashboardDto? scheduling,
        DTOs.Scheduling.TimetableDashboardDto? timetable,
        DTOs.Scheduling.TimetableGovernanceDashboardDto? governance,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yearName = await Safe(async () =>
        {
            var y = await _db.SchedulingAcademicYears.AsNoTracking()
                .Where(x => x.TenantId == _currentUser.TenantId && x.IsCurrent)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
            return y;
        });
        var college = await Safe(async () =>
        {
            // Prefer college name for tenant when available.
            var name = await _db.Colleges.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId && !c.IsDeleted)
                .OrderBy(c => c.Id)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
            return name;
        });
        var semester = await Safe(async () =>
        {
            var s = await _db.SchedulingAcademicTerms.AsNoTracking()
                .Where(t => t.TenantId == _currentUser.TenantId)
                .OrderByDescending(t => t.Id)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
            return s;
        });

        var completed = analytics?.SessionsCompleted;
        var started = analytics?.SessionsStarted;
        var attendancePct = started is > 0 && completed.HasValue
            ? $"{Math.Round(100.0 * completed.Value / started.Value, 1):0.#}%"
            : "—";
        var healthLabel = health?.OverallStatus switch
        {
            "Green" => "Healthy",
            "Yellow" => "Warning",
            "Red" => "Critical",
            _ => health?.OverallStatus ?? "Healthy"
        };
        var critical = (health?.Components.Count(c => c.Status == "Red") ?? 0)
                       + (recovery?.FailedCount ?? 0);

        var cards = new List<DashboardWidgetDto>
        {
            ExecCard("exec-academic-year", "Academic Year", yearName ?? "—", null, "/setup/scheduling/academic-years", now, "Current academic year for the college."),
            ExecCard("exec-college", "College Name", college ?? "—", null, "/setup", now, "Institution identity for this tenant."),
            ExecCard("exec-semester", "Current Semester", semester ?? "—", null, "/setup/scheduling", now, "Latest academic term on record."),
            ExecCard("exec-date", "Today's Date", today.ToString("dd MMM yyyy"), null, "/dashboard", now, "Operator local calendar date."),
            ExecCard("exec-working-day", "Current Working Day", today.DayOfWeek.ToString(), null, "/setup/scheduling", now, "Calendar weekday for today's operations."),
            ExecCard("exec-classes", "Total Scheduled Classes Today", timetable is null ? "—" : timetable.ScheduledPeriodCount.ToString(), timetable?.ScheduledPeriodCount, "/setup/scheduling/timetables", now, "Scheduled periods composed from timetable dashboard."),
            ExecCard("exec-attendance", "Overall Attendance Today", attendancePct, null, "/setup/attendance-recovery", now, "Completed vs started attendance sessions today."),
            ExecCard("exec-faculty", "Faculty Available Today", (scheduling?.FacultyCount ?? timetable?.FacultyScheduledCount)?.ToString() ?? "—", scheduling?.FacultyCount ?? timetable?.FacultyScheduledCount, "/setup/staff", now, "Faculty catalog / scheduled load signal."),
            ExecCard("exec-students", "Active Students", "—", null, "/students", now, "Open Students module for active roster counts."),
            ExecCard("exec-alerts", "Critical Alerts", critical.ToString(), critical, "/dashboard/health", now, "Critical health components plus failed recognition sessions.", critical > 0 ? "Red" : "Green"),
            ExecCard("exec-health", "Platform Health", healthLabel, null, "/dashboard/health", now, "College system health composition status.", health?.OverallStatus ?? "Green"),
        };

        return new ExecutiveSummaryDto
        {
            AcademicYear = yearName,
            CollegeName = college,
            CurrentSemester = semester,
            TodaysDate = today,
            CurrentWorkingDay = today.DayOfWeek.ToString(),
            TotalScheduledClassesToday = timetable?.ScheduledPeriodCount,
            OverallAttendanceToday = attendancePct,
            FacultyAvailableToday = scheduling?.FacultyCount ?? timetable?.FacultyScheduledCount,
            ActiveStudents = null,
            CriticalAlerts = critical,
            PlatformHealth = healthLabel,
            PlatformHealthStatus = health?.OverallStatus ?? "Green",
            Cards = cards
        };
    }

    private async Task<DashboardFilterStateDto> BuildFilterStateAsync(
        DashboardFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var years = await Safe(async () =>
            await _db.SchedulingAcademicYears.AsNoTracking()
                .Where(y => y.TenantId == _currentUser.TenantId)
                .OrderByDescending(y => y.IsCurrent).ThenByDescending(y => y.Id)
                .Select(y => new NamedOptionDto { Id = y.Id, Name = y.Name })
                .Take(40)
                .ToListAsync(cancellationToken)) ?? [];

        var departments = await Safe(async () =>
            await _db.Departments.AsNoTracking()
                .Where(d => d.TenantId == _currentUser.TenantId && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .Select(d => new NamedOptionDto { Id = d.Id, Name = d.Name })
                .Take(80)
                .ToListAsync(cancellationToken)) ?? [];

        var courses = await Safe(async () =>
            await _db.Courses.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new NamedOptionDto { Id = c.Id, Name = c.Name })
                .Take(80)
                .ToListAsync(cancellationToken)) ?? [];

        var campuses = await Safe(async () =>
            await _db.SchedulingCampuses.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId)
                .OrderBy(c => c.Name)
                .Select(c => new NamedOptionDto { Id = c.Id, Name = c.Name })
                .Take(40)
                .ToListAsync(cancellationToken)) ?? [];

        var buildingsQuery = _db.SchedulingBuildings.AsNoTracking()
            .Where(b => b.TenantId == _currentUser.TenantId);
        if (filters.CampusId is int campusId)
            buildingsQuery = buildingsQuery.Where(b => b.CampusId == campusId);
        var buildings = await Safe(async () =>
            await buildingsQuery.OrderBy(b => b.Name)
                .Select(b => new NamedOptionDto { Id = b.Id, Name = b.Name })
                .Take(80)
                .ToListAsync(cancellationToken)) ?? [];

        List<NamedOptionDto> rooms = [];
        try
        {
            var roomsQuery = _db.SchedulingRooms.AsNoTracking()
                .Where(r => r.TenantId == _currentUser.TenantId);
            if (filters.BuildingId is int buildingId)
            {
                var floorIds = await _db.SchedulingFloors.AsNoTracking()
                    .Where(f => f.BuildingId == buildingId)
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken);
                roomsQuery = roomsQuery.Where(r => floorIds.Contains(r.FloorId));
            }
            rooms = await roomsQuery.OrderBy(r => r.Name)
                .Select(r => new NamedOptionDto { Id = r.Id, Name = r.Name })
                .Take(120)
                .ToListAsync(cancellationToken);
        }
        catch { /* filter options degrade gracefully */ }

        return new DashboardFilterStateDto
        {
            AcademicYearId = filters.AcademicYearId,
            DepartmentId = filters.DepartmentId,
            CourseId = filters.CourseId,
            CampusId = filters.CampusId,
            BuildingId = filters.BuildingId,
            RoomId = filters.RoomId,
            AcademicYears = years,
            Departments = departments,
            Courses = courses,
            Campuses = campuses,
            Buildings = buildings,
            Rooms = rooms
        };
    }

    private static AcademicTimelineDto BuildTimeline(DTOs.Scheduling.TimetableDashboardDto? timetable)
    {
        var local = DateTime.Now.TimeOfDay;
        var items = new List<AcademicTimelineItemDto>();
        // Indicative college day periods — composition UX only; attendance mode still via resolver.
        var slots = new (string Label, TimeSpan Start, TimeSpan End, string Kind)[]
        {
            ("Period 1", new(9, 0, 0), new(9, 50, 0), "Period"),
            ("Break", new(9, 50, 0), new(10, 0, 0), "Break"),
            ("Period 2", new(10, 0, 0), new(10, 50, 0), "Period"),
            ("Break", new(10, 50, 0), new(11, 0, 0), "Break"),
            ("Period 3", new(11, 0, 0), new(11, 50, 0), "Period"),
            ("Lunch", new(11, 50, 0), new(12, 40, 0), "Lunch"),
            ("Period 4", new(12, 40, 0), new(13, 30, 0), "Period"),
            ("Period 5", new(13, 30, 0), new(14, 20, 0), "Period"),
            ("Period 6", new(14, 20, 0), new(15, 10, 0), "Period"),
        };

        string? currentLabel = null;
        foreach (var s in slots)
        {
            string status;
            var isCurrent = s.Start <= local && local < s.End;
            if (isCurrent) { status = s.Kind == "Period" ? "Current" : s.Kind; currentLabel = s.Label; }
            else if (s.End <= local) status = "Completed";
            else status = "Upcoming";

            items.Add(new AcademicTimelineItemDto
            {
                Kind = s.Kind,
                Label = s.Label,
                Status = status,
                StartTime = s.Start,
                EndTime = s.End,
                FacultyOccupancy = s.Kind == "Period" ? timetable?.FacultyScheduledCount : null,
                RoomOccupancy = s.Kind == "Period" ? timetable?.RoomsScheduledCount : null,
                IsCurrent = isCurrent
            });
        }

        return new AcademicTimelineDto
        {
            CurrentPeriodLabel = currentLabel ?? "Outside periods",
            CurrentTime = local,
            Items = items
        };
    }

    private static DashboardVisualizationsDto BuildVisualizations(
        EnterpriseOperationalAnalyticsDto? enterpriseAnalytics,
        DTOs.Scheduling.SchedulingDashboardDto? scheduling,
        DTOs.Scheduling.TimetableDashboardDto? timetable,
        DTOs.Scheduling.ConflictDashboardDto? conflicts,
        DTOs.AttendanceRecovery.AttendanceOperationalAnalyticsDto? analytics)
    {
        OperationalChartSeriesDto? Find(string code) =>
            enterpriseAnalytics?.Series.FirstOrDefault(s =>
                s.Code.Contains(code, StringComparison.OrdinalIgnoreCase)
                || s.Title.Contains(code, StringComparison.OrdinalIgnoreCase));

        var attendance = Find("attendance") ?? Find("daily") ?? SeriesFromAnalytics(analytics);
        var department = Find("department") ?? new OperationalChartSeriesDto
        {
            Code = "department-heatmap",
            Title = "Department Heatmap",
            Points = scheduling is null
                ? []
                : [new ChartPointDto { Label = "Departments", Value = scheduling.DepartmentCount }]
        };
        var faculty = Find("faculty") ?? new OperationalChartSeriesDto
        {
            Code = "faculty-workload",
            Title = "Faculty Workload Heatmap",
            Points = (timetable?.FacultyLoad ?? [])
                .Select(x => new ChartPointDto { Label = x.Name, Value = x.Count })
                .Take(12)
                .ToList()
        };
        var roomSeries = Find("room") ?? new OperationalChartSeriesDto
        {
            Code = "room-utilization",
            Title = "Room Utilization Heatmap",
            Points = (timetable?.RoomUsage ?? [])
                .Select(x => new ChartPointDto { Label = x.Name, Value = x.Count })
                .Take(12)
                .ToList()
        };
        var weekly = Find("week") ?? attendance;
        var schedulingCompletion = new OperationalChartSeriesDto
        {
            Code = "scheduling-completion",
            Title = "Scheduling Completion",
            Points =
            [
                new() { Label = "Draft", Value = timetable?.DraftTimetableCount ?? 0 },
                new() { Label = "Locked", Value = timetable?.LockedCount ?? 0 },
                new() { Label = "Scheduled Periods", Value = timetable?.ScheduledPeriodCount ?? 0 },
            ]
        };
        var conflictTrend = new OperationalChartSeriesDto
        {
            Code = "conflict-trend",
            Title = "Conflict Trend",
            Points = conflicts is null
                ? []
                :
                [
                    new() { Label = "Faculty", Value = conflicts.FacultyConflicts },
                    new() { Label = "Room", Value = conflicts.RoomConflicts },
                    new() { Label = "Student", Value = conflicts.StudentConflicts },
                    new() { Label = "Calendar", Value = conflicts.CalendarConflicts },
                ]
        };

        return new DashboardVisualizationsDto
        {
            AttendanceHeatmap = attendance,
            DepartmentHeatmap = department,
            FacultyWorkloadHeatmap = faculty,
            RoomUtilizationHeatmap = roomSeries,
            WeeklyAttendanceTrend = weekly,
            SchedulingCompletion = schedulingCompletion,
            ConflictTrend = conflictTrend
        };
    }

    private static OperationalChartSeriesDto SeriesFromAnalytics(
        DTOs.AttendanceRecovery.AttendanceOperationalAnalyticsDto? analytics) =>
        new()
        {
            Code = "attendance-heatmap",
            Title = "Attendance Heatmap",
            Points = (analytics?.DailyTrends ?? [])
                .Select(p => new ChartPointDto { Label = p.Label, Value = p.Value })
                .ToList()
        };

    private static IReadOnlyList<WidgetHelpDto> BuildWidgetHelp() =>
    [
        Help("attendance-recovery-queue", "Attendance Recovery Queue",
            "Sessions waiting in the recovery queue for faculty or admin action.",
            "Count of today's pending recovery sessions from Attendance Recovery dashboard.",
            "On dashboard refresh / SignalR recovery notifications",
            ["/setup/attendance-recovery"], "Attendance Recovery"),
        Help("scheduling-issues", "Scheduling Issues Requiring Attention",
            "Open faculty/room/student/calendar scheduling issues.",
            "Sum of conflict counters from Conflict Detection dashboard.",
            "On refresh",
            ["/setup/scheduling/conflicts/workspace"], "Conflict Workspace"),
        Help("timetable-approval-queue", "Timetable Approval Queue",
            "Timetable versions awaiting approval.",
            "Governance approval queue count.",
            "On refresh",
            ["/setup/scheduling/governance/approvals"], "Approvals"),
        Help("ai-recognition-queue", "AI Attendance Recognition Queue",
            "AI recognition results awaiting human review.",
            "Review-pending count from recovery composition.",
            "On refresh / SignalR",
            ["/setup/attendance-recovery"], "Attendance Recovery"),
        Help("exec-health", "Platform Health",
            "Overall college system health signal.",
            "Composed from Enterprise Health Center component statuses.",
            "On refresh",
            ["/dashboard/health"], "Health Center"),
        Help("classes-running-now", "Current Running Classes",
            "Attendance sessions currently processing.",
            "Processing count from recovery dashboard.",
            "Every refresh interval",
            ["/setup/attendance-recovery"], "Attendance Recovery"),
    ];

    private static WidgetHelpDto Help(
        string code, string title, string purpose, string calc, string freq, string[] paths, string module) =>
        new()
        {
            WidgetCode = code,
            Purpose = purpose,
            HowCalculated = calc,
            UpdateFrequency = freq,
            RelatedModules = [module, "Enterprise Operations Command Center"],
            NavigationLinks = paths.Select(p => new QuickLinkDto { Label = title, Path = p }).ToList()
        };

    private static IReadOnlyList<ActionGroupDto> BuildActionGroups(
        IReadOnlyList<CommandCenterQuickActionDto> actions)
    {
        CommandCenterQuickActionDto? Find(string code) =>
            actions.FirstOrDefault(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        return
        [
            new()
            {
                Code = "scheduling",
                Title = "Scheduling",
                Actions = new[] { Find("create-timetable"), Find("approve-timetable"), Find("run-optimization") }
                    .Where(a => a is not null).Cast<CommandCenterQuickActionDto>().ToList()
            },
            new()
            {
                Code = "attendance",
                Title = "Attendance",
                Actions = new[] { Find("take-attendance"), Find("review-attendance"), Find("attendance-recovery") }
                    .Where(a => a is not null).Cast<CommandCenterQuickActionDto>().ToList()
            },
            new()
            {
                Code = "reports",
                Title = "Reports",
                Actions = new[] { Find("reports") }.Where(a => a is not null).Cast<CommandCenterQuickActionDto>().ToList()
            },
            new()
            {
                Code = "administration",
                Title = "Administration",
                Actions =
                [
                    new() { Code = "catalog", Label = "Catalog", Path = "/setup", RequiredPermission = PermissionKeys.DashboardView },
                    new() { Code = "preferences", Label = "Preferences", Path = "/dashboard/preferences", RequiredPermission = PermissionKeys.DashboardView },
                ]
            },
            new()
            {
                Code = "operations",
                Title = "Operations",
                Actions = new[] { Find("notifications") }
                    .Where(a => a is not null)
                    .Cast<CommandCenterQuickActionDto>()
                    .Concat([new() { Code = "health", Label = "Health Center", Path = "/dashboard/health", RequiredPermission = PermissionKeys.DashboardView, Shortcut = "H" }])
                    .ToList()
            },
        ];
    }

    private static EnterpriseOperationsCommandCenterDto ApplyPersonalization(
        EnterpriseOperationsCommandCenterDto cc,
        DashboardPreferenceDto prefs)
    {
        var hidden = prefs.HiddenWidgets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pinned = prefs.PinnedWidgets.ToHashSet(StringComparer.OrdinalIgnoreCase);

        CommandCenterSectionDto MapSection(CommandCenterSectionDto s)
        {
            var cards = s.Cards
                .Select(c => CloneWidget(c, pinned.Contains(c.Code), !hidden.Contains(c.Code)))
                .Where(c => c.Visible)
                .ToList();
            if (prefs.WidgetOrder.Count > 0)
            {
                var order = prefs.WidgetOrder
                    .Select((code, i) => (code, i))
                    .ToDictionary(x => x.code, x => x.i, StringComparer.OrdinalIgnoreCase);
                cards = cards
                    .OrderBy(c => pinned.Contains(c.Code) ? 0 : 1)
                    .ThenBy(c => order.TryGetValue(c.Code, out var idx) ? idx : c.SortOrder + 1000)
                    .ToList();
            }
            else
            {
                cards = cards.OrderBy(c => pinned.Contains(c.Code) ? 0 : 1).ThenBy(c => c.SortOrder).ToList();
            }

            return new CommandCenterSectionDto
            {
                Code = s.Code,
                Title = s.Title,
                Icon = s.Icon,
                Subtitle = s.Subtitle,
                CollapsedByDefault = s.CollapsedByDefault,
                Cards = cards,
                GroupOrder = s.GroupOrder,
                QuickLinks = s.QuickLinks
            };
        }

        return new EnterpriseOperationsCommandCenterDto
        {
            Title = cc.Title,
            Subtitle = cc.Subtitle,
            RefreshIntervalSeconds = prefs.RefreshIntervalSeconds > 0 ? prefs.RefreshIntervalSeconds : cc.RefreshIntervalSeconds,
            AttentionRequired = MapSection(cc.AttentionRequired),
            TodaysOperations = MapSection(cc.TodaysOperations),
            SchedulingOperations = MapSection(cc.SchedulingOperations),
            AttendanceOperations = MapSection(cc.AttendanceOperations),
            AcademicResources = MapSection(cc.AcademicResources),
            SystemHealth = MapSection(cc.SystemHealth),
            ActionBanners = cc.ActionBanners,
            QuickActions = cc.QuickActions,
            Preferences = prefs,
            GeneratedUtc = cc.GeneratedUtc
        };
    }

    private static DashboardWidgetDto CloneWidget(DashboardWidgetDto c, bool pinned, bool visible) =>
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
            Explanation = c.Explanation ?? c.Tooltip,
            Pinned = pinned,
            RequiredPermission = c.RequiredPermission,
            Configurable = c.Configurable,
            Visible = visible,
            SortOrder = c.SortOrder
        };

    private static DashboardWidgetDto ExecCard(
        string code, string title, string display, decimal? value, string path, DateTime now, string explanation, string? status = null) =>
        new()
        {
            Code = code,
            Title = title,
            Kind = "Kpi",
            Category = "Executive",
            Value = value,
            DisplayValue = display,
            Path = path,
            Explanation = explanation,
            Tooltip = explanation,
            LastUpdatedUtc = now,
            Status = status ?? "Info",
            StatusLabel = status is "Red" ? "Critical" : status is "Green" ? "Healthy" : "Information",
            ActionLabel = "View Details",
            Visible = true,
            Configurable = true
        };

    private static DashboardFilterRequest MergeFilters(DashboardFilterRequest? query, DashboardFilterRequest? saved) =>
        new()
        {
            AcademicYearId = query?.AcademicYearId ?? saved?.AcademicYearId,
            DepartmentId = query?.DepartmentId ?? saved?.DepartmentId,
            CourseId = query?.CourseId ?? saved?.CourseId,
            CampusId = query?.CampusId ?? saved?.CampusId,
            BuildingId = query?.BuildingId ?? saved?.BuildingId,
            RoomId = query?.RoomId ?? saved?.RoomId
        };

    private static DashboardExportResultDto ExportExcel(EnterpriseDashboardExcellenceDto dto)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Executive Summary");
        ws.Cell(1, 1).Value = "Metric";
        ws.Cell(1, 2).Value = "Value";
        var row = 2;
        foreach (var c in dto.ExecutiveSummary.Cards)
        {
            ws.Cell(row, 1).Value = c.Title;
            ws.Cell(row, 2).Value = c.DisplayValue ?? c.Value?.ToString() ?? "—";
            row++;
        }

        var att = wb.Worksheets.Add("Attention");
        att.Cell(1, 1).Value = "Title";
        att.Cell(1, 2).Value = "Value";
        att.Cell(1, 3).Value = "Severity";
        row = 2;
        foreach (var c in dto.CommandCenter.AttentionRequired.Cards)
        {
            att.Cell(row, 1).Value = c.Title;
            att.Cell(row, 2).Value = c.DisplayValue ?? "";
            att.Cell(row, 3).Value = c.StatusLabel ?? c.Status ?? "";
            row++;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return new DashboardExportResultDto
        {
            FileName = $"enterprise-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Content = stream.ToArray()
        };
    }

    private static DashboardExportResultDto ExportCsv(EnterpriseDashboardExcellenceDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Title,Value,Status");
        foreach (var c in dto.ExecutiveSummary.Cards)
            sb.AppendLine($"Executive,\"{c.Title}\",\"{c.DisplayValue}\",\"{c.StatusLabel}\"");
        foreach (var c in dto.CommandCenter.AttentionRequired.Cards)
            sb.AppendLine($"Attention,\"{c.Title}\",\"{c.DisplayValue}\",\"{c.StatusLabel}\"");
        return new DashboardExportResultDto
        {
            FileName = $"enterprise-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv",
            ContentType = "text/csv",
            Content = Encoding.UTF8.GetBytes(sb.ToString())
        };
    }

    private static DashboardExportResultDto ExportPdfText(EnterpriseDashboardExcellenceDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Enterprise Operations Dashboard — Executive Snapshot");
        sb.AppendLine($"Generated (UTC): {dto.GeneratedUtc:O}");
        sb.AppendLine();
        foreach (var c in dto.ExecutiveSummary.Cards)
            sb.AppendLine($"{c.Title}: {c.DisplayValue}");
        sb.AppendLine();
        sb.AppendLine("Attention Required");
        foreach (var c in dto.CommandCenter.AttentionRequired.Cards)
            sb.AppendLine($"- {c.Title}: {c.DisplayValue} ({c.StatusLabel})");
        return new DashboardExportResultDto
        {
            FileName = $"enterprise-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmm}.txt",
            ContentType = "text/plain",
            Content = Encoding.UTF8.GetBytes(sb.ToString())
        };
    }

    private static async Task<T?> Safe<T>(Func<Task<T>> factory) where T : class
    {
        try { return await factory(); }
        catch { return null; }
    }
}
