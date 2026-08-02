using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Scheduling.Optimization;
using Abhyanvaya.Application.Scheduling.Optimization.Approval;
using Abhyanvaya.Application.Scheduling.Optimization.Dashboard;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI30 Phase 3 — Enterprise Optimization Engine.
/// Always produces sandbox scenarios. Approval creates a new draft version only.
/// Never edits published timetables or attendance APIs.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingConflict)]
[Route("api/scheduling/optimization/engine")]
public sealed class OptimizationEngineController : ControllerBase
{
    private readonly IOptimizationExecutionService _execution;
    private readonly IOptimizationApprovalService _approval;
    private readonly IOptimizationDashboardService _dashboard;

    public OptimizationEngineController(
        IOptimizationExecutionService execution,
        IOptimizationApprovalService approval,
        IOptimizationDashboardService dashboard)
    {
        _execution = execution;
        _approval = approval;
        _dashboard = dashboard;
    }

    [HttpPost("run")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    [ProducesResponseType(typeof(OptimizationExecutionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationExecutionResult>> Run(
        [FromBody] OptimizationRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _execution.RunPipelineAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<OptimizationRunSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OptimizationRunSummaryDto>>> List(
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        CancellationToken ct) =>
        Ok(await _execution.ListRunsAsync(academicYearId, departmentId, ct));

    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(OptimizationExecutionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationExecutionResult>> Get(Guid runId, CancellationToken ct)
    {
        var result = await _execution.GetRunAsync(runId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("runs/{runId:guid}/comparison")]
    [ProducesResponseType(typeof(OptimizationComparisonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationComparisonDto>> Comparison(Guid runId, CancellationToken ct)
    {
        var result = await _execution.GetRunAsync(runId, ct);
        if (result is null) return NotFound();
        return result.Comparison is null ? NotFound() : Ok(result.Comparison);
    }

    [HttpPost("approve")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    [ProducesResponseType(typeof(OptimizationApprovalResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationApprovalResultDto>> Approve(
        [FromBody] ApproveOptimizationRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _approval.ApproveAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("reject")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<IActionResult> Reject([FromBody] RejectOptimizationRequest request, CancellationToken ct)
    {
        try
        {
            await _approval.RejectAsync(request.RunId, request.Reason, ct);
            return Ok(new { request.RunId, status = "Rejected" });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(OptimizationDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationDashboardDto>> Dashboard(
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        CancellationToken ct) =>
        Ok(await _dashboard.GetAsync(academicYearId, departmentId, ct));
}

public sealed class RejectOptimizationRequest
{
    public Guid RunId { get; set; }
    public string? Reason { get; set; }
}
