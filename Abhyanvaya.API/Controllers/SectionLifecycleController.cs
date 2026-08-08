using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/sections/lifecycle")]
[Authorize(Policy = AuthorizationPolicies.CanViewSectionLifecycle)]
public sealed class SectionLifecycleController : ControllerBase
{
    private readonly ISectionLifecycleService _lifecycle;

    public SectionLifecycleController(ISectionLifecycleService lifecycle) => _lifecycle = lifecycle;

    [HttpGet("states")]
    public ActionResult<IReadOnlyList<string>> GetStates() => Ok(_lifecycle.GetAllStates());

    [HttpGet("types")]
    public ActionResult<IReadOnlyList<SectionTypeOptionDto>> GetTypes() => Ok(_lifecycle.GetSectionTypes());

    [HttpGet("{sectionId:int}/allowed")]
    public ActionResult<IReadOnlyList<string>> GetAllowed(int sectionId, [FromQuery] string currentStatus)
        => Ok(_lifecycle.GetAllowedTransitions(currentStatus));

    [HttpGet("{sectionId:int}/history")]
    public async Task<ActionResult<IReadOnlyList<SectionLifecycleHistoryDto>>> History(int sectionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _lifecycle.GetHistoryAsync(sectionId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{sectionId:int}/transition")]
    [Authorize(Policy = AuthorizationPolicies.CanEditSectionLifecycle)]
    public async Task<ActionResult<SectionDto>> Transition(
        int sectionId,
        [FromBody] SectionLifecycleTransitionRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _lifecycle.TransitionAsync(sectionId, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
