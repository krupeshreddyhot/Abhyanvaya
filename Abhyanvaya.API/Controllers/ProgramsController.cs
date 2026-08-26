using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/programs")]
[Authorize(Policy = AuthorizationPolicies.CanViewProgramCatalog)]
public sealed class ProgramsController : ControllerBase
{
    private readonly IAcademicStructureService _service;

    public ProgramsController(IAcademicStructureService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProgramDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetProgramsAsync(includeInactive, cancellationToken));

    [HttpGet("department-options")]
    public async Task<ActionResult<IReadOnlyList<ProgramDepartmentOptionDto>>> DepartmentOptions(
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetProgramDepartmentOptionsAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProgramDto>> Get(int id, CancellationToken cancellationToken)
    {
        var row = await _service.GetProgramAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanCreatePrograms)]
    public async Task<ActionResult<ProgramDto>> Create([FromBody] CreateProgramRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateProgramAsync(request, cancellationToken)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanEditPrograms)]
    public async Task<ActionResult<ProgramDto>> Update(int id, [FromBody] UpdateProgramRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpdateProgramAsync(id, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanEditPrograms)]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.ArchiveProgramAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanDeletePrograms)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteProgramAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<IReadOnlyList<ProgramStatisticsDto>>> Statistics(CancellationToken cancellationToken)
        => Ok(await _service.GetProgramStatisticsAsync(cancellationToken));

    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<ProgramDto>> Summary(int id, CancellationToken cancellationToken)
    {
        var row = await _service.GetProgramSummaryAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("{id:int}/hierarchy")]
    public async Task<ActionResult<AcademicHierarchyDto>> Hierarchy(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramHierarchyAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/courses")]
    public async Task<IActionResult> Courses(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramCoursesAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/groups")]
    public async Task<IActionResult> Groups(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramGroupsAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/semesters")]
    public async Task<IActionResult> Semesters(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramSemestersAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/sections")]
    public async Task<ActionResult<IReadOnlyList<SectionDto>>> Sections(int id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramSectionsAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:int}/policy")]
    public async Task<ActionResult<ProgramPolicyDto>> GetPolicy(int id, CancellationToken cancellationToken)
    {
        if (await _service.GetProgramAsync(id, cancellationToken) is null)
            return NotFound();
        var row = await _service.GetProgramPolicyAsync(id, cancellationToken);
        return row is null ? NoContent() : Ok(row);
    }

    [HttpPut("{id:int}/policy")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<ActionResult<ProgramPolicyDto>> UpsertPolicy(
        int id,
        [FromBody] UpsertProgramPolicyRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpsertProgramPolicyAsync(id, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:int}/student-count")]
    public async Task<ActionResult<int>> StudentCount(int id, CancellationToken cancellationToken)
        => Ok(await _service.GetProgramStudentCountAsync(id, cancellationToken));

    [HttpGet("{id:int}/faculty-count")]
    public async Task<ActionResult<int>> FacultyCount(int id, CancellationToken cancellationToken)
        => Ok(await _service.GetProgramFacultyCountAsync(id, cancellationToken));

    [HttpGet("{id:int}/course-count")]
    public async Task<ActionResult<int>> CourseCount(int id, CancellationToken cancellationToken)
        => Ok(await _service.GetProgramCourseCountAsync(id, cancellationToken));

    [HttpPost("assign-course")]
    [Authorize(Policy = AuthorizationPolicies.CanAssignCourseToProgram)]
    public async Task<IActionResult> AssignCourse([FromBody] AssignCourseProgramRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _service.AssignCourseToProgramAsync(request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/academic-structure")]
[Authorize(Policy = AuthorizationPolicies.CanViewProgramCatalog)]
public sealed class AcademicStructureController : ControllerBase
{
    private readonly IAcademicStructureService _service;

    public AcademicStructureController(IAcademicStructureService service) => _service = service;

    /// <summary>Read-only hierarchical tree (Program optional via EnablePrograms).</summary>
    [HttpGet]
    public async Task<ActionResult<AcademicHierarchyDto>> Get(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeSections = true,
        [FromQuery] bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAcademicHierarchyAsync(includeInactive, includeSections, includeSubjects, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<AcademicHierarchyStatisticsDto>> Statistics(CancellationToken cancellationToken)
        => Ok(await _service.GetHierarchyStatisticsAsync(cancellationToken));

    [HttpGet("configuration")]
    public async Task<ActionResult<TenantAcademicConfigurationDto>> GetConfiguration(CancellationToken cancellationToken)
        => Ok(await _service.GetConfigurationAsync(cancellationToken));

    [HttpPut("configuration")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<ActionResult<TenantAcademicConfigurationDto>> UpdateConfiguration(
        [FromBody] UpdateTenantAcademicConfigurationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateConfigurationAsync(request, cancellationToken));
}
