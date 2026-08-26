using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.ReadModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI29.1D Prompt 16A — read-only academic operational context endpoints.
/// Authorized via consumer permissions (Attendance / Sections / Timetable / Allocation / Program.View).
/// Does not require Program.View and does not grant Program write permissions.
/// </summary>
[ApiController]
[Route("api/v1/academic-structure")]
[Authorize(Policy = AuthorizationPolicies.CanViewAcademicOperationalContext)]
public sealed class AcademicOperationalContextController : ControllerBase
{
    private readonly IAcademicBreadcrumbService _breadcrumbs;

    public AcademicOperationalContextController(IAcademicBreadcrumbService breadcrumbs)
    {
        _breadcrumbs = breadcrumbs;
    }

    /// <summary>
    /// Consistent academic context breadcrumb for Attendance / Sections / Faculty / Allocation / Timetable.
    /// </summary>
    [HttpGet("breadcrumb/context")]
    [ProducesResponseType(typeof(AcademicBreadcrumb), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AcademicBreadcrumb>> OperationalContextBreadcrumb(
        [FromQuery] int? programId,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? semesterId,
        [FromQuery] int? sectionId,
        [FromQuery] List<int>? sectionIds,
        [FromQuery] int? subjectId,
        CancellationToken cancellationToken)
    {
        var outcome = await _breadcrumbs.BuildOperationalContextBreadcrumbAsync(
            new AcademicOperationalContext
            {
                ProgramId = programId,
                CourseId = courseId,
                GroupId = groupId,
                SemesterId = semesterId,
                SectionId = sectionId,
                SectionIds = sectionIds,
                SubjectId = subjectId,
            },
            cancellationToken);

        if (!outcome.IsValid)
            return BadRequest(new { message = outcome.Error });

        return Ok(outcome.Breadcrumb);
    }
}
