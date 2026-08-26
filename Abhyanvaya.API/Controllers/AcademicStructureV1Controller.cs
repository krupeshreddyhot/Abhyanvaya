using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI29.1A.5 / AI29.1A.6 — Versioned academic structure API. Existing /api/academic-structure remains unchanged.
/// </summary>
[ApiController]
[Route("api/v1/academic-structure")]
[Authorize(Policy = AuthorizationPolicies.CanViewPrograms)]
public sealed class AcademicStructureV1Controller : ControllerBase
{
    private readonly IAcademicStructureService _service;
    private readonly IAcademicHierarchyCache _cache;
    private readonly IAcademicStatisticsCache _statisticsCache;
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicBreadcrumbService _breadcrumbs;
    private readonly IAcademicSearchService _search;
    private readonly IAcademicHierarchySnapshotService _snapshots;

    public AcademicStructureV1Controller(
        IAcademicStructureService service,
        IAcademicHierarchyCache cache,
        IAcademicStatisticsCache statisticsCache,
        IAcademicTreeService tree,
        IAcademicBreadcrumbService breadcrumbs,
        IAcademicSearchService search,
        IAcademicHierarchySnapshotService snapshots)
    {
        _service = service;
        _cache = cache;
        _statisticsCache = statisticsCache;
        _tree = tree;
        _breadcrumbs = breadcrumbs;
        _search = search;
        _snapshots = snapshots;
    }

    [HttpGet]
    public async Task<ActionResult<AcademicHierarchyDto>> Get(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeSections = true,
        [FromQuery] bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAcademicHierarchyAsync(includeInactive, includeSections, includeSubjects, cancellationToken));

