using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingConflict)]
[Route("api/scheduling/conflicts")]
public sealed class ConflictsController : ControllerBase
{
    private readonly IConflictDetectionService _service;

    public ConflictsController(IConflictDetectionService service) => _service = service;

    [HttpPost("analyze")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ConflictAnalysisReportDto>> Analyze([FromBody] RunConflictDetectionRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AnalyzeAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("workspace")]
    public async Task<ActionResult<ConflictWorkspaceDto>> Workspace(
        [FromQuery] int? timetableId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        [FromQuery] int? staffId,
        [FromQuery] int? roomId,
        [FromQuery] ConflictCategory? category,
        [FromQuery] ConflictSeverity? severity,
        [FromQuery] string? search,
        [FromQuery] bool reanalyze = false,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _service.GetWorkspaceAsync(new ConflictWorkspaceQuery
            {
                TimetableId = timetableId,
                AcademicYearId = academicYearId,
                DepartmentId = departmentId,
                StaffId = staffId,
                RoomId = roomId,
                Category = category,
                Severity = severity,
                Search = search,
                Reanalyze = reanalyze,
                UseLatestRun = !reanalyze
            }, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ConflictDashboardDto>> Dashboard(
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct = default)
    {
        try { return Ok(await _service.GetDashboardAsync(academicYearId, timetableId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("heatmaps/faculty")]
    public Task<HeatMapDto> FacultyHeatMap(
        [FromQuery] int academicYearId,
        [FromQuery] int? staffId,
        [FromQuery] int? timetableId,
        CancellationToken ct = default) =>
        _service.GetFacultyHeatMapAsync(academicYearId, staffId, timetableId, ct);

    [HttpGet("heatmaps/room")]
    public Task<HeatMapDto> RoomHeatMap(
        [FromQuery] int academicYearId,
        [FromQuery] int? roomId,
        [FromQuery] int? timetableId,
        CancellationToken ct = default) =>
        _service.GetRoomHeatMapAsync(academicYearId, roomId, timetableId, ct);

    [HttpGet("heatmaps/department")]
    public Task<HeatMapDto> DepartmentHeatMap(
        [FromQuery] int academicYearId,
        [FromQuery] int? departmentId,
        [FromQuery] int? timetableId,
        CancellationToken ct = default) =>
        _service.GetDepartmentHeatMapAsync(academicYearId, departmentId, timetableId, ct);
}

/// <summary>
/// Optional attendance resolution helper. Does not replace or alter existing attendance APIs.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/attendance-resolution")]
public sealed class AttendanceResolutionController : ControllerBase
{
    private readonly IAttendanceSessionResolver _resolver;

    public AttendanceResolutionController(IAttendanceSessionResolver resolver) => _resolver = resolver;

    [HttpGet("current")]
    [ProducesResponseType(typeof(AttendanceSessionResolutionDto), StatusCodes.Status200OK)]
    public Task<AttendanceSessionResolutionDto> Resolve(
        [FromQuery] int? staffId,
        [FromQuery] DateOnly? date,
        CancellationToken ct = default) =>
        _resolver.ResolveAsync(staffId, date, ct);
}
