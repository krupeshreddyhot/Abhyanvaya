using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Scheduling;

/// <summary>
/// AI30 Phase 3.5 — additive configuration experience endpoints.
/// Does not modify timetable generation, attendance APIs, or AttendanceSessionResolver.
/// </summary>
[ApiController]
[Route("api/scheduling/configuration")]
[Authorize(Policy = AuthorizationPolicies.CanViewScheduling)]
public sealed class SchedulingConfigurationExperienceController : ControllerBase
{
    private readonly ISchedulingConfigurationReadinessService _readiness;
    private readonly ISchedulingSetupValidator _validator;

    public SchedulingConfigurationExperienceController(
        ISchedulingConfigurationReadinessService readiness,
        ISchedulingSetupValidator validator)
    {
        _readiness = readiness;
        _validator = validator;
    }

    [HttpGet("readiness")]
    public async Task<ActionResult<SchedulingReadinessSummaryDto>> Readiness(CancellationToken cancellationToken)
        => Ok(await _readiness.GetSummaryAsync(cancellationToken));

    [HttpGet("setup-validation")]
    public async Task<ActionResult<SchedulingSetupValidationDto>> SetupValidation(CancellationToken cancellationToken)
        => Ok(await _validator.ValidateAsync(cancellationToken));
}
