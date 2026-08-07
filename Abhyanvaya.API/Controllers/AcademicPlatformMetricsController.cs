using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI29.1A.7 — Read-only academic platform observability APIs. Does not modify AI31 Dashboard.
/// </summary>
[ApiController]
[Route("api/v1/academic-platform")]
[Authorize(Policy = AuthorizationPolicies.CanViewPrograms)]
public sealed class AcademicPlatformMetricsController : ControllerBase
{
    private readonly IAcademicPlatformMetricsService _metrics;
    private readonly IAcademicHealthService _health;
    private readonly IAcademicArchitectureTrendService _trends;
    private readonly IAcademicPerformanceMonitor _performance;

    public AcademicPlatformMetricsController(
        IAcademicPlatformMetricsService metrics,
        IAcademicHealthService health,
        IAcademicArchitectureTrendService trends,
        IAcademicPerformanceMonitor performance)
    {
        _metrics = metrics;
        _health = health;
        _trends = trends;
        _performance = performance;
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<AcademicPlatformMetricsDto>> GetMetrics(CancellationToken cancellationToken)
        => Ok(await _metrics.GetMetricsAsync(cancellationToken));

    [HttpGet("health")]
    public async Task<ActionResult<AcademicHealthReport>> GetHealth(CancellationToken cancellationToken)
        => Ok(await _health.GetHealthAsync(cancellationToken));

    [HttpGet("performance")]
    public ActionResult<AcademicPerformanceReportDto> GetPerformance()
        => Ok(_performance.GetReport());

    [HttpGet("architecture/trends")]
    public async Task<ActionResult<ArchitectureTrendReportDto>> GetArchitectureTrends(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
        => Ok(await _trends.GetReportAsync(take, cancellationToken));

    [HttpPost("architecture/trends/capture")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePrograms)]
    public async Task<ActionResult<ArchitectureTrendReportDto>> CaptureArchitectureTrend(CancellationToken cancellationToken)
        => Ok(await _trends.CaptureAsync(cancellationToken));
}
