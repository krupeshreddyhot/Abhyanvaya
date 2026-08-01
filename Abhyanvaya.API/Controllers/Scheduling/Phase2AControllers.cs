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
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingVersion)]
[Route("api/scheduling/versions")]
public sealed class ScheduleVersionsController : ControllerBase
{
    private readonly IScheduleVersionService _service;
    private readonly IVersionComparisonService _comparisonService;

    public ScheduleVersionsController(IScheduleVersionService service, IVersionComparisonService comparisonService)
    {
        _service = service;
        _comparisonService = comparisonService;
    }

    [HttpGet]
    public Task<IReadOnlyList<ScheduleVersionDto>> List(
        [FromQuery] int? academicYearId,
        [FromQuery] int? academicTermId,
        [FromQuery] ScheduleVersionStatus? status,
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default) =>
        _service.ListAsync(academicYearId, academicTermId, status, includeArchived, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ScheduleVersionDto>> Get(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpGet("history")]
    public Task<IReadOnlyList<ScheduleVersionHistoryDto>> History(
        [FromQuery] int academicYearId,
        [FromQuery] int? academicTermId,
        CancellationToken ct) =>
        _service.HistoryAsync(academicYearId, academicTermId, ct);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingVersion)]
    public async Task<ActionResult<ScheduleVersionDto>> Create([FromBody] CreateScheduleVersionRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.CreateAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("duplicate")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingVersion)]
    public async Task<ActionResult<ScheduleVersionDto>> Duplicate([FromBody] DuplicateScheduleVersionRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.DuplicateAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("clone-previous")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingVersion)]
    public async Task<ActionResult<ScheduleVersionDto>> ClonePrevious(
        [FromQuery] int academicYearId,
        [FromQuery] int? academicTermId,
        [FromQuery] string versionName,
        CancellationToken ct)
    {
        try { return Ok(await _service.ClonePreviousVersionAsync(academicYearId, academicTermId, versionName, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/mark-current")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingVersion)]
    public async Task<ActionResult<ScheduleVersionDto>> MarkCurrent(int id, CancellationToken ct)
    {
        try { return Ok(await _service.MarkCurrentAsync(id, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanArchiveScheduling)]
    public async Task<ActionResult<ScheduleVersionDto>> Archive(int id, [FromBody] ArchiveScheduleVersionRequest? r, CancellationToken ct)
    {
        try { return Ok(await _service.ArchiveAsync(id, r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("compare")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSchedulingVersionCompare)]
    public async Task<ActionResult<VersionComparisonDto>> Compare([FromBody] CompareScheduleVersionsRequest r, CancellationToken ct)
    {
        try { return Ok(await _comparisonService.CompareAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("compare/export")]
    [Authorize(Policy = AuthorizationPolicies.CanExportSchedulingVersionCompare)]
    public async Task<IActionResult> CompareExport([FromBody] CompareScheduleVersionsRequest r, CancellationToken ct)
    {
        try
        {
            var bytes = await _comparisonService.ExportExcelAsync(r, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "version-comparison.xlsx");
        }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanReviewScheduling)]
[Route("api/scheduling/approvals")]
public sealed class TimetableApprovalsController : ControllerBase
{
    private readonly ITimetableApprovalService _service;

    public TimetableApprovalsController(ITimetableApprovalService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<TimetableApprovalRequestDto>> ListQueue([FromQuery] TimetableApprovalRequestStatus? status, CancellationToken ct) =>
        _service.ListQueueAsync(status, ct);

    [HttpGet("{requestId:int}/timeline")]
    public async Task<ActionResult<TimetableApprovalTimelineDto>> Timeline(int requestId, CancellationToken ct)
    {
        var x = await _service.GetTimelineAsync(requestId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost("submit")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<TimetableApprovalRequestDto>> Submit([FromBody] SubmitForReviewRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.SubmitForReviewAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("decide")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveScheduling)]
    public async Task<ActionResult<TimetableApprovalRequestDto>> Decide([FromBody] DecideApprovalStepRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.DecideStepAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("comments")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingApprovalComments)]
    public async Task<ActionResult<ApprovalCommentDto>> AddComment([FromBody] AddApprovalCommentRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.AddCommentAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanCloneScheduling)]
[Route("api/scheduling/clone-jobs")]
public sealed class TimetableCloneJobsController : ControllerBase
{
    private readonly ITimetableCloneService _service;

    public TimetableCloneJobsController(ITimetableCloneService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<TimetableCloneJobDto>> List([FromQuery] TimetableCloneJobStatus? status, CancellationToken ct) =>
        _service.ListAsync(status, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TimetableCloneJobDto>> Get(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<ActionResult<TimetableCloneJobDto>> Enqueue([FromBody] EnqueueTimetableCloneRequest r, CancellationToken ct)
    {
        try { return Ok(await _service.EnqueueAsync(r, ct)); }
        catch (DomainException ex) { return BadRequest(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingGovernanceDashboard)]
[Route("api/scheduling/governance")]
public sealed class TimetableGovernanceController : ControllerBase
{
    private readonly ITimetableGovernanceDashboardService _dashboardService;

    public TimetableGovernanceController(ITimetableGovernanceDashboardService dashboardService) =>
        _dashboardService = dashboardService;

    [HttpGet("dashboard")]
    public Task<TimetableGovernanceDashboardDto> Dashboard([FromQuery] int? academicYearId, CancellationToken ct) =>
        _dashboardService.GetDashboardAsync(academicYearId, ct);
}
