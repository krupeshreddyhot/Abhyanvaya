using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingFacultyPreferences)]
[Route("api/scheduling/faculty-preferences")]
public sealed class FacultyTeachingPreferencesController : ControllerBase
{
    private readonly IFacultyTeachingPreferenceService _service;
    public FacultyTeachingPreferencesController(IFacultyTeachingPreferenceService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<FacultyTeachingPreferenceDto>> List([FromQuery] int? academicYearId, [FromQuery] int? staffId, [FromQuery] bool? isActive, CancellationToken ct) => _service.ListAsync(academicYearId, staffId, isActive, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<FacultyTeachingPreferenceDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyPreferences)] public async Task<ActionResult<FacultyTeachingPreferenceDto>> Create([FromBody] CreateFacultyTeachingPreferenceRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyPreferences)] public async Task<ActionResult<FacultyTeachingPreferenceDto>> Update(int id, [FromBody] UpdateFacultyTeachingPreferenceRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingFacultyPreferences)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingRoomFeatures)]
[Route("api/scheduling/room-features")]
public sealed class RoomFeaturesController : ControllerBase
{
    private readonly IRoomFeatureService _service;
    public RoomFeaturesController(IRoomFeatureService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<RoomFeatureDto>> List([FromQuery] string? category, [FromQuery] bool? isActive, CancellationToken ct) => _service.ListFeaturesAsync(category, isActive, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<RoomFeatureDto>> Get(int id, CancellationToken ct) { var x = await _service.GetFeatureByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<ActionResult<RoomFeatureDto>> Create([FromBody] CreateRoomFeatureRequest r, CancellationToken ct) { try { return Ok(await _service.CreateFeatureAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<ActionResult<RoomFeatureDto>> Update(int id, [FromBody] UpdateRoomFeatureRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateFeatureAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteFeatureAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPost("clone-assignments")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<ActionResult<IReadOnlyList<RoomFeatureAssignmentDto>>> CloneAssignments([FromBody] CloneRoomFeatureAssignmentsRequest r, CancellationToken ct) { try { return Ok(await _service.CloneAssignmentsAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingRoomFeatures)]
[Route("api/scheduling/rooms/{roomId:int}/features")]
public sealed class RoomFeatureAssignmentsController : ControllerBase
{
    private readonly IRoomFeatureService _service;
    public RoomFeatureAssignmentsController(IRoomFeatureService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<RoomFeatureAssignmentDto>> List(int roomId, CancellationToken ct) => _service.ListAssignmentsByRoomAsync(roomId, ct);
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<ActionResult<RoomFeatureAssignmentDto>> Assign(int roomId, [FromBody] AssignRoomFeatureRequest r, CancellationToken ct) { try { return Ok(await _service.AssignFeatureAsync(roomId, r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpDelete("{roomFeatureId:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingRoomFeatures)] public async Task<IActionResult> Unassign(int roomId, int roomFeatureId, CancellationToken ct) { try { await _service.UnassignFeatureAsync(roomId, roomFeatureId, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingSubjectDelivery)]
[Route("api/scheduling/subject-delivery-types")]
public sealed class SubjectDeliveryTypesController : ControllerBase
{
    private readonly ISubjectDeliveryTypeService _service;
    public SubjectDeliveryTypesController(ISubjectDeliveryTypeService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<SubjectDeliveryTypeDto>> List([FromQuery] bool? isActive, CancellationToken ct) => _service.ListAsync(isActive, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<SubjectDeliveryTypeDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingSubjectDelivery)] public async Task<ActionResult<SubjectDeliveryTypeDto>> Create([FromBody] CreateSubjectDeliveryTypeRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingSubjectDelivery)] public async Task<ActionResult<SubjectDeliveryTypeDto>> Update(int id, [FromBody] UpdateSubjectDeliveryTypeRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingSubjectDelivery)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPut("subjects/{subjectId:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingSubjectDelivery)] public async Task<IActionResult> UpdateSubject(int subjectId, [FromBody] UpdateSubjectDeliveryFieldsRequest r, CancellationToken ct) { if (subjectId != r.SubjectId) return BadRequest(); try { await _service.UpdateSubjectDeliveryFieldsAsync(r, ct); return NoContent(); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingHolidayTypes)]
[Route("api/scheduling/holiday-types")]
public sealed class HolidayTypesController : ControllerBase
{
    private readonly IHolidayTypeCatalogService _service;
    public HolidayTypesController(IHolidayTypeCatalogService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<HolidayTypeCatalogDto>> List([FromQuery] bool? isActive, CancellationToken ct) => _service.ListAsync(isActive, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<HolidayTypeCatalogDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingHolidayTypes)] public async Task<ActionResult<HolidayTypeCatalogDto>> Create([FromBody] CreateHolidayTypeCatalogRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingHolidayTypes)] public async Task<ActionResult<HolidayTypeCatalogDto>> Update(int id, [FromBody] UpdateHolidayTypeCatalogRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageSchedulingHolidayTypes)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/validation-report")]
public sealed class SchedulingValidationReportController : ControllerBase
{
    private readonly ISchedulingValidationService _service;
    public SchedulingValidationReportController(ISchedulingValidationService service) => _service = service;

    [HttpGet] public Task<SchedulingValidationReportDto> Get(CancellationToken ct) => _service.GetReportAsync(ct);
}
