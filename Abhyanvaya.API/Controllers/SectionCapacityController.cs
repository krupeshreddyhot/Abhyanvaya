using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI29.1B — Dashboard-ready capacity APIs (read-only consumers; AI31 dashboards unchanged).</summary>
[ApiController]
[Route("api/sections/capacity")]
[Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
public sealed class SectionCapacityController : ControllerBase
{
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionReadinessService _readiness;

    public SectionCapacityController(ISectionCapacityEngine capacity, ISectionReadinessService readiness)
    {
        _capacity = capacity;
        _readiness = readiness;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SectionCapacitySummaryDto>> GetCapacitySummary(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _capacity.GetCapacitySummaryAsync(academicYearId, semesterId, cancellationToken));

    /// <summary>
    /// Occupancy snapshots. Optional <paramref name="sectionIds"/> scopes to Allocation Context sections
    /// (AI29.1D.24B.2 additive filter — engine already supported sectionIds; year/semester remain compatible).
    /// </summary>
    [HttpGet("occupancy")]
    public async Task<ActionResult<IReadOnlyList<SectionCapacitySnapshotDto>>> GetSectionOccupancy(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        [FromQuery] int[]? sectionIds = null,
        CancellationToken cancellationToken = default)
        => Ok(await _capacity.GetOccupancyAsync(sectionIds, academicYearId, semesterId, cancellationToken));

    [HttpGet("occupancy/{sectionId:int}")]
    public async Task<ActionResult<SectionCapacitySnapshotDto>> GetSectionOccupancyById(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _capacity.GetOccupancyAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("over")]
    public async Task<ActionResult<IReadOnlyList<SectionCapacitySnapshotDto>>> GetOverCapacity(CancellationToken cancellationToken)
        => Ok(await _capacity.GetOverCapacityAsync(cancellationToken));

    [HttpGet("under")]
    public async Task<ActionResult<IReadOnlyList<SectionCapacitySnapshotDto>>> GetUnderCapacity(CancellationToken cancellationToken)
        => Ok(await _capacity.GetUnderCapacityAsync(cancellationToken));

    [HttpGet("health")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSectionReadiness)]
    public async Task<ActionResult<IReadOnlyList<SectionReadinessDto>>> GetSectionHealth(CancellationToken cancellationToken)
        => Ok(await _readiness.GetSectionHealthAsync(cancellationToken));

    [HttpGet("analytics")]
    public async Task<ActionResult<SectionCapacityAnalyticsDto>> GetAnalytics(CancellationToken cancellationToken)
        => Ok(await _capacity.GetAnalyticsAsync(cancellationToken));

    [HttpGet("policy")]
    public async Task<ActionResult<TenantSectionCapacityPolicyDto>> GetPolicy(CancellationToken cancellationToken)
        => Ok(await _capacity.GetPolicyAsync(cancellationToken));

    [HttpPut("policy")]
    public async Task<ActionResult<TenantSectionCapacityPolicyDto>> UpsertPolicy(
        [FromBody] UpsertTenantSectionCapacityPolicyRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _capacity.UpsertPolicyAsync(request, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{sectionId:int}")]
    public async Task<IActionResult> UpdateCapacity(
        int sectionId,
        [FromBody] UpdateSectionCapacityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _capacity.UpdateCapacityAsync(sectionId, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
