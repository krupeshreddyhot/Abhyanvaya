using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/campuses")]
public sealed class CampusesController : ControllerBase
{
    private readonly ICampusFacilityService _service;
    public CampusesController(ICampusFacilityService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<CampusDto>> List(CancellationToken ct) => _service.ListCampusesAsync(ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<CampusDto>> Get(int id, CancellationToken ct) { var x = await _service.GetCampusByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<CampusDto>> Create([FromBody] CreateCampusRequest r, CancellationToken ct) { try { return Ok(await _service.CreateCampusAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<CampusDto>> Update(int id, [FromBody] UpdateCampusRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateCampusAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteCampusAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/buildings")]
public sealed class BuildingsController : ControllerBase
{
    private readonly ICampusFacilityService _service;
    public BuildingsController(ICampusFacilityService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<BuildingDto>> List([FromQuery] int? campusId, CancellationToken ct) => _service.ListBuildingsAsync(campusId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<BuildingDto>> Get(int id, CancellationToken ct) { var x = await _service.GetBuildingByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<BuildingDto>> Create([FromBody] CreateBuildingRequest r, CancellationToken ct) { try { return Ok(await _service.CreateBuildingAsync(r, ct)); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<BuildingDto>> Update(int id, [FromBody] UpdateBuildingRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateBuildingAsync(r, ct)); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteBuildingAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/floors")]
public sealed class FloorsController : ControllerBase
{
    private readonly ICampusFacilityService _service;
    public FloorsController(ICampusFacilityService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<FloorDto>> List([FromQuery] int? buildingId, CancellationToken ct) => _service.ListFloorsAsync(buildingId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<FloorDto>> Get(int id, CancellationToken ct) { var x = await _service.GetFloorByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<FloorDto>> Create([FromBody] CreateFloorRequest r, CancellationToken ct) { try { return Ok(await _service.CreateFloorAsync(r, ct)); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<FloorDto>> Update(int id, [FromBody] UpdateFloorRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateFloorAsync(r, ct)); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteFloorAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly ICampusFacilityService _service;
    public RoomsController(ICampusFacilityService service) => _service = service;

    [HttpGet] public Task<PagedRoomsResult> Search([FromQuery] RoomSearchQuery query, CancellationToken ct) => _service.SearchRoomsAsync(query, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<RoomDto>> Get(int id, CancellationToken ct) { var x = await _service.GetRoomByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<RoomDto>> Create([FromBody] CreateRoomRequest r, CancellationToken ct) { try { return Ok(await _service.CreateRoomAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<RoomDto>> Update(int id, [FromBody] UpdateRoomRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateRoomAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteRoomAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/time-slot-sets")]
public sealed class TimeSlotSetsController : ControllerBase
{
    private readonly ITimeSlotService _service;
    public TimeSlotSetsController(ITimeSlotService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<TimeSlotSetDto>> List([FromQuery] int? academicYearId, CancellationToken ct) => _service.ListSetsAsync(academicYearId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<TimeSlotSetDto>> Get(int id, CancellationToken ct) { var x = await _service.GetSetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<TimeSlotSetDto>> Create([FromBody] CreateTimeSlotSetRequest r, CancellationToken ct) { try { return Ok(await _service.CreateSetAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<TimeSlotSetDto>> Update(int id, [FromBody] UpdateTimeSlotSetRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateSetAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteSetAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPost("clone")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<TimeSlotSetDto>> Clone([FromBody] CloneTimeSlotSetRequest r, CancellationToken ct) { try { return Ok(await _service.CloneSetAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/time-slots")]
public sealed class TimeSlotsController : ControllerBase
{
    private readonly ITimeSlotService _service;
    public TimeSlotsController(ITimeSlotService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<TimeSlotDto>> List([FromQuery] int timeSlotSetId, CancellationToken ct) => _service.ListSlotsAsync(timeSlotSetId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<TimeSlotDto>> Get(int id, CancellationToken ct) { var x = await _service.GetSlotByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<TimeSlotDto>> Create([FromBody] CreateTimeSlotRequest r, CancellationToken ct) { try { return Ok(await _service.CreateSlotAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<TimeSlotDto>> Update(int id, [FromBody] UpdateTimeSlotRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateSlotAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteSlotAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/faculty-workloads")]
public sealed class FacultyWorkloadsController : ControllerBase
{
    private readonly IFacultyWorkloadService _service;
    public FacultyWorkloadsController(IFacultyWorkloadService service) => _service = service;

    [HttpGet("{staffId:int}")] public async Task<ActionResult<FacultyWorkloadDto>> GetByStaff(int staffId, CancellationToken ct) { var x = await _service.GetByStaffIdAsync(staffId, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPut][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public Task<FacultyWorkloadDto> Upsert([FromBody] UpsertFacultyWorkloadRequest r, CancellationToken ct) => _service.UpsertAsync(r, ct);
    [HttpDelete("{staffId:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int staffId, CancellationToken ct) { try { await _service.DeleteAsync(staffId, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPost("day-preferences")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<FacultyDayPreferenceDto>> UpsertDayPref([FromBody] UpsertFacultyDayPreferenceRequest r, CancellationToken ct) { try { return Ok(await _service.UpsertDayPreferenceAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpDelete("day-preferences/{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> DeleteDayPref(int id, CancellationToken ct) { try { await _service.DeleteDayPreferenceAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpPost("time-slot-preferences")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<FacultyTimeSlotPreferenceDto>> UpsertSlotPref([FromBody] UpsertFacultyTimeSlotPreferenceRequest r, CancellationToken ct) { try { return Ok(await _service.UpsertTimeSlotPreferenceAsync(r, ct)); } catch (KeyNotFoundException ex) { return NotFound(ex.Message); } }
    [HttpDelete("time-slot-preferences/{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> DeleteSlotPref(int id, CancellationToken ct) { try { await _service.DeleteTimeSlotPreferenceAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/subject-allocations")]
public sealed class SubjectAllocationsController : ControllerBase
{
    private readonly ISubjectAllocationService _service;
    public SubjectAllocationsController(ISubjectAllocationService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<SubjectAllocationDto>> List([FromQuery] int? academicYearId, [FromQuery] int? staffId, [FromQuery] int? departmentId, CancellationToken ct) => _service.ListAsync(academicYearId, staffId, departmentId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<SubjectAllocationDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<SubjectAllocationDto>> Create([FromBody] CreateSubjectAllocationRequest r, CancellationToken ct) { try { return Ok(await _service.CreateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } }
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<SubjectAllocationDto>> Update(int id, [FromBody] UpdateSubjectAllocationRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (DomainException ex) { return BadRequest(ex.Message); } catch (ValidationException ex) { return BadRequest(ex.Message); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/room-rules")]
public sealed class RoomRulesController : ControllerBase
{
    private readonly IRoomAllocationRuleService _service;
    public RoomRulesController(IRoomAllocationRuleService service) => _service = service;

    [HttpGet] public Task<IReadOnlyList<RoomAllocationRuleDto>> List([FromQuery] int? academicYearId, CancellationToken ct) => _service.ListAsync(academicYearId, ct);
    [HttpGet("{id:int}")] public async Task<ActionResult<RoomAllocationRuleDto>> Get(int id, CancellationToken ct) { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }
    [HttpPost][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public Task<RoomAllocationRuleDto> Create([FromBody] CreateRoomAllocationRuleRequest r, CancellationToken ct) => _service.CreateAsync(r, ct);
    [HttpPut("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<ActionResult<RoomAllocationRuleDto>> Update(int id, [FromBody] UpdateRoomAllocationRuleRequest r, CancellationToken ct) { if (id != r.Id) return BadRequest(); try { return Ok(await _service.UpdateAsync(r, ct)); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")][Authorize(Policy = AuthorizationPolicies.CanManageScheduling)] public async Task<IActionResult> Delete(int id, CancellationToken ct) { try { await _service.DeleteAsync(id, ct); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
[Route("api/scheduling/dashboard")]
public sealed class SchedulingDashboardController : ControllerBase
{
    private readonly ISchedulingDashboardService _service;
    public SchedulingDashboardController(ISchedulingDashboardService service) => _service = service;

    [HttpGet] public Task<SchedulingDashboardDto> Get(CancellationToken ct) => _service.GetSummaryAsync(ct);
}