    /// <summary>AI29.1A.6 — Immutable read-model projection (not for writes).</summary>
    [HttpGet("read-model")]
    public async Task<ActionResult<AcademicHierarchyReadModel>> GetReadModel(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeSections = true,
        [FromQuery] bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => Ok(await _tree.BuildTreeAsync(includeInactive, includeSections, includeSubjects, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<AcademicHierarchyStatisticsDto>> Statistics(CancellationToken cancellationToken)
    {
        var cached = await _statisticsCache.GetHierarchyStatisticsAsync(cancellationToken);
        if (cached is not null) return Ok(cached);
        var fresh = await _service.GetHierarchyStatisticsAsync(cancellationToken);
        await _statisticsCache.SetHierarchyStatisticsAsync(fresh, cancellationToken);
        return Ok(fresh);
    }

    [HttpGet("configuration")]
    public async Task<ActionResult<TenantAcademicConfigurationDto>> GetConfiguration(CancellationToken cancellationToken)
        => Ok(await _service.GetConfigurationAsync(cancellationToken));

    [HttpPut("configuration")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<ActionResult<TenantAcademicConfigurationDto>> UpdateConfiguration(
        [FromBody] UpdateTenantAcademicConfigurationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateConfigurationAsync(request, cancellationToken));

    [HttpGet("programs/statistics")]
    public async Task<ActionResult<IReadOnlyList<ProgramStatisticsDto>>> ProgramStatistics(CancellationToken cancellationToken)
    {
        var cached = await _statisticsCache.GetStatisticsAsync(cancellationToken);
        if (cached is not null) return Ok(cached);
        var fresh = await _service.GetProgramStatisticsAsync(cancellationToken);
        await _statisticsCache.SetStatisticsAsync(fresh, cancellationToken);
        return Ok(fresh);
    }

    [HttpGet("programs/{programId:int}/hierarchy")]
    public async Task<ActionResult<AcademicHierarchyDto>> ProgramHierarchy(int programId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramHierarchyAsync(programId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("programs/{programId:int}/courses")]
    public async Task<ActionResult<IReadOnlyList<Course>>> ProgramCourses(int programId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramCoursesAsync(programId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("programs/{programId:int}/groups")]
    public async Task<ActionResult<IReadOnlyList<Group>>> ProgramGroups(int programId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramGroupsAsync(programId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("programs/{programId:int}/semesters")]
    public async Task<ActionResult<IReadOnlyList<Semester>>> ProgramSemesters(int programId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramSemestersAsync(programId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("programs/{programId:int}/sections")]
    public async Task<ActionResult<IReadOnlyList<SectionDto>>> ProgramSections(int programId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetProgramSectionsAsync(programId, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("breadcrumb")]
    public async Task<ActionResult<AcademicBreadcrumb>> Breadcrumb([FromQuery] string nodeId, CancellationToken cancellationToken)
        => Ok(await _breadcrumbs.BuildBreadcrumbAsync(nodeId, cancellationToken));

    [HttpGet("breadcrumb/program/{programId:int}")]
    public async Task<ActionResult<AcademicBreadcrumb>> ProgramBreadcrumb(int programId, CancellationToken cancellationToken)
        => Ok(await _breadcrumbs.BuildProgramBreadcrumbAsync(programId, cancellationToken));

    [HttpGet("breadcrumb/course/{courseId:int}")]
    public async Task<ActionResult<AcademicBreadcrumb>> CourseBreadcrumb(int courseId, CancellationToken cancellationToken)
        => Ok(await _breadcrumbs.BuildCourseBreadcrumbAsync(courseId, cancellationToken));

    [HttpGet("breadcrumb/section/{sectionId:int}")]
    public async Task<ActionResult<AcademicBreadcrumb>> SectionBreadcrumb(int sectionId, CancellationToken cancellationToken)
        => Ok(await _breadcrumbs.BuildSectionBreadcrumbAsync(sectionId, cancellationToken));

    // AI29.1D Prompt 16A — operational breadcrumb/context moved to AcademicOperationalContextController
    // (CanViewAcademicOperationalContext; does not require Program.View).

    [HttpGet("search/node")]
    public async Task<ActionResult<AcademicSearchResult>> FindNode([FromQuery] string nodeId, CancellationToken cancellationToken)
    {
        var row = await _search.FindNodeAsync(nodeId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("search/courses")]
    public async Task<ActionResult<IReadOnlyList<AcademicSearchResult>>> FindCourses([FromQuery] string q = "", CancellationToken cancellationToken = default)
        => Ok(await _search.FindCourseAsync(q, cancellationToken));

    [HttpGet("search/semesters")]
    public async Task<ActionResult<IReadOnlyList<AcademicSearchResult>>> FindSemesters([FromQuery] string q = "", CancellationToken cancellationToken = default)
        => Ok(await _search.FindSemesterAsync(q, cancellationToken));

    [HttpGet("search/sections")]
    public async Task<ActionResult<IReadOnlyList<AcademicSearchResult>>> FindSections([FromQuery] string q = "", CancellationToken cancellationToken = default)
        => Ok(await _search.FindSectionAsync(q, cancellationToken));

    [HttpGet("search/subjects")]
    public async Task<ActionResult<IReadOnlyList<AcademicSearchResult>>> FindSubjects([FromQuery] string q = "", CancellationToken cancellationToken = default)
        => Ok(await _search.FindSubjectAsync(q, cancellationToken));

    [HttpGet("programs/{programId:int}/policy")]
    public async Task<ActionResult<ProgramPolicyDto>> GetPolicy(int programId, CancellationToken cancellationToken)
    {
        if (await _service.GetProgramAsync(programId, cancellationToken) is null)
            return NotFound();
        var row = await _service.GetProgramPolicyAsync(programId, cancellationToken);
        return row is null ? NoContent() : Ok(row);
    }

    [HttpPut("programs/{programId:int}/policy")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<ActionResult<ProgramPolicyDto>> UpsertPolicy(
        int programId,
        [FromBody] UpsertProgramPolicyRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpsertProgramPolicyAsync(programId, request, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("architecture/report")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public ActionResult<AcademicArchitectureReport> ArchitectureReport()
        => Ok(AcademicArchitectureGuard.Validate());

    /// <summary>
    /// AI29.1D Prompt 21 — UI → API/Application → Domain architecture compliance report.
    /// </summary>
    [HttpGet("architecture/ai29-1d-report")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public ActionResult<Ai291DArchitectureComplianceReport> Ai291DArchitectureReport(
        [FromServices] IWebHostEnvironment env)
    {
        var root = Ai291DArchitectureGuard.TryResolveRepositoryRoot(env.ContentRootPath)
                   ?? Ai291DArchitectureGuard.TryResolveRepositoryRoot();
        return Ok(Ai291DArchitectureGuard.Validate(root));
    }

    [HttpGet("snapshots/latest")]
    public async Task<IActionResult> LatestSnapshot(CancellationToken cancellationToken)
    {
        if (!_snapshots.IsEnabled) return StatusCode(StatusCodes.Status501NotImplemented, "Daily snapshots are disabled.");
        var row = await _snapshots.GetLatestAsync(cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("snapshots/generate")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> GenerateSnapshot(CancellationToken cancellationToken)
    {
        if (!_snapshots.IsEnabled) return StatusCode(StatusCodes.Status501NotImplemented, "Daily snapshots are disabled.");
        var row = await _snapshots.GenerateTodayAsync(cancellationToken);
        return Ok(row);
    }

    [HttpPost("cache/warm")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> WarmCache(CancellationToken cancellationToken)
    {
        await _cache.WarmCacheAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("cache/refresh")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> RefreshCache(CancellationToken cancellationToken)
    {
        await _cache.RefreshCacheAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("cache/invalidate")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> InvalidateCache(CancellationToken cancellationToken)
    {
        await _cache.InvalidateHierarchyAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("statistics-cache/warm")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> WarmStatisticsCache(CancellationToken cancellationToken)
    {
        await _statisticsCache.WarmAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("statistics-cache/refresh")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> RefreshStatisticsCache(CancellationToken cancellationToken)
    {
        await _statisticsCache.RefreshAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("statistics-cache/invalidate")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<IActionResult> InvalidateStatisticsCache(CancellationToken cancellationToken)
    {
        await _statisticsCache.InvalidateAsync(cancellationToken);
        return NoContent();
    }
}
