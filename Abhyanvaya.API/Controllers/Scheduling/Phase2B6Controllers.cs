using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization;
using Abhyanvaya.Application.Scheduling.Optimization.Simulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI30 Phase 2B.6 — Optimization Readiness (architecture only).
/// Preview/simulate/score only. Never applies timetable changes. No Phase 3 algorithms.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingConflict)]
[Route("api/scheduling/optimization")]
public sealed class OptimizationReadinessController : ControllerBase
{
    private readonly IOptimizationReadinessService _readiness;
    private readonly IOptimizationSimulationService _simulation;

    public OptimizationReadinessController(
        IOptimizationReadinessService readiness,
        IOptimizationSimulationService simulation)
    {
        _readiness = readiness;
        _simulation = simulation;
    }

    [HttpGet("preview")]
    [ProducesResponseType(typeof(OptimizationPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationPreviewDto>> Preview(
        [FromQuery] Guid? simulationId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct)
    {
        try { return Ok(await _readiness.GetPreviewAsync(simulationId, academicYearId, timetableId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("simulate")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    [ProducesResponseType(typeof(OptimizationSimulationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OptimizationSimulationDto>> Simulate(
        [FromBody] RunOptimizationSimulationRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _simulation.SimulateAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("simulations/{simulationId:guid}")]
    public async Task<ActionResult<OptimizationSimulationDto>> GetSimulation(Guid simulationId, CancellationToken ct)
    {
        var dto = await _simulation.GetAsync(simulationId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("simulations/compare")]
    public async Task<ActionResult<SimulationComparisonDto>> Compare(
        [FromBody] CompareSimulationsRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _simulation.CompareAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("simulations/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<OptimizationSimulationDto>> Reject(
        [FromBody] RejectSimulationRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _simulation.RejectAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>
    /// Accepts a simulation for the future Phase 3 apply pipeline only.
    /// Does NOT mutate the live timetable in Phase 2B.6.
    /// </summary>
    [HttpPost("simulations/accept")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<OptimizationSimulationDto>> Accept(
        [FromBody] AcceptSimulationRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _simulation.AcceptAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("score")]
    public async Task<ActionResult<OptimizationScoreDto>> Score(
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        [FromQuery] int? departmentId,
        CancellationToken ct)
    {
        try { return Ok(await _readiness.ScoreAsync(academicYearId, timetableId, departmentId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<IReadOnlyList<OptimizationMetricDto>>> Metrics(
        [FromQuery] int academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct) =>
        Ok(await _readiness.GetMetricsAsync(academicYearId, timetableId, ct));

    [HttpGet("telemetry")]
    public Task<OptimizationTelemetryDto> Telemetry(CancellationToken ct) =>
        _readiness.GetTelemetryAsync(ct);

    [HttpGet("plugins")]
    public ActionResult<IReadOnlyList<OptimizationPluginDto>> Plugins() =>
        Ok(_readiness.GetPlugins());
}
