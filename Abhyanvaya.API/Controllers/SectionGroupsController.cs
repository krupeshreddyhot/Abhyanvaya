using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/section-groups")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class SectionGroupsController : ControllerBase
{
    private readonly ISectionGroupService _groups;

    public SectionGroupsController(ISectionGroupService groups) => _groups = groups;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SectionGroupDto>>> List(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _groups.ListAsync(academicYearId, semesterId, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SectionGroupDto>> Get(int id, CancellationToken cancellationToken)
    {
        var row = await _groups.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanEditSections)]
    public async Task<ActionResult<SectionGroupDto>> Create([FromBody] CreateSectionGroupRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _groups.CreateAsync(request, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}/members")]
    [Authorize(Policy = AuthorizationPolicies.CanEditSections)]
    public async Task<ActionResult<SectionGroupDto>> UpdateMembers(
        int id,
        [FromBody] UpdateSectionGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _groups.UpdateMembersAsync(id, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
