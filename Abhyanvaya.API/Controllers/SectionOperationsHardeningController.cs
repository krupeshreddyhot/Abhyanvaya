using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI29.1B.5 — Versioning, timeline, preview engines, policies, health (read-heavy).</summary>
[ApiController]
[Route("api/sections/ops")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class SectionOperationsHardeningController : ControllerBase
{
    private readonly ISectionVersioningService _versions;
    private readonly ISectionCapacityHistoryService _capacityHistory;
    private readonly ISectionTimelineService _timeline;
    private readonly IMergePreviewService _mergePreview;
    private readonly ISplitPreviewService _splitPreview;
    private readonly ISectionPolicyService _policies;
    private readonly ISectionCapacityRecommendationService _recommendations;
    private readonly ISectionHealthService _health;

    public SectionOperationsHardeningController(
        ISectionVersioningService versions,
        ISectionCapacityHistoryService capacityHistory,
        ISectionTimelineService timeline,
        IMergePreviewService mergePreview,
        ISplitPreviewService splitPreview,
        ISectionPolicyService policies,
        ISectionCapacityRecommendationService recommendations,
        ISectionHealthService health)
    {
        _versions = versions;
        _capacityHistory = capacityHistory;
        _timeline = timeline;
        _mergePreview = mergePreview;
        _splitPreview = splitPreview;
        _policies = policies;
        _recommendations = recommendations;
        _health = health;
    }

    [HttpGet("{sectionId:int}/versions")]
    public async Task<ActionResult<IReadOnlyList<SectionVersionDto>>> GetVersions(int sectionId, CancellationToken cancellationToken)
        => Ok(await _versions.GetVersionsAsync(sectionId, cancellationToken));

    [HttpGet("{sectionId:int}/capacity-history")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
    public async Task<ActionResult<IReadOnlyList<SectionCapacityHistoryDto>>> GetCapacityHistory(int sectionId, CancellationToken cancellationToken)
        => Ok(await _capacityHistory.GetCapacityHistoryAsync(sectionId, cancellationToken));

    [HttpGet("{sectionId:int}/timeline")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSectionLifecycle)]
    public async Task<ActionResult<IReadOnlyList<SectionTimelineEventDto>>> GetTimeline(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _timeline.GetTimelineAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("merge/preview")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<MergePreviewEngineDto>> MergePreview(
        [FromBody] SectionMergeValidateRequest request,
        CancellationToken cancellationToken)
        => Ok(await _mergePreview.PreviewAsync(
            request.SourceSectionIds,
            request.TargetSectionId ?? 0,
            cancellationToken));

    [HttpPost("split/preview")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<SplitPreviewEngineDto>> SplitPreview(
        [FromBody] SectionSplitValidateRequest request,
        CancellationToken cancellationToken)
        => Ok(await _splitPreview.PreviewAsync(request.SourceSectionId, request.ChildCount, cancellationToken));

    [HttpGet("policies")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
    public async Task<ActionResult<IReadOnlyList<SectionPolicyDto>>> ListPolicies(CancellationToken cancellationToken)
        => Ok(await _policies.ListAsync(cancellationToken));

    [HttpPut("policies")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
    public async Task<ActionResult<SectionPolicyDto>> UpsertPolicy(
        [FromBody] UpsertSectionPolicyRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _policies.UpsertAsync(request, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{sectionId:int}/policy")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
    public async Task<ActionResult<ResolvedSectionPolicyDto>> ResolvePolicy(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _policies.ResolveForSectionAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("recommendations")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSectionCapacity)]
    public async Task<ActionResult<IReadOnlyList<SectionCapacityRecommendationDto>>> Recommendations(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _recommendations.RecommendAsync(academicYearId, semesterId, cancellationToken));

    [HttpGet("health")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSectionReadiness)]
    public async Task<ActionResult<IReadOnlyList<SectionHealthReportDto>>> Health(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _health.EvaluateManyAsync(academicYearId, semesterId, cancellationToken));

    [HttpGet("health/{sectionId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSectionReadiness)]
    public async Task<ActionResult<SectionHealthReportDto>> HealthById(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _health.EvaluateAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("architecture-report")]
    public ActionResult<SectionArchitectureReportDto> ArchitectureReport()
        => Ok(AcademicArchitectureGuard.ValidateSectionBoundaries());
}
