using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/sections/readiness")]
[Authorize(Policy = AuthorizationPolicies.CanViewSectionReadiness)]
public sealed class SectionReadinessController : ControllerBase
{
    private readonly ISectionReadinessService _readiness;

    public SectionReadinessController(ISectionReadinessService readiness) => _readiness = readiness;

    [HttpGet("{sectionId:int}")]
    public async Task<ActionResult<SectionReadinessDto>> Get(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _readiness.EvaluateAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SectionReadinessDto>>> List(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _readiness.EvaluateManyAsync(academicYearId, semesterId, cancellationToken));
}
