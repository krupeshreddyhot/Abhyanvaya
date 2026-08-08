using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI29.1C.5 — Allocation operations, governance, analytics (no live student writes).</summary>
[ApiController]
[Route("api/allocation")]
[Authorize]
public sealed class AllocationOperationsController : ControllerBase
{
    private readonly IAllocationOpsDashboardService _ops;
    private readonly IAllocationHistoryService _history;
    private readonly IAllocationExplanationService _explain;
    private readonly IAllocationComparisonService _compare;
    private readonly IAllocationReplayService _replay;
    private readonly IAllocationGovernanceService _governance;
    private readonly IAllocationAnalyticsService _analytics;
    private readonly IAllocationScenarioVersioningService _versions;
    private readonly IAllocationAuditService _audit;
    private readonly IAllocationScenarioQueryService _scenarios;
    private readonly IAllocationReportService _reports;

    public AllocationOperationsController(
        IAllocationOpsDashboardService ops,
        IAllocationHistoryService history,
        IAllocationExplanationService explain,
        IAllocationComparisonService compare,
        IAllocationReplayService replay,
        IAllocationGovernanceService governance,
        IAllocationAnalyticsService analytics,
        IAllocationScenarioVersioningService versions,
        IAllocationAuditService audit,
        IAllocationScenarioQueryService scenarios,
        IAllocationReportService reports)
    {
        _ops = ops;
        _history = history;
        _explain = explain;
        _compare = compare;
        _replay = replay;
        _governance = governance;
        _analytics = analytics;
        _versions = versions;
        _audit = audit;
        _scenarios = scenarios;
        _reports = reports;
    }

