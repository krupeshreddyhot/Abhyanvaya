using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingTimetable)]
[Route("api/scheduling/timetables")]
public sealed class TimetablesController : ControllerBase
{
    private readonly ITimetableService _service;
    private readonly ITimetableExportService _exportService;
    private readonly ITimetableLifecycleService _lifecycleService;
    private readonly ITimetableSoftValidationService _softValidationService;
    private readonly ITimetableChangeHistoryService _historyService;

    public TimetablesController(
        ITimetableService service,
        ITimetableExportService exportService,
        ITimetableLifecycleService lifecycleService,
        ITimetableSoftValidationService softValidationService,
        ITimetableChangeHistoryService historyService)
    {
        _service = service;
        _exportService = exportService;
        _lifecycleService = lifecycleService;
        _softValidationService = softValidationService;
        _historyService = historyService;
    }

    [HttpGet]
    public Task<IReadOnlyList<TimetableDto>> List(
        [FromQuery] int? academicYearId,
        [FromQuery] TimetableStatus? status,
        [FromQuery] int? departmentId,
        CancellationToken ct,
        [FromQuery] bool includeArchived = false) =>
        _service.ListTimetablesAsync(academicYearId, status, departmentId, includeArchived, ct);

    [HttpGet("dashboard")]
    public Task<TimetableDashboardDto> Dashboard([FromQuery] int? academicYearId, CancellationToken ct) =>
        _service.GetDashboardAsync(academicYearId, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TimetableDto>> Get(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("{id:int}/grid")]
    public async Task<ActionResult<TimetableGridDto>> GetGrid(int id, CancellationToken ct)
    {
        var x = await _service.GetGridAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("{id:int}/faculty/{staffId:int}")]
    public async Task<ActionResult<TimetableProjectionDto>> FacultyProjection(int id, int staffId, CancellationToken ct)
    {
        var x = await _service.GetFacultyProjectionAsync(id, staffId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("{id:int}/student")]
    public async Task<ActionResult<TimetableProjectionDto>> StudentProjection(
        int id,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken ct)
    {
        var x = await _service.GetStudentProjectionAsync(id, courseId, groupId, semesterId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("{id:int}/room/{roomId:int}")]
    public async Task<ActionResult<TimetableProjectionDto>> RoomProjection(int id, int roomId, CancellationToken ct)
    {
        var x = await _service.GetRoomProjectionAsync(id, roomId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("{id:int}/department/{departmentId:int}")]
    public async Task<ActionResult<TimetableProjectionDto>> DepartmentProjection(int id, int departmentId, CancellationToken ct)
    {
        var x = await _service.GetDepartmentProjectionAsync(id, departmentId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableDto>> Create([FromBody] CreateTimetableRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.CreateTimetableAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableDto>> Update(int id, [FromBody] UpdateTimetableRequest r, CancellationToken ct)
    {
        if (id != r.Id) return BadRequest();
        try { return Ok(await _service.UpdateTimetableAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { await _service.DeleteTimetableAsync(id, ct); return NoContent(); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/lock")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableDto>> Lock(int id, CancellationToken ct)
    {
        try { return Ok(await _service.LockAsync(id, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/unlock")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableDto>> Unlock(int id, CancellationToken ct)
    {
        try { return Ok(await _service.UnlockAsync(id, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/entries")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableEntryDto>> CreateEntry(int id, [FromBody] CreateTimetableEntryRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.CreateEntryAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("entries/{entryId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableEntryDto>> UpdateEntry(int entryId, [FromBody] UpdateTimetableEntryRequest r, CancellationToken ct)
    {
        if (entryId != r.Id) return BadRequest();
        try { return Ok(await _service.UpdateEntryAsync(entryId, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("entries/{entryId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<IActionResult> DeleteEntry(int entryId, CancellationToken ct)
    {
        try { await _service.DeleteEntryAsync(entryId, ct); return NoContent(); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("entries/{entryId:int}/move")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableEntryDto>> MoveEntry(int entryId, [FromBody] MoveTimetableEntryRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.MoveEntryAsync(entryId, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("entries/{entryId:int}/copy")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableEntryDto>> CopyEntry(int entryId, [FromBody] CopyTimetableEntryRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.CopyEntryAsync(entryId, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("entries/{entryId:int}/duplicate")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableEntryDto>> DuplicateEntry(int entryId, CancellationToken ct)
    {
        try { return Ok(await _service.DuplicateEntryAsync(entryId, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/entries/bulk")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<IReadOnlyList<TimetableEntryDto>>> BulkEntries(int id, [FromBody] BulkPasteEntriesRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.BulkUpsertEntriesAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/publish")]
    [Authorize(Policy = AuthorizationPolicies.CanPublishScheduling)]
    public async Task<ActionResult<TimetableDto>> Publish(int id, [FromBody] PublishTimetableRequest? r, CancellationToken ct)
    {
        try { return Ok(await _lifecycleService.PublishAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanArchiveScheduling)]
    public async Task<ActionResult<TimetableDto>> Archive(int id, [FromBody] ArchiveTimetableRequest? r, CancellationToken ct)
    {
        try { return Ok(await _lifecycleService.ArchiveAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/freeze")]
    [Authorize(Policy = AuthorizationPolicies.CanFreezeScheduling)]
    public async Task<ActionResult<TimetableDto>> Freeze(int id, [FromBody] FreezeTimetableRequest r, CancellationToken ct)
    {
        try { return Ok(await _lifecycleService.FreezeAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/unlock-frozen")]
    [Authorize(Policy = AuthorizationPolicies.CanUnlockScheduling)]
    public async Task<ActionResult<TimetableDto>> UnlockFrozen(int id, [FromBody] UnlockFrozenTimetableRequest r, CancellationToken ct)
    {
        try { return Ok(await _lifecycleService.UnlockFrozenAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("archive-reasons")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSchedulingArchive)]
    public Task<IReadOnlyList<ArchiveReasonDto>> ArchiveReasons(CancellationToken ct) =>
        _lifecycleService.ListArchiveReasonsAsync(ct);

    [HttpGet("{id:int}/soft-warnings")]
    public async Task<ActionResult<IReadOnlyList<SoftWarningDto>>> SoftWarnings(int id, CancellationToken ct)
    {
        try { return Ok(await _softValidationService.ValidateAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/soft-warnings/dismiss")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<IActionResult> DismissSoftWarning(int id, [FromBody] DismissSoftWarningRequest r, CancellationToken ct)
    {
        try { await _softValidationService.DismissWarningAsync(id, r, ct); return NoContent(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/history")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSchedulingHistory)]
    public Task<IReadOnlyList<TimetableChangeHistoryDto>> History(
        int id,
        [FromQuery] int? entryId,
        [FromQuery] TimetableChangeOperation? operation,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct) =>
        _historyService.ListAsync(new TimetableChangeHistoryFilter
        {
            TimetableId = id,
            EntryId = entryId,
            Operation = operation,
            FromUtc = fromUtc,
            ToUtc = toUtc
        }, ct);

    [HttpGet("{id:int}/history/export/excel")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSchedulingHistory)]
    public async Task<IActionResult> ExportHistoryExcel(
        int id,
        [FromQuery] int? entryId,
        [FromQuery] TimetableChangeOperation? operation,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct)
    {
        var bytes = await _historyService.ExportExcelAsync(new TimetableChangeHistoryFilter
        {
            TimetableId = id,
            EntryId = entryId,
            Operation = operation,
            FromUtc = fromUtc,
            ToUtc = toUtc
        }, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"timetable-{id}-history.xlsx");
    }

    [HttpGet("{id:int}/export/excel")]
    public async Task<IActionResult> ExportExcel(
        int id,
        [FromQuery] string view,
        [FromQuery] int? staffId,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? semesterId,
        [FromQuery] int? roomId,
        [FromQuery] int? departmentId,
        CancellationToken ct)
    {
        try
        {
            var bytes = view?.ToLowerInvariant() switch
            {
                "faculty" when staffId.HasValue => await _exportService.ExportFacultyExcelAsync(id, staffId.Value, ct),
                "student" when courseId.HasValue && groupId.HasValue && semesterId.HasValue =>
                    await _exportService.ExportStudentExcelAsync(id, courseId.Value, groupId.Value, semesterId.Value, ct),
                "room" when roomId.HasValue => await _exportService.ExportRoomExcelAsync(id, roomId.Value, ct),
                "department" when departmentId.HasValue => await _exportService.ExportDepartmentExcelAsync(id, departmentId.Value, ct),
                _ => throw new DomainException("Invalid export view or missing filter parameters.")
            };
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"timetable-{id}-{view}.xlsx");
        }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
