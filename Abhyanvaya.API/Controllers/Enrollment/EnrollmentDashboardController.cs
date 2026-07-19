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
public sealed class EnrollmentDashboardController : EnrollmentControllerBase
{
    private readonly IEnrollmentDashboardService _dashboardService;
    private readonly ITenantContextService _tenantContextService;

    public EnrollmentDashboardController(
        IEnrollmentDashboardService dashboardService,
        ITenantContextService tenantContextService)
    {
        _dashboardService = dashboardService;
        _tenantContextService = tenantContextService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(EnrollmentDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnrollmentDashboardResponse>> GetDashboard(
        [FromQuery] int? collegeId,
        CancellationToken cancellationToken)
    {
        var resolution = _tenantContextService.ResolveForOperation();
        var (tenantId, _, contextCollegeId) = MapResolution(resolution);
        var result = await _dashboardService.GetDashboardAsync(tenantId, collegeId ?? contextCollegeId, cancellationToken);
        return Ok(result);
    }
}
