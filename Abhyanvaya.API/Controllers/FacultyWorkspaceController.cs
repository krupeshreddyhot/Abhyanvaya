using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.Faculty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI31 — Faculty Workspace APIs. Aggregates scheduling/attendance; does not replace attendance APIs.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/faculty/workspace")]
public sealed class FacultyWorkspaceController : ControllerBase
{
    private readonly IFacultyDashboardService _dashboard;

    public FacultyWorkspaceController(IFacultyDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("today")]
    [ProducesResponseType(typeof(FacultyTodayDto), StatusCodes.Status200OK)]
    public Task<FacultyTodayDto> Today([FromQuery] DateOnly? date, CancellationToken ct) =>
        _dashboard.GetTodayAsync(date, ct);

    [HttpGet("current-class")]
    [ProducesResponseType(typeof(FacultyCurrentClassWorkspaceDto), StatusCodes.Status200OK)]
    public Task<FacultyCurrentClassWorkspaceDto> CurrentClass(CancellationToken ct) =>
        _dashboard.GetCurrentClassAsync(ct);

    [HttpGet("timetable")]
    [ProducesResponseType(typeof(FacultyTimetableViewDto), StatusCodes.Status200OK)]
    public Task<FacultyTimetableViewDto> Timetable(
        [FromQuery] string view = "Today",
        [FromQuery] DateOnly? anchor = null,
        CancellationToken ct = default) =>
        _dashboard.GetTimetableAsync(view, anchor, ct);

    [HttpGet("insights")]
    [ProducesResponseType(typeof(FacultyInsightsDto), StatusCodes.Status200OK)]
    public Task<FacultyInsightsDto> Insights(CancellationToken ct) =>
        _dashboard.GetInsightsAsync(ct);

    [HttpGet("notifications")]
    [ProducesResponseType(typeof(IReadOnlyList<FacultyScheduleNotificationDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<FacultyScheduleNotificationDto>> Notifications(CancellationToken ct) =>
        _dashboard.GetNotificationsAsync(ct);
}
