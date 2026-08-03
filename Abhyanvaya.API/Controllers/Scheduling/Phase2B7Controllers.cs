using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Sandbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI30 Phase 2B.7 — Optimization Sandbox.
/// Scenario store/replay/compare/collaborate only. Never edits production timetables. No optimizer.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingConflict)]
[Route("api/scheduling/optimization/sandbox")]
public sealed class OptimizationSandboxController : ControllerBase
{
    private readonly ISandboxService _sandbox;
    private readonly IReplayService _replay;
    private readonly IScenarioComparisonService _comparison;
    private readonly IScenarioCollaborationService _collaboration;
    private readonly IMetricsEvolutionService _evolution;

    public OptimizationSandboxController(
        ISandboxService sandbox,
        IReplayService replay,
        IScenarioComparisonService comparison,
        IScenarioCollaborationService collaboration,
        IMetricsEvolutionService evolution)
    {
        _sandbox = sandbox;
        _replay = replay;
        _comparison = comparison;
        _collaboration = collaboration;
        _evolution = evolution;
    }

    [HttpGet("workspace")]
    public Task<OptimizationWorkspaceDto> Workspace([FromQuery] int? academicYearId, [FromQuery] int? departmentId, CancellationToken ct) =>
        _sandbox.GetWorkspaceAsync(academicYearId, departmentId, ct);

    [HttpPost("scenarios")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ScenarioSummaryDto>> Create([FromBody] CreateScenarioRequest request, CancellationToken ct)
    {
        try { return Ok(await _sandbox.CreateAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("scenarios/{scenarioId:guid}")]
    public async Task<ActionResult<OptimizationScenarioDetailDto>> Detail(Guid scenarioId, CancellationToken ct)
    {
        try { return Ok(await _sandbox.GetDetailAsync(scenarioId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("scenarios/{scenarioId:guid}/save")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ScenarioSummaryDto>> Save(Guid scenarioId, CancellationToken ct)
    {
        try { return Ok(await _sandbox.SaveAsync(scenarioId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("scenarios/rename")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ScenarioSummaryDto>> Rename([FromBody] RenameScenarioRequest request, CancellationToken ct)
    {
        try { return Ok(await _sandbox.RenameAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("scenarios/duplicate")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ScenarioSummaryDto>> Duplicate([FromBody] DuplicateScenarioRequest request, CancellationToken ct)
    {
        try { return Ok(await _sandbox.DuplicateAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("scenarios/{scenarioId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<IActionResult> Delete(Guid scenarioId, CancellationToken ct)
    {
        try { await _sandbox.DeleteAsync(scenarioId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("scenarios/{scenarioId:guid}/favorite")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioSummaryDto> Favorite(Guid scenarioId, [FromQuery] bool value = true, CancellationToken ct = default) =>
        _sandbox.FavoriteAsync(scenarioId, value, ct);

    [HttpPost("scenarios/{scenarioId:guid}/pin")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioSummaryDto> Pin(Guid scenarioId, [FromQuery] bool value = true, CancellationToken ct = default) =>
        _sandbox.PinAsync(scenarioId, value, ct);

    [HttpPost("scenarios/tag")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioSummaryDto> Tag([FromBody] TagScenarioRequest request, CancellationToken ct) =>
        _sandbox.TagAsync(request, ct);

    [HttpPost("scenarios/{scenarioId:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioSummaryDto> Archive(Guid scenarioId, CancellationToken ct) =>
        _sandbox.ArchiveAsync(scenarioId, ct);

    [HttpPost("scenarios/{scenarioId:guid}/template")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioSummaryDto> Template(Guid scenarioId, [FromQuery] bool value = true, CancellationToken ct = default) =>
        _sandbox.MarkTemplateAsync(scenarioId, value, ct);

    [HttpGet("scenarios/{scenarioId:guid}/replay")]
    public Task<ReplayTimelineDto> ReplayTimeline(Guid scenarioId, CancellationToken ct) =>
        _replay.GetTimelineAsync(scenarioId, ct);

    [HttpPost("scenarios/{scenarioId:guid}/replay")]
    public Task<ReplayTimelineDto> Replay(Guid scenarioId, CancellationToken ct) =>
        _replay.ReplayAsync(scenarioId, ct);

    [HttpPost("scenarios/{scenarioId:guid}/restart")]
    public Task<ReplayTimelineDto> Restart(Guid scenarioId, CancellationToken ct) =>
        _replay.RestartAsync(scenarioId, ct);

    [HttpPost("scenarios/compare")]
    public async Task<ActionResult<ScenarioComparisonResultDto>> Compare([FromBody] CompareScenariosRequest request, CancellationToken ct)
    {
        try { return Ok(await _comparison.CompareAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("scenarios/notes")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioNoteDto> Note([FromBody] AddScenarioNoteRequest request, CancellationToken ct) =>
        _collaboration.AddNoteAsync(request, ct);

    [HttpPost("scenarios/comments")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioCommentDto> Comment([FromBody] AddScenarioCommentRequest request, CancellationToken ct) =>
        _collaboration.AddCommentAsync(request, ct);

    [HttpPost("scenarios/{scenarioId:guid}/bookmarks")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioBookmarkDto> Bookmark(Guid scenarioId, [FromQuery] string? name, CancellationToken ct) =>
        _collaboration.AddBookmarkAsync(scenarioId, name ?? "", ct);

    [HttpPost("scenarios/share")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<IActionResult> Share([FromBody] ShareScenarioRequest request, CancellationToken ct)
    {
        await _collaboration.ShareReadOnlyAsync(request, ct);
        return NoContent();
    }

    [HttpPost("scenarios/approvals")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public Task<ScenarioApprovalDto> Approval([FromBody] RequestScenarioApprovalRequest request, CancellationToken ct) =>
        _collaboration.RequestApprovalAsync(request, ct);

    [HttpPost("scenarios/{scenarioId:guid}/reviewed")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<IActionResult> Reviewed(Guid scenarioId, CancellationToken ct)
    {
        await _collaboration.MarkReviewedAsync(scenarioId, ct);
        return NoContent();
    }

    [HttpGet("metrics/evolution")]
    public Task<MetricsEvolutionDto> Evolution([FromQuery] int? academicYearId, CancellationToken ct) =>
        _evolution.GetEvolutionAsync(academicYearId, ct);
}
