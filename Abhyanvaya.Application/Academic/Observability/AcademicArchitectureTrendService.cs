using System.Diagnostics;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicArchitectureTrendService : IAcademicArchitectureTrendService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AcademicMetricsStore _store;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly AcademicPlatformOptions _options;
    private readonly ILogger<AcademicArchitectureTrendService> _logger;

    public AcademicArchitectureTrendService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        AcademicMetricsStore store,
        IAcademicTelemetryService telemetry,
        IOptions<AcademicPlatformOptions> options,
        ILogger<AcademicArchitectureTrendService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _store = store;
        _telemetry = telemetry;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ArchitectureTrendReportDto> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return await _telemetry.TrackAsync(
            AcademicOperations.ArchitectureGuard,
            "AcademicArchitecture.Guard",
            async ct =>
            {
                var sw = Stopwatch.StartNew();
                var report = AcademicArchitectureGuard.Validate();
                sw.Stop();

                var violations = report.Violations.Count;
                var score = report.Passed ? 100 : Math.Max(0, 100 - violations * 10);
                var dependency = report.Violations.Count(v => v.Contains("depend", StringComparison.OrdinalIgnoreCase)
                    || v.Contains("Cyclic", StringComparison.OrdinalIgnoreCase));
                var forbidden = report.Violations.Count(v => v.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                    || v.Contains("references UI", StringComparison.OrdinalIgnoreCase)
                    || v.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
                var layer = report.Violations.Count(v => v.Contains("Domain assembly", StringComparison.OrdinalIgnoreCase)
                    || v.Contains("layer", StringComparison.OrdinalIgnoreCase));

                if (_options.EnableArchitectureMetrics)
                {
                    // Persist without blocking caller semantics: awaited here only when Capture is invoked
                    // by background worker or explicit API; request path should use GetReport or fire-and-forget.
                    await _db.AddAsync(new AcademicArchitectureTrend
                    {
                        TenantId = _currentUser.TenantId > 0 ? _currentUser.TenantId : 0,
                        RecordedUtc = DateTime.UtcNow,
                        Score = score,
                        DependencyViolations = dependency,
                        ForbiddenReferences = forbidden,
                        LayerViolations = layer,
                        Summary = report.Passed
                            ? "Architecture guard passed"
                            : string.Join("; ", report.Violations.Take(5)),
                        CreatedDate = DateTime.UtcNow,
                    });
                    await _db.SaveChangesAsync(ct);
                }

                _logger.LogInformation(
                    "Architecture guard trend Score={Score} Violations={Violations} DurationMs={DurationMs} Passed={Passed}",
                    score, violations, sw.Elapsed.TotalMilliseconds, report.Passed);

                return await GetReportAsync(30, ct);
            },
            cancellationToken);
    }

    public async Task<ArchitectureTrendReportDto> GetReportAsync(int take = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var rows = await _db.AcademicArchitectureTrends.AsNoTracking()
            .Where(t => tenantId <= 0 || t.TenantId == tenantId || t.TenantId == 0)
            .OrderByDescending(t => t.RecordedUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            var live = AcademicArchitectureGuard.Validate();
            var score = live.Passed ? 100 : Math.Max(0, 100 - live.Violations.Count * 10);
            return new ArchitectureTrendReportDto
            {
                GeneratedUtc = DateTime.UtcNow,
                LatestScore = score,
                LatestViolationCount = live.Violations.Count,
                History = [],
            };
        }

        var latest = rows[0];
        return new ArchitectureTrendReportDto
        {
            GeneratedUtc = DateTime.UtcNow,
            LatestScore = latest.Score,
            LatestViolationCount = latest.DependencyViolations + latest.ForbiddenReferences + latest.LayerViolations,
            History = rows.Select(r => new ArchitectureTrendPointDto
            {
                RecordedUtc = r.RecordedUtc,
                Score = r.Score,
                DependencyViolations = r.DependencyViolations,
                ForbiddenReferences = r.ForbiddenReferences,
                LayerViolations = r.LayerViolations,
                Summary = r.Summary,
            }).Reverse().ToList(),
        };
    }
}
