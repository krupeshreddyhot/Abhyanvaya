using System.Text;
using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.Faculty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI31.5 — Faculty workspace enhancements. Composition over AI31; no attendance API changes.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/faculty/workspace")]
public sealed class FacultyWorkspaceEnhancementController : ControllerBase
{
    private readonly IFacultyCalendarService _calendar;
    private readonly IFacultyTimelineService _timeline;
    private readonly IClassroomNavigationService _navigation;
    private readonly IWorkspacePreferenceService _preferences;
    private readonly IFacultyProductivityService _productivity;
    private readonly IFacultySearchService _search;
    private readonly IFacultySmartNotificationService _smartNotifications;

    public FacultyWorkspaceEnhancementController(
        IFacultyCalendarService calendar,
        IFacultyTimelineService timeline,
        IClassroomNavigationService navigation,
        IWorkspacePreferenceService preferences,
        IFacultyProductivityService productivity,
        IFacultySearchService search,
        IFacultySmartNotificationService smartNotifications)
    {
        _calendar = calendar;
        _timeline = timeline;
        _navigation = navigation;
        _preferences = preferences;
        _productivity = productivity;
        _search = search;
        _smartNotifications = smartNotifications;
    }

    [HttpGet("calendar/ics")]
    [Produces("text/calendar")]
    public async Task<IActionResult> ExportIcs(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var export = await _calendar.ExportIcsAsync(from, to, ct);
        return File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
    }

    [HttpGet("calendar/subscribe.ics")]
    [Produces("text/calendar")]
    public async Task<IActionResult> SubscribeIcs(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        // Export-only feed for Outlook/Google "from URL" subscription — no two-way sync.
        var export = await _calendar.ExportIcsAsync(from, to, ct);
        Response.Headers.CacheControl = "private, max-age=300";
        return Content(export.Content, export.ContentType, Encoding.UTF8);
    }

    [HttpGet("calendar/meta")]
    [ProducesResponseType(typeof(FacultyCalendarExportDto), StatusCodes.Status200OK)]
    public async Task<FacultyCalendarExportDto> CalendarMeta(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var export = await _calendar.ExportIcsAsync(from, to, ct);
        // Avoid sending full ICS body twice for meta; keep hints + flags.
        return new FacultyCalendarExportDto
        {
            Content = "",
            FileName = export.FileName,
            OutlookSubscriptionHint = export.OutlookSubscriptionHint,
            GoogleSubscriptionHint = export.GoogleSubscriptionHint
        };
    }

    [HttpGet("timeline")]
    [ProducesResponseType(typeof(FacultyTimelineDto), StatusCodes.Status200OK)]
    public Task<FacultyTimelineDto> Timeline([FromQuery] DateOnly? date, CancellationToken ct) =>
        _timeline.GetDailyTimelineAsync(date, ct);

    [HttpGet("rooms/{roomId:int}/navigation")]
    [ProducesResponseType(typeof(ClassroomNavigationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassroomNavigationDto>> RoomNavigation(
        int roomId,
        [FromQuery] int? fromRoomId,
        CancellationToken ct)
    {
        var dto = await _navigation.GetAsync(roomId, fromRoomId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(WorkspacePreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspacePreferenceDto>> GetPreferences(CancellationToken ct)
    {
        try { return Ok(await _preferences.GetAsync(ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("preferences")]
    [ProducesResponseType(typeof(WorkspacePreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkspacePreferenceDto>> UpsertPreferences(
        [FromBody] UpdateWorkspacePreferenceRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _preferences.UpsertAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("productivity")]
    [ProducesResponseType(typeof(FacultyAttendanceProductivityDto), StatusCodes.Status200OK)]
    public Task<FacultyAttendanceProductivityDto> Productivity(CancellationToken ct) =>
        _productivity.GetAttendanceProductivityAsync(ct);

    [HttpGet("productivity/dashboard")]
    [ProducesResponseType(typeof(FacultyProductivityDashboardDto), StatusCodes.Status200OK)]
    public Task<FacultyProductivityDashboardDto> ProductivityDashboard(CancellationToken ct) =>
        _productivity.GetDashboardAsync(ct);

    [HttpGet("search")]
    [ProducesResponseType(typeof(FacultySearchResponseDto), StatusCodes.Status200OK)]
    public Task<FacultySearchResponseDto> Search([FromQuery] string q, CancellationToken ct) =>
        _search.SearchAsync(q, ct);

    [HttpGet("notifications/smart")]
    [ProducesResponseType(typeof(FacultySmartNotificationsDto), StatusCodes.Status200OK)]
    public Task<FacultySmartNotificationsDto> SmartNotifications(CancellationToken ct) =>
        _smartNotifications.GetSmartAsync(ct);
}
