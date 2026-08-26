using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI29.1C — Allocation engine APIs (scenarios / drafts only; no live student writes).</summary>
[ApiController]
[Route("api/allocation")]
[Authorize]
public sealed class AllocationEngineController : ControllerBase
{
    private readonly IAllocationExecutionService _execution;
    private readonly IAllocationSimulationService _simulation;
    private readonly IAllocationApprovalService _approval;
    private readonly IAllocationSandboxService _sandbox;
    private readonly IAllocationDashboardService _dashboard;
    private readonly IAllocationReportService _reports;

    public AllocationEngineController(
        IAllocationExecutionService execution,
        IAllocationSimulationService simulation,
        IAllocationApprovalService approval,
        IAllocationSandboxService sandbox,
        IAllocationDashboardService dashboard,
        IAllocationReportService reports)
    {
        _execution = execution;
        _simulation = simulation;
        _approval = approval;
        _sandbox = sandbox;
        _dashboard = dashboard;
        _reports = reports;
    }

    public sealed class AllocationRunRequest
    {
        public int AcademicYearId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
        public int SemesterId { get; set; }
        public string? GroupingMode { get; set; }
        public Dictionary<string, bool>? EnabledStrategies { get; set; }
        /// <summary>AI29.1C constraint priorities (Mandatory / Preferred / Informational) by constraint code.</summary>
        public Dictionary<string, string>? ConstraintPriorities { get; set; }
        /// <summary>AI29.1D — Population selection criteria (resolved against Allocation Context only).</summary>
        public AllocationPopulationSelection? PopulationSelection { get; set; }
        /// <summary>AI29.1D — Explicit target section ids (omit/empty = all eligible sections in context).</summary>
        public List<int>? TargetSectionIds { get; set; }
        /// <summary>AI29.1D.24B.4 — Optional band size for RollNumberBands placement (null = first section capacity).</summary>
        public int? RollNumberBandSize { get; set; }
        /// <summary>AI29.1D.24B.4A — PreserveExisting | Reallocate | LegacyPreserveWhenCapacityAllows (omit = legacy).</summary>
        public string? ExistingAssignmentPolicy { get; set; }
    }

