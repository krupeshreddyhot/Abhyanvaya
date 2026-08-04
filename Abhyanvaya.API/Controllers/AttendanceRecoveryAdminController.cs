using Abhyanvaya.API.Common;
using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI22.8 / AI22.8.5 — administrator recovery / operations dashboard (tenant-scoped).</summary>
[ApiController]
[Route("api/admin/attendance-recovery")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AttendanceRecoveryAdminController : ControllerBase
{
    private readonly IAttendanceRecoveryDashboardService _dashboard;
    private readonly IAttendanceRecoverySearchService _search;
    private readonly IAttendanceExpirationService _expiration;
    private readonly IAttendanceOperationsDashboardService _operations;
    private readonly IAttendanceOperationalAnalyticsService _analytics;
    private readonly IAttendanceHealthMonitorService _health;
    private readonly IDepartmentOperationsService _departments;
    private readonly IEnterpriseOpsDashboardService _enterpriseOps;
    private readonly IBulkOperationService _bulk;
    private readonly ISessionTimelineService _timeline;

    public AttendanceRecoveryAdminController(
        IAttendanceRecoveryDashboardService dashboard,
        IAttendanceRecoverySearchService search,
        IAttendanceExpirationService expiration,
        IAttendanceOperationsDashboardService operations,
        IAttendanceOperationalAnalyticsService analytics,
        IAttendanceHealthMonitorService health,
        IDepartmentOperationsService departments,
        IEnterpriseOpsDashboardService enterpriseOps,
        IBulkOperationService bulk,
        ISessionTimelineService timeline)
    {
        _dashboard = dashboard;
        _search = search;
        _expiration = expiration;
        _operations = operations;
        _analytics = analytics;
        _health = health;
        _departments = departments;
        _enterpriseOps = enterpriseOps;
        _bulk = bulk;
        _timeline = timeline;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AttendanceRecoveryDashboardDto>> Dashboard(CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAdminDashboardAsync(cancellationToken));

    [HttpGet("analytics")]
    public async Task<ActionResult<AttendanceRecoveryAnalyticsDto>> Analytics(CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAnalyticsAsync(cancellationToken));

    [HttpGet("operations")]
    public async Task<ActionResult<AttendanceOperationsDashboardDto>> Operations(CancellationToken cancellationToken)
        => Ok(await _operations.GetAsync(cancellationToken));

    [HttpGet("operational-analytics")]
    public async Task<ActionResult<AttendanceOperationalAnalyticsDto>> OperationalAnalytics(CancellationToken cancellationToken)
        => Ok(await _analytics.GetAsync(cancellationToken));

    [HttpGet("health")]
    public async Task<ActionResult<AttendanceHealthSnapshotDto>> Health(CancellationToken cancellationToken)
        => Ok(await _health.ScanAsync(cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PendingAttendanceSessionDto>>> Search(
        [FromQuery] AttendanceRecoverySearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _search.SearchAsync(request, cancellationToken));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var data = await _dashboard.GetAdminDashboardAsync(cancellationToken);
        var lines = new List<string>
        {
            "SessionId,StaffId,StaffName,SubjectId,SubjectName,CourseId,CourseName,GroupId,Status,WorkflowStatus,PriorityScore,PriorityBand,StartedUtc,LastActivityUtc,ElapsedMinutes,AgeMinutes,RetryCount,FailureCount,ExpectedRemainingMinutes,FailureReason"
        };
        lines.AddRange(data.Sessions.Select(s =>
            $"{s.SessionId},{s.StaffId},\"{Escape(s.StaffName)}\",{s.SubjectId},\"{Escape(s.SubjectName)}\",{s.CourseId},\"{Escape(s.CourseName)}\",{s.GroupId},{s.Status},{s.WorkflowStatus},{s.PriorityScore},{s.PriorityBand},{s.StartedUtc:o},{s.LastActivityUtc:o},{s.ElapsedMinutes:F1},{s.AgeMinutes:F1},{s.RetryCount},{s.FailureCount},{s.ExpectedRemainingMinutes:F1},\"{Escape(s.FailureReason)}\""));
        return File(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines)),
            "application/vnd.ms-excel",
            "attendance-recovery.xls");
    }

    [HttpPost("sessions/{sessionId:guid}/actions")]
    public async Task<IActionResult> Action(
        Guid sessionId,
        [FromBody] AdminSessionActionRequest request,
        CancellationToken cancellationToken)
    {
        await _dashboard.AdminActionAsync(sessionId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("expiration/run")]
    public async Task<ActionResult<object>> RunExpiration(CancellationToken cancellationToken)
    {
        var count = await _expiration.ExpireStaleSessionsAsync(cancellationToken);
        return Ok(new { expired = count, options = _expiration.GetOptions() });
    }

    /// <summary>AI22.8.6.2 — Catalog Department operations summary.</summary>
    [HttpGet("department-operations")]
    public async Task<ActionResult<DepartmentOperationsDashboardDto>> DepartmentOperations(CancellationToken cancellationToken)
        => Ok(await _departments.GetAsync(cancellationToken));

    /// <summary>AI22.8.6.5 — enterprise ops polish widgets.</summary>
    [HttpGet("enterprise-ops")]
    public async Task<ActionResult<EnterpriseOpsDashboardDto>> EnterpriseOps(CancellationToken cancellationToken)
        => Ok(await _enterpriseOps.GetAsync(cancellationToken));

    [HttpGet("sessions/{sessionId:guid}/timeline")]
    public async Task<ActionResult<SessionTimelineDto>> Timeline(
        Guid sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _timeline.GetAsync(sessionId, page, pageSize, cancellationToken));

    /// <summary>AI22.8.6.4 — administrator bulk assist (never auto-finalizes).</summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<BulkOperationResultDto>> Bulk(
        [FromBody] BulkOperationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _bulk.ExecuteAsync(request, cancellationToken));

    [HttpGet("bulk/history")]
    public async Task<ActionResult<IReadOnlyList<BulkOperationHistoryDto>>> BulkHistory(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _bulk.GetHistoryAsync(take, cancellationToken));

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "'");
}
