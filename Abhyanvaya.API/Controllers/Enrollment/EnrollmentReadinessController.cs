using Abhyanvaya.API.Common;
using Abhyanvaya.API.Filters;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Enrollment;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanViewEnrollment)]
[RequireOperationalContext]
[Route("api/enrollment")]
public sealed class EnrollmentReadinessController : EnrollmentControllerBase
{
    private readonly IEnrollmentReadinessService _readinessService;
    private readonly ITenantContextService _tenantContextService;

    public EnrollmentReadinessController(
        IEnrollmentReadinessService readinessService,
        ITenantContextService tenantContextService)
    {
        _readinessService = readinessService;
        _tenantContextService = tenantContextService;
    }

    [HttpGet("readiness")]
    [ProducesResponseType(typeof(EnrollmentReadinessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnrollmentReadinessResult>> GetReadiness(
        [FromQuery] int? collegeId,
        [FromQuery] int academicYear,
        [FromQuery] int? courseId,
        [FromQuery] int? groupId,
        [FromQuery] int? batch,
        [FromQuery] int? subjectId,
        [FromQuery] bool forceReEnrollment = false,
        CancellationToken cancellationToken = default)
    {
        var resolution = _tenantContextService.ResolveForOperation();
        var (tenantId, _, contextCollegeId) = MapResolution(resolution);
        var effectiveCollegeId = collegeId ?? contextCollegeId;
        if (effectiveCollegeId is null or <= 0)
        {
            return BadRequest(new { errorCode = "ContextRequired", message = "A college context is required for readiness evaluation." });
        }

        var preview = new EnrollmentPreviewRequest
        {
            TenantId = tenantId,
            CollegeId = effectiveCollegeId.Value,
            AcademicYear = academicYear,
            CourseId = courseId,
            GroupId = groupId,
            Batch = batch,
            SubjectId = subjectId,
            ForceReEnrollment = forceReEnrollment,
        };

        var result = await _readinessService.EvaluateAsync(
            tenantId,
            effectiveCollegeId.Value,
            academicYear,
            preview,
            cancellationToken);

        return Ok(result);
    }
}
