using System.Diagnostics;
using Abhyanvaya.Application.Academic.Architecture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicHealthService : IAcademicHealthService
{
    private readonly IAcademicCatalogService _catalog;
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicHierarchyCache _hierarchyCache;
    private readonly IAcademicStatisticsCache _statisticsCache;
    private readonly IAcademicDomainEventMetrics _events;
    private readonly AcademicPlatformOptions _options;
    private readonly ILogger<AcademicHealthService> _logger;

    public AcademicHealthService(
        IAcademicCatalogService catalog,
        IAcademicTreeService tree,
        IAcademicHierarchyCache hierarchyCache,
        IAcademicStatisticsCache statisticsCache,
        IAcademicDomainEventMetrics events,
        IOptions<AcademicPlatformOptions> options,
        ILogger<AcademicHealthService> logger)
    {
        _catalog = catalog;
        _tree = tree;
        _hierarchyCache = hierarchyCache;
        _statisticsCache = statisticsCache;
        _events = events;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AcademicHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<AcademicComponentHealth>();

        // Hierarchy cache — advisory probe
        var sw = Stopwatch.StartNew();
        try
        {
            _ = await _hierarchyCache.GetProgramsAsync(cancellationToken);
            sw.Stop();
            components.Add(CheckDuration("HierarchyCache", sw.Elapsed.TotalMilliseconds, AcademicPerformanceBudgets.HierarchyCacheMs));
        }
        catch (Exception ex)
        {
            sw.Stop();
            components.Add(new AcademicComponentHealth
            {
                Component = "HierarchyCache",
                Level = AcademicHealthLevel.Critical,
                Message = ex.Message,
                DurationMs = sw.Elapsed.TotalMilliseconds,
            });
        }

        sw.Restart();
        try
        {
            _ = await _statisticsCache.GetStatisticsAsync(cancellationToken);
            sw.Stop();
            components.Add(CheckDuration("StatisticsCache", sw.Elapsed.TotalMilliseconds, AcademicPerformanceBudgets.StatisticsCacheMs));
        }
        catch (Exception ex)
        {
            sw.Stop();
            components.Add(new AcademicComponentHealth
            {
                Component = "StatisticsCache",
                Level = AcademicHealthLevel.Warning,
                Message = ex.Message,
                DurationMs = sw.Elapsed.TotalMilliseconds,
            });
        }

        sw.Restart();
        try
        {
            var tree = await _tree.BuildTreeAsync(cancellationToken: cancellationToken);
            sw.Stop();
            var level = sw.Elapsed.TotalMilliseconds <= AcademicPerformanceBudgets.TreeBuildMs
                ? AcademicHealthLevel.Healthy
                : AcademicHealthLevel.Warning;
            components.Add(new AcademicComponentHealth
            {
                Component = "AcademicTree",
                Level = level,
                Message = $"Nodes={tree.TotalNodes}",
                DurationMs = sw.Elapsed.TotalMilliseconds,
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            components.Add(new AcademicComponentHealth
            {
                Component = "AcademicTree",
                Level = AcademicHealthLevel.Critical,
                Message = ex.Message,
                DurationMs = sw.Elapsed.TotalMilliseconds,
            });
        }

        try
        {
            var cfg = await _catalog.GetConfigurationAsync(cancellationToken);
            components.Add(new AcademicComponentHealth
            {
                Component = "ProgramConfiguration",
                Level = AcademicHealthLevel.Healthy,
                Message = $"EnablePrograms={cfg.EnablePrograms}",
            });
        }
        catch (Exception ex)
        {
            components.Add(new AcademicComponentHealth
            {
                Component = "ProgramConfiguration",
                Level = AcademicHealthLevel.Warning,
                Message = ex.Message,
            });
        }

        components.Add(new AcademicComponentHealth
        {
            Component = "AcademicStructure",
            Level = AcademicHealthLevel.Healthy,
            Message = "Structure services registered",
        });

        var eventMetrics = _events.GetMetrics();
        var failed = eventMetrics.Sum(e => e.Failed);
        components.Add(new AcademicComponentHealth
        {
            Component = "DomainEvents",
            Level = failed > 0 ? AcademicHealthLevel.Warning : AcademicHealthLevel.Healthy,
            Message = $"Failed={failed}",
        });

        var guard = AcademicArchitectureGuard.Validate();
        components.Add(new AcademicComponentHealth
        {
            Component = "ArchitectureGuard",
            Level = guard.Passed ? AcademicHealthLevel.Healthy : AcademicHealthLevel.Warning,
            Message = guard.Passed ? "Passed" : $"{guard.Violations.Count} violation(s)",
        });

        components.Add(new AcademicComponentHealth
        {
            Component = "ObservabilityFlags",
            Level = AcademicHealthLevel.Healthy,
            Message = $"Telemetry={_options.EnableTelemetry}; Perf={_options.EnablePerformanceMetrics}; Arch={_options.EnableArchitectureMetrics}",
        });

        var overall = components.Any(c => c.Level == AcademicHealthLevel.Critical)
            ? AcademicHealthLevel.Critical
            : components.Any(c => c.Level == AcademicHealthLevel.Warning)
                ? AcademicHealthLevel.Warning
                : AcademicHealthLevel.Healthy;

        _logger.LogInformation(
            "Academic health Overall={Overall} Components={Count}",
            overall, components.Count);

        return new AcademicHealthReport
        {
            Overall = overall,
            GeneratedUtc = DateTime.UtcNow,
            Components = components,
        };
    }

    private static AcademicComponentHealth CheckDuration(string name, double ms, double budget) => new()
    {
        Component = name,
        Level = ms <= budget ? AcademicHealthLevel.Healthy : AcademicHealthLevel.Warning,
        Message = ms <= budget ? "Within budget" : $"Exceeded budget {budget}ms",
        DurationMs = ms,
    };
}
