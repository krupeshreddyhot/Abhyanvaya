using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Timetable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>Timetable class schedule management and session creation.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/timetable/schedules")]
public sealed class ClassScheduleController : ControllerBase
{
    private readonly IClassScheduleService _scheduleService;

    public ClassScheduleController(IClassScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleDto>>> List(
        [FromQuery] ClassScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var schedules = await _scheduleService.ListAsync(query, cancellationToken);
        return Ok(schedules);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClassScheduleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassScheduleDto>> Create(
        [FromBody] CreateClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var schedule = await _scheduleService.CreateAsync(request, cancellationToken);
        return Ok(schedule);
    }

    [HttpPost("{scheduleId:guid}/attendance-session")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<ActionResult<Guid>> CreateAttendanceSession(
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var sessionId = await _scheduleService.CreateAttendanceSessionFromScheduleAsync(scheduleId, cancellationToken);
        return Ok(sessionId);
    }
}