    [HttpPost("run")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult<AllocationExecutionResult>> Run([FromBody] AllocationRunRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var config = BuildConfig(request);
            return Ok(await _execution.RunAsync(Scope(request), config, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("simulate")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult<AllocationExecutionResult>> Simulate([FromBody] AllocationRunRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _simulation.PreviewAsync(Scope(request), BuildConfig(request), cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("compare")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<AllocationComparisonReport>> Compare([FromQuery] Guid scenarioId, CancellationToken cancellationToken)
    {
        try { return Ok(await _simulation.CompareAsync(scenarioId, cancellationToken)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveAllocation)]
    public async Task<ActionResult<AllocationDraft>> Approve([FromQuery] Guid scenarioId, CancellationToken cancellationToken)
    {
        try
        {
            var draft = await _approval.ApproveAsync(scenarioId, cancellationToken);
            return Ok(draft);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("simulate/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult> Reject([FromQuery] Guid scenarioId, CancellationToken cancellationToken)
        => await _simulation.RejectAsync(scenarioId, cancellationToken) ? Ok() : NotFound();

    [HttpPost("simulate/accept")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult> AcceptSimulation([FromQuery] Guid scenarioId, CancellationToken cancellationToken)
        => await _simulation.AcceptSimulationAsync(scenarioId, cancellationToken) ? Ok() : NotFound();

    [HttpGet("history")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<IReadOnlyList<AllocationHistoryItem>>> History(CancellationToken cancellationToken)
        => Ok(await _execution.GetHistoryAsync(cancellationToken));

    [HttpGet("session/{sessionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<AllocationExecutionResult>> Session(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _execution.GetSessionResultAsync(sessionId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<AllocationDashboardDto>> Dashboard(CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAsync(cancellationToken));

    [HttpGet("sandbox")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<IReadOnlyList<AllocationSandboxItem>>> Sandbox([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => Ok(await _sandbox.ListAsync(includeArchived, cancellationToken));

    [HttpPost("sandbox")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult<AllocationSandboxItem>> SaveSandbox(
        [FromQuery] Guid scenarioId,
        [FromQuery] string name,
        [FromQuery] string? tags,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _sandbox.SaveAsync(scenarioId, name, tags, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("sandbox/{sandboxId:guid}/duplicate")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult<AllocationSandboxItem>> DuplicateSandbox(Guid sandboxId, [FromQuery] string? name, CancellationToken cancellationToken)
    {
        var item = await _sandbox.DuplicateAsync(sandboxId, name, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("sandbox/{sandboxId:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanRunAllocation)]
    public async Task<ActionResult> ArchiveSandbox(Guid sandboxId, CancellationToken cancellationToken)
        => await _sandbox.ArchiveAsync(sandboxId, cancellationToken) ? Ok() : NotFound();

    [HttpGet("sandbox/{sandboxId:guid}/replay")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<AllocationScenario>> ReplaySandbox(Guid sandboxId, CancellationToken cancellationToken)
    {
        var scenario = await _sandbox.ReplayAsync(sandboxId, cancellationToken);
        return scenario is null ? NotFound() : Ok(scenario);
    }

    [HttpGet("reports/export")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<IActionResult> Export(
        [FromQuery] string kind = "allocation-summary",
        [FromQuery] string format = "csv",
        [FromQuery] Guid? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _reports.ExportAsync(kind, format, scenarioId, cancellationToken);
        var contentType = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) || format.Equals("excel", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "text/csv";
        return File(bytes, contentType, $"allocation-{kind}.{format}");
    }

    [HttpGet("grouping-modes")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public ActionResult<IReadOnlyList<string>> GroupingModes() => Ok(AllocationGroupingModes.All);

    [HttpGet("pipeline-strategies")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public ActionResult<IReadOnlyList<string>> PipelineStrategies()
        => Ok(AllocationPipelineConfig.Default.EnabledStrategies.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList());

    [HttpGet("constraint-priorities")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public ActionResult<IReadOnlyDictionary<string, string>> ConstraintPriorityDefaults()
        => Ok(AllocationPipelineConfig.Default.ConstraintPriorities.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString(),
            StringComparer.OrdinalIgnoreCase));

    [HttpGet("engine-architecture-report")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public ActionResult<AllocationArchitectureReport> EngineArchitecture()
        => Ok(AcademicArchitectureGuard.ValidateAllocationBoundaries());

    private static AllocationScopeRequest Scope(AllocationRunRequest r) => new()
    {
        AcademicYearId = r.AcademicYearId,
        CourseId = r.CourseId,
        GroupId = r.GroupId,
        SemesterId = r.SemesterId,
    };

    private static AllocationPipelineConfig BuildConfig(AllocationRunRequest request)
    {
        var defaults = AllocationPipelineConfig.Default;
        return new AllocationPipelineConfig
        {
            GroupingMode = string.IsNullOrWhiteSpace(request.GroupingMode) ? defaults.GroupingMode : request.GroupingMode!,
            EnabledStrategies = MergeStrategies(defaults.EnabledStrategies, request.EnabledStrategies),
            ConstraintPriorities = MergeConstraintPriorities(defaults.ConstraintPriorities, request.ConstraintPriorities),
            PopulationSelection = request.PopulationSelection ?? AllocationPopulationSelection.AllEligible,
            TargetSectionIds = request.TargetSectionIds is { Count: > 0 } ? request.TargetSectionIds : null,
            RollNumberBandSize = request.RollNumberBandSize is > 0 ? request.RollNumberBandSize : null,
            ExistingAssignmentPolicy = request.ExistingAssignmentPolicy,
        }.Normalize();
    }

    /// <summary>
    /// Merge request toggles onto platform defaults so opt-in strategies (e.g. RollNumberBands)
    /// remain false when omitted — missing keys must not silently enable placement policies.
    /// </summary>
    private static IReadOnlyDictionary<string, bool> MergeStrategies(
        IReadOnlyDictionary<string, bool> defaults,
        Dictionary<string, bool>? overrides)
    {
        var merged = new Dictionary<string, bool>(defaults, StringComparer.OrdinalIgnoreCase);
        if (overrides is null || overrides.Count == 0) return merged;
        foreach (var (code, enabled) in overrides)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            merged[code.Trim()] = enabled;
        }
        return merged;
    }

    private static IReadOnlyDictionary<string, AllocationConstraintPriority> MergeConstraintPriorities(
        IReadOnlyDictionary<string, AllocationConstraintPriority> defaults,
        Dictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, AllocationConstraintPriority>(defaults, StringComparer.OrdinalIgnoreCase);
        if (overrides is null || overrides.Count == 0) return merged;

        foreach (var (code, raw) in overrides)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(raw)) continue;
            if (!Enum.TryParse<AllocationConstraintPriority>(raw.Trim(), ignoreCase: true, out var priority))
                continue;
            // Only allow known engine constraint codes from the default contract.
            if (!merged.ContainsKey(code)) continue;
            merged[code] = priority;
        }

        return merged;
    }
}
