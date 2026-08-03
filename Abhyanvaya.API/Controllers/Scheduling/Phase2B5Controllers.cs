using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI30 Phase 2B.5 — Enterprise Conflict Intelligence (advisory only).
/// Never edits timetables. No optimizer. Attendance APIs unchanged.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingConflict)]
[Route("api/scheduling/conflicts")]
public sealed class ConflictIntelligenceController : ControllerBase
{
    private readonly IConflictIntelligenceService _intelligence;
    private readonly IConflictAnalyticsService _analytics;
    private readonly IConflictRuleConfigurationService _ruleConfig;

    public ConflictIntelligenceController(
        IConflictIntelligenceService intelligence,
        IConflictAnalyticsService analytics,
        IConflictRuleConfigurationService ruleConfig)
    {
        _intelligence = intelligence;
        _analytics = analytics;
        _ruleConfig = ruleConfig;
    }

    [HttpGet("guidance")]
    [ProducesResponseType(typeof(ConflictGuidanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConflictGuidanceDto>> Guidance(
        [FromQuery] string ruleCode,
        [FromQuery] int? timetableEntryId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _intelligence.GetGuidanceAsync(timetableEntryId, ruleCode, academicYearId, timetableId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("impact")]
    [ProducesResponseType(typeof(ImpactGraphDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImpactGraphDto>> Impact(
        [FromQuery] string ruleCode,
        [FromQuery] int? timetableEntryId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct)
    {
        try
        {
            var guidance = await _intelligence.GetGuidanceAsync(timetableEntryId, ruleCode, academicYearId, timetableId, ct);
            return Ok(guidance.Impact);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("dependencies")]
    [ProducesResponseType(typeof(DependencyGraphDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DependencyGraphDto>> Dependencies(
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct)
    {
        try { return Ok(await _intelligence.GetDependencyGraphAsync(academicYearId, timetableId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("explain")]
    [ProducesResponseType(typeof(ConflictExplanationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConflictExplanationDto>> Explain(
        [FromQuery] string ruleCode,
        [FromQuery] int? timetableEntryId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? timetableId,
        CancellationToken ct)
    {
        try
        {
            var guidance = await _intelligence.GetGuidanceAsync(timetableEntryId, ruleCode, academicYearId, timetableId, ct);
            return Ok(guidance.Explanation);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("workspace/enhanced")]
    [ProducesResponseType(typeof(EnhancedConflictWorkspaceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EnhancedConflictWorkspaceDto>> EnhancedWorkspace(
        [FromQuery] int? timetableId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? departmentId,
        [FromQuery] int? staffId,
        [FromQuery] int? roomId,
        [FromQuery] ConflictCategory? category,
        [FromQuery] ConflictSeverity? severity,
        [FromQuery] string? search,
        [FromQuery] bool reanalyze = false,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _intelligence.GetEnhancedWorkspaceAsync(new ConflictWorkspaceQuery
            {
                TimetableId = timetableId,
                AcademicYearId = academicYearId,
                DepartmentId = departmentId,
                StaffId = staffId,
                RoomId = roomId,
                Category = category,
                Severity = severity,
                Search = search,
                Reanalyze = reanalyze,
                UseLatestRun = !reanalyze
            }, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("workspace/pins")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ConflictWorkspacePinDto>> Pin([FromBody] UpsertConflictPinRequest request, CancellationToken ct) =>
        Ok(await _intelligence.PinAsync(request, ct));

    [HttpPost("workspace/notes")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ConflictWorkspaceNoteDto>> Note([FromBody] UpsertConflictNoteRequest request, CancellationToken ct) =>
        Ok(await _intelligence.AddNoteAsync(request, ct));

    [HttpPost("workspace/bookmarks")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ConflictWorkspaceBookmarkDto>> Bookmark([FromBody] UpsertConflictBookmarkRequest request, CancellationToken ct) =>
        Ok(await _intelligence.SaveBookmarkAsync(request, ct));

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(ConflictAnalyticsDashboardDto), StatusCodes.Status200OK)]
    public Task<ConflictAnalyticsDashboardDto> Analytics([FromQuery] int? academicYearId, CancellationToken ct) =>
        _analytics.GetDashboardAsync(academicYearId, ct);

    [HttpGet("analytics/export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] int? academicYearId, CancellationToken ct)
    {
        var bytes = await _analytics.ExportExcelAsync(academicYearId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "conflict-analytics.xlsx");
    }

    [HttpGet("analytics/export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] int? academicYearId, CancellationToken ct)
    {
        var bytes = await _analytics.ExportPdfAsync(academicYearId, ct);
        return File(bytes, "application/pdf", "conflict-analytics.pdf");
    }

    [HttpGet("rules/thresholds")]
    [ProducesResponseType(typeof(IReadOnlyList<ConflictRuleThresholdDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConflictRuleThresholdDto>> Thresholds(CancellationToken ct) =>
        _ruleConfig.ListAsync(ct);

    [HttpPut("rules/thresholds")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingConflict)]
    public async Task<ActionResult<ConflictRuleThresholdDto>> UpdateThreshold(
        [FromBody] UpdateConflictRuleThresholdRequest request,
        CancellationToken ct)
    {
        try { return Ok(await _ruleConfig.UpdateAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("rules/thresholds/history")]
    public Task<IReadOnlyList<ConflictRuleConfigHistoryDto>> ThresholdHistory(
        [FromQuery] string? thresholdKey,
        CancellationToken ct) =>
        _ruleConfig.GetHistoryAsync(thresholdKey, ct);
}
