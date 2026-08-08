using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>AI29.1B.7 — Read-only allocation platform APIs (no student assignment).</summary>
[ApiController]
[Route("api/allocation")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class AllocationPlatformController : ControllerBase
{
    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IAllocationReadinessService _readiness;
    private readonly IAllocationHealthService _health;
    private readonly IAllocationSnapshotService _snapshots;
    private readonly IAllocationContextCache _cache;

    public AllocationPlatformController(
        ISectionAllocationContextBuilder builder,
        IAllocationReadinessService readiness,
        IAllocationHealthService health,
        IAllocationSnapshotService snapshots,
        IAllocationContextCache cache)
    {
        _builder = builder;
        _readiness = readiness;
        _health = health;
        _snapshots = snapshots;
        _cache = cache;
    }

    [HttpGet("context")]
    public async Task<ActionResult<SectionAllocationContext>> GetContext(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scope = Scope(academicYearId, courseId, groupId, semesterId);
            if (!refresh)
            {
                var cached = await _cache.GetAsync(scope, cancellationToken);
                if (cached is not null) return Ok(cached);
            }
            var ctx = refresh
                ? await _builder.RefreshAsync(scope, cancellationToken)
                : await _builder.BuildAsync(scope, cancellationToken);
            await _cache.SetAsync(scope, ctx, cancellationToken);
            return Ok(ctx);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("readiness")]
    public async Task<ActionResult<AllocationReadinessReport>> GetReadiness(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _readiness.EvaluateAsync(Scope(academicYearId, courseId, groupId, semesterId), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("health")]
    public async Task<ActionResult<AllocationHealthReport>> GetHealth(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _health.EvaluateAsync(Scope(academicYearId, courseId, groupId, semesterId), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<object>> GetSnapshot(
        [FromQuery] Guid? snapshotId,
        [FromQuery] int? academicYearId,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? semesterId,
        [FromQuery] bool create = false,
        CancellationToken cancellationToken = default)
    {
        if (snapshotId is Guid id)
        {
            var row = await _snapshots.GetAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }

        if (create)
        {
            if (academicYearId is null || courseId is null || groupId is null || semesterId is null)
                return BadRequest("Scope query parameters required to create a snapshot.");
            try
            {
                var scope = Scope(academicYearId.Value, courseId.Value, groupId.Value, semesterId.Value);
                return Ok(await _builder.SnapshotAsync(scope, cancellationToken));
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        AllocationScopeRequest? scopeFilter = null;
        if (academicYearId is > 0 && courseId is > 0 && groupId is > 0 && semesterId is > 0)
            scopeFilter = Scope(academicYearId.Value, courseId.Value, groupId.Value, semesterId.Value);
        return Ok(await _snapshots.ListAsync(scopeFilter, cancellationToken));
    }

    [HttpGet("validation")]
    public async Task<ActionResult<AllocationValidationReport>> Validate(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _builder.ValidateAsync(Scope(academicYearId, courseId, groupId, semesterId), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("composition")]
    public async Task<ActionResult<AllocationContextCompositionReport>> Composition(CancellationToken cancellationToken = default)
    {
        var report = await _builder.GetLastCompositionReportAsync(cancellationToken);
        return report is null ? NotFound("Build a context first.") : Ok(report);
    }

    [HttpGet("analysis")]
    public async Task<ActionResult<SectionAllocationAnalysisContext>> Analysis(
        [FromQuery] int academicYearId,
        [FromQuery] int courseId,
        [FromQuery] int groupId,
        [FromQuery] int semesterId,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _builder.BuildAnalysisContextAsync(Scope(academicYearId, courseId, groupId, semesterId), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("constraints")]
    public ActionResult<IReadOnlyList<AllocationConstraintDescriptor>> Constraints()
        => Ok(AllocationConstraintRegistry.All);

    [HttpGet("architecture-report")]
    public ActionResult<AllocationArchitectureReport> ArchitectureReport()
        => Ok(AcademicArchitectureGuard.ValidateAllocationBoundaries());

    private static AllocationScopeRequest Scope(int year, int course, int group, int semester) => new()
    {
        AcademicYearId = year,
        CourseId = course,
        GroupId = group,
        SemesterId = semester,
    };
}
