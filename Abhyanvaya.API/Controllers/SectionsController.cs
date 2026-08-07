using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/sections")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class SectionsController : ControllerBase
{
    private readonly ISectionManagementService _service;

    public SectionsController(ISectionManagementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SectionDto>>> GetAll(
        [FromQuery] int? academicYearId,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetSectionsAsync(academicYearId, courseId, groupId, semesterId, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SectionDto>> Get(int id, CancellationToken cancellationToken)
    {
        var row = await _service.GetSectionAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanCreateSections)]
    public async Task<ActionResult<SectionDto>> Create([FromBody] CreateSectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CreateSectionAsync(request, cancellationToken));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanEditSections)]
    public async Task<ActionResult<SectionDto>> Update(int id, [FromBody] UpdateSectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateSectionAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CanDeleteSections)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteSectionAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("ensure-general")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateSections)]
    public async Task<IActionResult> EnsureGeneral(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken)
    {
        await _service.EnsureDefaultGeneralSectionAsync(academicYearId, courseId, groupId, semesterId, cancellationToken);
        return Ok();
    }

    [HttpPost("auto-allocate")]
    [Authorize(Policy = AuthorizationPolicies.CanAssignSectionStudents)]
    public async Task<ActionResult<AutoAllocateSectionsResult>> AutoAllocate(
        [FromBody] AutoAllocateSectionsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.AutoAllocateAsync(request, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<IReadOnlyList<SectionStatisticsDto>>> Statistics(
        [FromQuery] int? academicYearId,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetSectionStatisticsAsync(academicYearId, semesterId, cancellationToken));

    [HttpGet("reports/{kind}")]
    [Authorize(Policy = AuthorizationPolicies.CanViewReports)]
    public async Task<ActionResult<IReadOnlyList<SectionReportRowDto>>> Report(string kind, CancellationToken cancellationToken)
        => Ok(await _service.GetReportAsync(kind, cancellationToken));

    [HttpGet("dashboard/sections")]
    public Task<ActionResult<IReadOnlyList<SectionDto>>> DashboardSections(CancellationToken cancellationToken)
        => GetAll(null, null, null, null, cancellationToken);

    [HttpGet("dashboard/faculty/{sectionId:int}")]
    public async Task<ActionResult<IReadOnlyList<FacultySectionDto>>> DashboardFaculty(int sectionId, CancellationToken cancellationToken)
        => Ok(await _service.GetFacultyPerSectionAsync(sectionId, cancellationToken));

    [HttpGet("dashboard/students/{sectionId:int}")]
    public async Task<ActionResult<IReadOnlyList<StudentSectionDto>>> DashboardStudents(int sectionId, CancellationToken cancellationToken)
        => Ok(await _service.GetStudentsPerSectionAsync(sectionId, cancellationToken));

    [HttpGet("dashboard/combined-sessions")]
    public async Task<ActionResult<IReadOnlyList<TimetableSectionDto>>> CombinedSessions(
        [FromQuery] int? timetableId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetCombinedSessionsAsync(timetableId, cancellationToken));
}

[ApiController]
[Route("api/student-sections")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class StudentSectionsController : ControllerBase
{
    private readonly ISectionManagementService _service;
    public StudentSectionsController(ISectionManagementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentSectionDto>>> Get(
        [FromQuery] int? sectionId,
        [FromQuery] int? studentId,
        [FromQuery] bool currentOnly = true,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetStudentSectionsAsync(sectionId, studentId, currentOnly, cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanAssignSectionStudents)]
    public async Task<ActionResult<StudentSectionDto>> Assign([FromBody] AssignStudentSectionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.AssignStudentAsync(request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("transfer")]
    [Authorize(Policy = AuthorizationPolicies.CanAssignSectionStudents)]
    public async Task<ActionResult<StudentSectionDto>> Transfer([FromBody] TransferStudentSectionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.TransferStudentAsync(request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/faculty-sections")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class FacultySectionsController : ControllerBase
{
    private readonly ISectionManagementService _service;
    public FacultySectionsController(ISectionManagementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FacultySectionDto>>> Get(
        [FromQuery] int? sectionId,
        [FromQuery] int? facultyId,
        [FromQuery] bool currentOnly = true,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetFacultySectionsAsync(sectionId, facultyId, currentOnly, cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanAssignSectionFaculty)]
    public async Task<ActionResult<FacultySectionDto>> Assign([FromBody] AssignFacultySectionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.AssignFacultyAsync(request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/timetable/{timetableId:int}/sections")]
[Authorize(Policy = AuthorizationPolicies.CanViewSchedulingTimetable)]
public sealed class TimetableSectionsController : ControllerBase
{
    private readonly ISectionManagementService _service;
    public TimetableSectionsController(ISectionManagementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimetableSectionDto>>> Get(int timetableId, CancellationToken cancellationToken)
        => Ok(await _service.GetTimetableSectionsAsync(timetableId, cancellationToken));

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.CanManageSchedulingTimetable)]
    public async Task<ActionResult<IReadOnlyList<TimetableSectionDto>>> Set(
        int timetableId,
        [FromBody] SetTimetableSectionsRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.SetTimetableSectionsAsync(timetableId, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
