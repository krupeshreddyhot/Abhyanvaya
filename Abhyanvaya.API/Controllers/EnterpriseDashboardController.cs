using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.DTOs.Dashboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI31.6 / AI31.7 / AI31.8 — Enterprise Dashboards composition APIs.
/// Does not change Attendance, Timetable, Scheduling engine, or AttendanceSessionResolver.
/// </summary>
[ApiController]
[Route("api/enterprise-dashboards")]
public sealed class EnterpriseDashboardController : ControllerBase
{
    private readonly IFacultyCommandCenterService _faculty;
    private readonly IAdminOperationsDashboardService _admin;
    private readonly IOperationsCommandCenterService _commandCenter;
    private readonly IEnterpriseDashboardExcellenceService _excellence;
    private readonly IEnterpriseOperationalAnalyticsComposer _analytics;
    private readonly IEnterpriseHealthCenterService _health;
    private readonly IEnterpriseNotificationCenterService _notifications;
    private readonly IDashboardPreferenceService _preferences;

    public EnterpriseDashboardController(
        IFacultyCommandCenterService faculty,
        IAdminOperationsDashboardService admin,
        IOperationsCommandCenterService commandCenter,
        IEnterpriseDashboardExcellenceService excellence,
        IEnterpriseOperationalAnalyticsComposer analytics,
        IEnterpriseHealthCenterService health,
        IEnterpriseNotificationCenterService notifications,
        IDashboardPreferenceService preferences)
    {
        _faculty = faculty;
        _admin = admin;
        _commandCenter = commandCenter;
        _excellence = excellence;
        _analytics = analytics;
        _health = health;
        _notifications = notifications;
        _preferences = preferences;
    }

    [HttpGet("faculty/command-center")]
    [Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
    [ProducesResponseType(typeof(FacultyCommandCenterDto), StatusCodes.Status200OK)]
    public Task<FacultyCommandCenterDto> FacultyCommandCenter(CancellationToken ct) =>
        _faculty.GetAsync(ct);

    [HttpGet("faculty/kpis")]
    [Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
    [ProducesResponseType(typeof(FacultyKpiBundleDto), StatusCodes.Status200OK)]
    public Task<FacultyKpiBundleDto> FacultyKpis(CancellationToken ct) =>
        _faculty.GetKpisAsync(ct);

    [HttpGet("faculty/insights")]
    [Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
    [ProducesResponseType(typeof(FacultyInsightsPanelDto), StatusCodes.Status200OK)]
    public Task<FacultyInsightsPanelDto> FacultyInsights(CancellationToken ct) =>
        _faculty.GetInsightsPanelAsync(ct);

    [HttpGet("faculty/activity-timeline")]
    [Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
    [ProducesResponseType(typeof(FacultyActivityTimelineDto), StatusCodes.Status200OK)]
    public Task<FacultyActivityTimelineDto> FacultyActivityTimeline(
        [FromQuery] string range = "Today",
        CancellationToken ct = default) =>
        _faculty.GetActivityTimelineAsync(range, ct);

    [HttpGet("admin/operations")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(AdminOperationsDashboardDto), StatusCodes.Status200OK)]
    public Task<AdminOperationsDashboardDto> AdminOperations(CancellationToken ct) =>
        _admin.GetAsync(ct);

    /// <summary>AI31.7 — Enterprise Operations Command Center (Attention → Quick Actions).</summary>
    [HttpGet("admin/command-center")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(EnterpriseOperationsCommandCenterDto), StatusCodes.Status200OK)]
    public Task<EnterpriseOperationsCommandCenterDto> AdminCommandCenter(
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        [FromQuery] int? courseId,
        [FromQuery] int? campusId,
        [FromQuery] int? buildingId,
        [FromQuery] int? roomId,
        CancellationToken ct) =>
        _commandCenter.GetAsync(new DashboardFilterRequest
        {
            AcademicYearId = academicYearId,
            DepartmentId = departmentId,
            CourseId = courseId,
            CampusId = campusId,
            BuildingId = buildingId,
            RoomId = roomId
        }, ct);

    /// <summary>AI31.8 — Enterprise Operations Dashboard Excellence (executive summary + filters + viz).</summary>
    [HttpGet("admin/excellence")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(EnterpriseDashboardExcellenceDto), StatusCodes.Status200OK)]
    public Task<EnterpriseDashboardExcellenceDto> AdminExcellence(
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        [FromQuery] int? courseId,
        [FromQuery] int? campusId,
        [FromQuery] int? buildingId,
        [FromQuery] int? roomId,
        CancellationToken ct) =>
        _excellence.GetAsync(new DashboardFilterRequest
        {
            AcademicYearId = academicYearId,
            DepartmentId = departmentId,
            CourseId = courseId,
            CampusId = campusId,
            BuildingId = buildingId,
            RoomId = roomId
        }, ct);

    [HttpPost("admin/excellence/export")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportExcellence(
        [FromBody] DashboardExportRequest request,
        CancellationToken ct)
    {
        var result = await _excellence.ExportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("widgets/catalog")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<DashboardWidgetDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DashboardWidgetDto>> WidgetCatalog([FromQuery] string role = "Faculty") =>
        Ok(role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            ? DashboardWidgetCatalog.AdminDefaults
            : DashboardWidgetCatalog.FacultyDefaults);

    [HttpGet("widgets/help")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<WidgetHelpDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WidgetHelpDto>>> WidgetHelp(CancellationToken ct)
    {
        var dto = await _excellence.GetAsync(null, ct);
        return Ok(dto.WidgetHelp);
    }

    [HttpGet("analytics")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(EnterpriseOperationalAnalyticsDto), StatusCodes.Status200OK)]
    public Task<EnterpriseOperationalAnalyticsDto> Analytics(CancellationToken ct) =>
        _analytics.GetAsync(ct);

    [HttpGet("health")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(EnterpriseHealthCenterDto), StatusCodes.Status200OK)]
    public Task<EnterpriseHealthCenterDto> Health(CancellationToken ct) =>
        _health.GetAsync(ct);

    [HttpGet("notifications")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(EnterpriseNotificationCenterDto), StatusCodes.Status200OK)]
    public Task<EnterpriseNotificationCenterDto> Notifications(CancellationToken ct) =>
        _notifications.GetAsync(ct);

    [HttpPost("notifications/state")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(EnterpriseNotificationCenterDto), StatusCodes.Status200OK)]
    public Task<EnterpriseNotificationCenterDto> UpdateNotificationState(
        [FromBody] NotificationStateUpdateRequest request,
        CancellationToken ct) =>
        _notifications.UpdateStateAsync(request, ct);

    [HttpGet("preferences")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(DashboardPreferenceDto), StatusCodes.Status200OK)]
    public Task<DashboardPreferenceDto> GetPreferences([FromQuery] string? roleScope, CancellationToken ct) =>
        _preferences.GetAsync(roleScope, ct);

    [HttpPut("preferences")]
    [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
    [ProducesResponseType(typeof(DashboardPreferenceDto), StatusCodes.Status200OK)]
    public Task<DashboardPreferenceDto> UpsertPreferences(
        [FromBody] UpdateDashboardPreferenceRequest request,
        CancellationToken ct) =>
        _preferences.UpsertAsync(request, ct);
}
