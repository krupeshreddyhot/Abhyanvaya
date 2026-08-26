using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 7 — Explicit disposable TimetableEntry → TeachingGroup conversion.
/// Not a production backfill; never invoked automatically.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
[Route("api/scheduling/legacy-teaching-group-conversion")]
public sealed class LegacyTimetableTeachingGroupConversionController : ControllerBase
{
    private readonly ILegacyTimetableTeachingGroupConversionService _conversion;

    public LegacyTimetableTeachingGroupConversionController(
        ILegacyTimetableTeachingGroupConversionService conversion)
        => _conversion = conversion;

    /// <summary>Identify TimetableEntries with TeachingGroupId == null (read-only).</summary>
    [HttpGet("entries-without-teaching-group")]
    public async Task<ActionResult<IReadOnlyList<LegacyTimetableEntryWithoutTeachingGroupDto>>> ListWithoutTeachingGroup(
        [FromQuery] int? timetableId,
        CancellationToken cancellationToken)
        => Ok(await _conversion.ListEntriesWithoutTeachingGroupAsync(timetableId, cancellationToken));

    /// <summary>
    /// Explicit conversion of selected TimetableEntry → TeachingGroup mappings.
    /// Supports dryRun. Never creates TeachingGroups or infers from SubjectAllocation.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LegacyTimetableConversionReportDto>> Convert(
        [FromBody] ConvertLegacyTimetableEntriesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _conversion.ConvertAsync(request, cancellationToken));
        }
        catch (DomainException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
