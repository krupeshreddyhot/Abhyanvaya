using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize]
[Route("api/scheduling/academic-years")]
public sealed class AcademicYearsController : ControllerBase
{
    private readonly IAcademicCalendarService _service;

    public AcademicYearsController(IAcademicCalendarService service) => _service = service;

    /// <summary>List years — Attendance/Section operators may read for optional Section scope (AI29.1D).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanViewAcademicYears)]
    public Task<IReadOnlyList<AcademicYearDto>> List(CancellationToken cancellationToken) =>
        _service.ListYearsAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAcademicYears)]
    public async Task<ActionResult<AcademicYearDto>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetYearByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<AcademicYearDto>> Create([FromBody] CreateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateYearAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<AcademicYearDto>> Update(int id, [FromBody] UpdateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route id does not match body id.");
        try { return Ok(await _service.UpdateYearAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteYearAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/set-current")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<IActionResult> SetCurrent(int id, CancellationToken cancellationToken)
    {
        try { await _service.SetCurrentYearAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("clone")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<AcademicYearDto>> Clone([FromBody] ClonePreviousYearRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.ClonePreviousYearAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/academic-terms")]
public sealed class AcademicTermsController : ControllerBase
{
    private readonly IAcademicCalendarService _service;

    public AcademicTermsController(IAcademicCalendarService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<AcademicTermDto>> List([FromQuery] int? academicYearId, CancellationToken cancellationToken) =>
        _service.ListTermsAsync(academicYearId, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AcademicTermDto>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetTermByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<AcademicTermDto>> Create([FromBody] CreateAcademicTermRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateTermAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<AcademicTermDto>> Update(int id, [FromBody] UpdateAcademicTermRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route id does not match body id.");
        try { return Ok(await _service.UpdateTermAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteTermAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/working-days")]
public sealed class WorkingDaysController : ControllerBase
{
    private readonly IAcademicCalendarService _service;

    public WorkingDaysController(IAcademicCalendarService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<WorkingDayDto>> List([FromQuery] int academicYearId, CancellationToken cancellationToken) =>
        _service.ListWorkingDaysAsync(academicYearId, cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<WorkingDayDto>> Upsert([FromBody] UpsertWorkingDayRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpsertWorkingDayAsync(request, cancellationToken)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteWorkingDayAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/holidays")]
public sealed class HolidaysController : ControllerBase
{
    private readonly IAcademicCalendarService _service;

    public HolidaysController(IAcademicCalendarService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<HolidayDto>> List([FromQuery] int? academicYearId, CancellationToken cancellationToken) =>
        _service.ListHolidaysAsync(academicYearId, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HolidayDto>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetHolidayByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<HolidayDto>> Create([FromBody] CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateHolidayAsync(request, cancellationToken)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<ActionResult<HolidayDto>> Update(int id, [FromBody] UpdateHolidayRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id) return BadRequest("Route id does not match body id.");
        try { return Ok(await _service.UpdateHolidayAsync(request, cancellationToken)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageScheduling)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteHolidayAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
