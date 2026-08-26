using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 2 — Teaching Group management HTTP contract.
/// Controllers never write TimetableSection or mutate TeachingGroup via DbContext.
/// Section mutations flow through <see cref="ITeachingGroupSectionApplicationService"/> → projector.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingTeachingGroup)]
[Route("api/scheduling/teaching-groups")]
public sealed class TeachingGroupsController : ControllerBase
{
    private readonly ITeachingGroupManagementApplicationService _management;
    private readonly ITeachingGroupSectionApplicationService _sections;
    private readonly ITeachingGroupMembershipApplicationService _memberships;

    public TeachingGroupsController(
        ITeachingGroupManagementApplicationService management,
        ITeachingGroupSectionApplicationService sections,
        ITeachingGroupMembershipApplicationService memberships)
    {
        _management = management;
        _sections = sections;
        _memberships = memberships;
    }

    /// <summary>List Teaching Groups for a SubjectAllocation (never auto-creates).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeachingGroupSummaryDto>>> List(
        [FromQuery] int subjectAllocationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _management.ListBySubjectAllocationAsync(subjectAllocationId, cancellationToken));
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TeachingGroupDetailDto>> Get(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _management.GetByIdAsync(id, cancellationToken));
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

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupDetailDto>> Create(
        [FromBody] CreateTeachingGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _management.CreateAsync(request, cancellationToken));
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

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupDetailDto>> Update(
        int id,
        [FromBody] UpdateTeachingGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _management.UpdateAsync(id, request, cancellationToken));
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

    [HttpPost("{id:int}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupDetailDto>> Archive(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _management.ArchiveAsync(id, cancellationToken));
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

    /// <summary>Raw membership overlays (Include/Exclude rows).</summary>
    [HttpGet("{id:int}/memberships")]
    public async Task<ActionResult<IReadOnlyList<TeachingGroupMembershipDto>>> GetMemberships(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.GetMembershipsAsync(id, cancellationToken));
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

    /// <summary>Resolved roster via Model B membership resolver (side-effect free).</summary>
    [HttpGet("{id:int}/resolved-members")]
    public async Task<ActionResult<IReadOnlyList<ResolvedTeachingGroupMemberDto>>> GetResolvedMembers(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.GetResolvedMembersAsync(id, cancellationToken));
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

    [HttpPost("{id:int}/memberships")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupMembershipMutationResultDto>> AddMembers(
        int id,
        [FromBody] AddTeachingGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.AddMembersAsync(id, request, cancellationToken));
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(ex.Message);
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

    [HttpPut("{id:int}/memberships")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupMembershipMutationResultDto>> ReplaceMemberships(
        int id,
        [FromBody] ReplaceTeachingGroupMembershipsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.ReplaceMembershipsAsync(id, request, cancellationToken));
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(ex.Message);
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

    [HttpDelete("{id:int}/memberships/{studentId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupMembershipMutationResultDto>> RemoveMember(
        int id,
        int studentId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.RemoveMemberAsync(id, studentId, cancellationToken));
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(ex.Message);
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

    [HttpGet("{id:int}/sections")]
    public async Task<ActionResult<IReadOnlyList<TeachingGroupSectionDto>>> GetSections(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sections.GetSectionsAsync(id, cancellationToken));
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

    /// <summary>
    /// Replace TeachingGroupSection SoT then project TimetableSection (TG.4A frozen flow).
    /// </summary>
    [HttpPut("{id:int}/sections")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<IReadOnlyList<TeachingGroupSectionDto>>> ReplaceSections(
        int id,
        [FromBody] ReplaceTeachingGroupSectionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sections.ReplaceSectionsAndProjectAsync(
                id,
                request?.SectionIds ?? Array.Empty<int>(),
                cancellationToken));
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

    [HttpPost("{id:int}/sections/{sectionId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<ActionResult<TeachingGroupSectionDto>> AddSection(
        int id,
        int sectionId,
        [FromBody] AddTeachingGroupSectionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sections.AddSectionAndProjectAsync(
                id,
                sectionId,
                request?.IsPrimary ?? false,
                cancellationToken));
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

    [HttpDelete("{id:int}/sections/{sectionId:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTeachingGroup)]
    public async Task<IActionResult> RemoveSection(
        int id,
        int sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _sections.RemoveSectionAndProjectAsync(id, sectionId, cancellationToken);
            return NoContent();
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