    [HttpGet("operations")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationOperations)]
    public async Task<ActionResult<AllocationOpsDashboardDto>> Operations(CancellationToken cancellationToken)
        => Ok(await _ops.GetAsync(cancellationToken));

    /// <remarks>Extends AI29.1C history with richer filters; same route family.</remarks>
    [HttpGet("ops/history")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationOperations)]
    public async Task<ActionResult<IReadOnlyList<AllocationHistoryRow>>> OpsHistory(
        [FromQuery] int? academicYearId,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? semesterId,
        [FromQuery] string? status,
        [FromQuery] string? lifecycleStatus,
        CancellationToken cancellationToken)
        => Ok(await _history.QueryAsync(new AllocationHistoryFilter
        {
            AcademicYearId = academicYearId,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            Status = status,
            LifecycleStatus = lifecycleStatus,
        }, cancellationToken));

    [HttpGet("scenarios")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<IReadOnlyList<AllocationHistoryRow>>> Scenarios(CancellationToken cancellationToken)
        => Ok(await _history.QueryAsync(new AllocationHistoryFilter(), cancellationToken));

    [HttpGet("scenarios/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<AllocationScenarioDetailDto>> Scenario(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _scenarios.GetDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("scenarios/{id:guid}/versions")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<IReadOnlyList<AllocationScenarioVersionDto>>> Versions(Guid id, CancellationToken cancellationToken)
        => Ok(await _versions.ListAsync(id, cancellationToken));

    [HttpGet("scenarios/{id:guid}/explanation")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<AllocationExplanationReport>> Explanation(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _explain.ExplainAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("scenarios/{id:guid}/score")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<AllocationScoreBreakdown>> Score(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok((await _explain.ExplainAsync(id, cancellationToken)).Score); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("scenarios/{id:guid}/constraints")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<AllocationConstraintDashboardDto>> ScenarioConstraints(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var report = await _explain.ExplainAsync(id, cancellationToken);
            var rows = report.Constraints;
            var mandatory = rows.Where(c => c.Priority == AllocationConstraintPriority.Mandatory).ToList();
            var preferred = rows.Where(c => c.Priority == AllocationConstraintPriority.Preferred).ToList();
            var mandatoryCompliance = mandatory.Count == 0
                ? 100
                : Math.Round(mandatory.Count(c => c.Satisfied) * 100.0 / mandatory.Count, 2);
            var preferredCompliance = preferred.Count == 0
                ? 100
                : Math.Round(preferred.Count(c => c.Satisfied) * 100.0 / preferred.Count, 2);
            return Ok(new AllocationConstraintDashboardDto
            {
                TotalConstraints = rows.Count,
                MandatoryViolations = mandatory.Count(c => !c.Satisfied),
                PreferredViolations = preferred.Count(c => !c.Satisfied),
                InformationalFindings = rows.Count(c => c.Priority == AllocationConstraintPriority.Informational && !c.Satisfied),
                MandatoryCompliance = mandatoryCompliance,
                PreferredCompliance = preferredCompliance,
                CompliancePercent = mandatoryCompliance,
                Rows = rows,
            });
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("analytics")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationOperations)]
    public async Task<ActionResult<AllocationAnalyticsDto>> Analytics([FromQuery] string period = "AcademicYear", CancellationToken cancellationToken = default)
        => Ok(await _analytics.GetAsync(period, cancellationToken));

    [HttpPost("scenarios/{id:guid}/replay")]
    [Authorize(Policy = AuthorizationPolicies.CanReplayAllocationScenarios)]
    public async Task<ActionResult<AllocationExecutionResult>> Replay(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _replay.ReplayAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("scenarios/compare")]
    [Authorize(Policy = AuthorizationPolicies.CanCompareAllocationScenarios)]
    public async Task<ActionResult<AllocationMultiCompareReport>> CompareScenarios([FromBody] Guid[] scenarioIds, CancellationToken cancellationToken)
    {
        try { return Ok(await _compare.CompareAsync(scenarioIds, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return BadRequest(ex.Message); }
    }

    [HttpPost("scenarios/{id:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.CanReviewAllocationScenarios)]
    public async Task<ActionResult<AllocationGovernanceResult>> Review(Guid id, [FromQuery] string? notes, CancellationToken cancellationToken)
    {
        var result = await _governance.ReviewAsync(id, notes, cancellationToken);
        if (!result.Success && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(result);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("scenarios/{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanRejectAllocation)]
    public async Task<ActionResult<AllocationGovernanceResult>> Reject(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _governance.RejectAsync(id, reason, cancellationToken);
        if (!result.Success && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(result);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("scenarios/{id:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanArchiveAllocationScenarios)]
    public async Task<ActionResult<AllocationGovernanceResult>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _governance.ArchiveAsync(id, cancellationToken);
        if (!result.Success && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(result);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("scenarios/{id:guid}/save")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateAllocationScenarios)]
    public async Task<ActionResult<AllocationGovernanceResult>> Save(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _governance.SaveAsync(id, reason, cancellationToken);
        if (!result.Success && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(result);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("scenarios/{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveAllocation)]
    public async Task<ActionResult<AllocationGovernanceResult>> ApproveGoverned(Guid id, CancellationToken cancellationToken)
    {
        var result = await _governance.ApproveWithGovernanceAsync(id, cancellationToken);
        if (result.ConcurrencyConflict)
            return Conflict(result);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("scenarios/{id:guid}/governance")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationScenarios)]
    public async Task<ActionResult<AllocationGovernanceResult>> Governance(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _governance.EvaluateAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("audit")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationOperations)]
    public async Task<ActionResult<IReadOnlyList<AllocationAuditDto>>> Audit([FromQuery] int take = 50, CancellationToken cancellationToken = default)
        => Ok(await _audit.ListAsync(take, cancellationToken));

    [HttpGet("operations/architecture-report")]
    [Authorize(Policy = AuthorizationPolicies.CanViewAllocationOperations)]
    public ActionResult<AllocationArchitectureReport> Architecture()
        => Ok(AcademicArchitectureGuard.ValidateAllocationBoundaries());

    [HttpGet("operations/reports/export")]
    [Authorize(Policy = AuthorizationPolicies.CanExportAllocation)]
    public async Task<IActionResult> ExportOps(
        [FromQuery] string kind = "allocation-audit",
        [FromQuery] string format = "csv",
        [FromQuery] Guid? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _reports.ExportAsync(kind, format, scenarioId, cancellationToken);
        var contentType = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) || format.Equals("excel", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        return File(bytes, contentType, $"allocation-ops-{kind}.{format}");
    }
}
