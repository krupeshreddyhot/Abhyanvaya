using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingFacultyAvailability)]
[Route("api/scheduling/faculty-availability")]
public sealed class FacultyAvailabilityController : ControllerBase
{
    private readonly IFacultyAvailabilityService _service;
    public FacultyAvailabilityController(IFacultyAvailabilityService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<FacultyAvailabilityDto>> List([FromQuery] int? academicYearId, [FromQuery] int? staffId, CancellationToken ct) => _service.ListAsync(academicYearId, staffId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<FacultyAvailabilityDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyAvailability)] public async Task<ActionResult<FacultyAvailabilityDto>> Create([FromBody] CreateFacultyAvailabilityRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyAvailability)] public async Task<ActionResult<FacultyAvailabilityDto>> Update(int id, [FromBody] UpdateFacultyAvailabilityRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyAvailability)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingRoomAvailability)]
[Route("api/scheduling/room-availability")]
public sealed class RoomAvailabilityController : ControllerBase
{
    private readonly IRoomAvailabilityService _service;
    public RoomAvailabilityController(IRoomAvailabilityService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<RoomAvailabilityDto>> List([FromQuery] int? academicYearId, [FromQuery] int? roomId, CancellationToken ct) => _service.ListAsync(academicYearId, roomId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<RoomAvailabilityDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomAvailability)] public async Task<ActionResult<RoomAvailabilityDto>> Create([FromBody] CreateRoomAvailabilityRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomAvailability)] public async Task<ActionResult<RoomAvailabilityDto>> Update(int id, [FromBody] UpdateRoomAvailabilityRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomAvailability)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/subject-categories")]
public sealed class SubjectCategoriesController : ControllerBase
{
    private readonly ISubjectCategoryService _service;
    public SubjectCategoriesController(ISubjectCategoryService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<SubjectCategoryDto>> List([FromQuery] bool? isActive, CancellationToken ct) => _service.ListAsync(isActive, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<SubjectCategoryDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<SubjectCategoryDto>> Create([FromBody] CreateSubjectCategoryRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<SubjectCategoryDto>> Update(int id, [FromBody] UpdateSubjectCategoryRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPut("subjects/{subjectId:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> UpdateSubject(int subjectId, [FromBody] UpdateSubjectSchedulingCategoryRequest r, CancellationToken ct) { if (subjectId != r.SubjectId) return BadRequest(); try { await _service.UpdateSubjectCategoryFieldsAsync(r, ct); return NoContent(); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingTemplate)]
[Route("api/scheduling/time-slot-templates")]
public sealed class TimeSlotTemplatesController : ControllerBase
{
    private readonly ITimeSlotTemplateService _service;
    public TimeSlotTemplatesController(ITimeSlotTemplateService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<TimeSlotTemplateDto>> List(CancellationToken ct) => _service.ListAsync(ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<TimeSlotTemplateDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpGet("{id:int}/preview")] public async Task<ActionResult<TimeSlotTemplatePreviewDto>> Preview(int id, CancellationToken ct) { var x = await _service.PreviewAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTemplate)] public async Task<ActionResult<TimeSlotTemplateDto>> Create([FromBody] CreateTimeSlotTemplateRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTemplate)] public async Task<ActionResult<TimeSlotTemplateDto>> Update(int id, [FromBody] UpdateTimeSlotTemplateRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTemplate)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPost("clone")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTemplate)] public async Task<ActionResult<TimeSlotTemplateDto>> Clone([FromBody] CloneTimeSlotTemplateRequest r, CancellationToken ct) { try { return Ok(await _service.CloneAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpPost("{id:int}/set-default")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTemplate)] public async Task<ActionResult<TimeSlotTemplateDto>> SetDefault(int id, CancellationToken ct) { try { return Ok(await _service.SetDefaultAsync(id, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
}
