using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Application.TenantContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Enrollment;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewEnrollment)]
[Route("api/enrollment")]
public sealed class EnrollmentController : EnrollmentControllerBase
{
    private readonly IEnrollmentHistoryService _historyService;
    private readonly ITenantContextService _tenantContextService;

    public EnrollmentController(IEnrollmentHistoryService historyService, ITenantContextService tenantContextService)
    {
        _historyService = historyService;
        _tenantContextService = tenantContextService;
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<BatchSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BatchSummary>>> GetHistory(
        [FromQuery] EnrollmentFilters filters,
        CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, collegeId) = MapResolution(resolution);
        filters = filters with { CollegeId = filters.CollegeId ?? collegeId };
        var result = await _historyService.GetHistoryAsync(tenantId, filters, cancellationToken);
        return Ok(result);
    }

    [HttpPost("preview")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollment)]
    [ProducesResponseType(typeof(EnrollmentPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnrollmentPreview>> Preview(
        [FromBody] EnrollmentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, collegeId) = MapResolution(resolution);
        var normalized = request with
        {
            TenantId = tenantId,
            CollegeId = request.CollegeId > 0 ? request.CollegeId : collegeId ?? request.CollegeId,
        };
        var result = await _historyService.PreviewAsync(normalized, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewEnrollment)]
[Route("api/enrollment/batches")]
public sealed class EnrollmentBatchController : EnrollmentControllerBase
{
    private readonly IEnrollmentHistoryService _historyService;
    private readonly IBatchCancellationService _cancellationService;
    private readonly IBatchRetryService _retryService;
    private readonly ITenantContextService _tenantContextService;

    public EnrollmentBatchController(
        IEnrollmentHistoryService historyService,
        IBatchCancellationService cancellationService,
        IBatchRetryService retryService,
        ITenantContextService tenantContextService)
    {
        _historyService = historyService;
        _cancellationService = cancellationService;
        _retryService = retryService;
        _tenantContextService = tenantContextService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BatchSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BatchSummary>>> List(
        [FromQuery] EnrollmentFilters filters,
        CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, collegeId) = MapResolution(resolution);
        filters = filters with { CollegeId = filters.CollegeId ?? collegeId };
        return Ok(await _historyService.GetBatchesAsync(tenantId, filters, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, _) = MapResolution(resolution);
        var batch = await _historyService.GetBatchDetailAsync(id, tenantId, cancellationToken);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(BatchProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchProgressDto>> GetProgress(Guid id, CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, _) = MapResolution(resolution);
        var progress = await _historyService.GetBatchProgressAsync(id, tenantId, cancellationToken);
        return progress is null ? NotFound() : Ok(progress);
    }

    [HttpGet("{id:guid}/students")]
    [ProducesResponseType(typeof(PagedResult<StudentEnrollmentExplorerItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StudentEnrollmentExplorerItem>>> GetStudents(
        Guid id,
        [FromQuery] EnrollmentFilters filters,
        CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, _, _) = MapResolution(resolution);
        return Ok(await _historyService.GetBatchStudentsAsync(id, tenantId, filters, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollment)]
    [ProducesResponseType(typeof(CreateBatchResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateBatchResponse>> Create(
        [FromBody] CreateEnrollmentBatchApiRequest request,
        CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, userId, collegeId) = MapResolution(resolution);
        var normalized = request with
        {
            CollegeId = request.CollegeId > 0 ? request.CollegeId : collegeId ?? request.CollegeId,
        };
        var result = await _historyService.CreateBatchAsync(normalized, tenantId, userId, cancellationToken);
        if (!result.Succeeded)
        {
            // Return 200 with succeeded=false so clients read failureMessage without treating it as a transport error.
            return Ok(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.BatchId }, result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollment)]
    [ProducesResponseType(typeof(BatchCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchCommandResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, userId, _) = MapResolution(resolution);
        return Ok(await _cancellationService.CancelAsync(id, tenantId, userId, cancellationToken));
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEnrollment)]
    [ProducesResponseType(typeof(BatchCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchCommandResponse>> Retry(Guid id, CancellationToken cancellationToken)
    {
        if (RequireTenantContext(_tenantContextService, out var resolution) is { } error)
        {
            return error;
        }

        var (tenantId, userId, _) = MapResolution(resolution);
        return Ok(await _retryService.RetryAsync(id, tenantId, userId, cancellationToken));
    }
}
