namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicPlatformMetricsService : IAcademicPlatformMetricsService
{
    private readonly AcademicMetricsStore _store;
    private readonly IAcademicCacheMetricsService _cacheMetrics;
    private readonly IAcademicPerformanceMonitor _performance;
    private readonly IAcademicDomainEventMetrics _events;
    private readonly IAcademicHealthService _health;
    private readonly IAcademicArchitectureTrendService _trends;

    public AcademicPlatformMetricsService(
        AcademicMetricsStore store,
        IAcademicCacheMetricsService cacheMetrics,
        IAcademicPerformanceMonitor performance,
        IAcademicDomainEventMetrics events,
        IAcademicHealthService health,
        IAcademicArchitectureTrendService trends)
    {
        _store = store;
        _cacheMetrics = cacheMetrics;
        _performance = performance;
        _events = events;
        _health = health;
        _trends = trends;
    }

    public async Task<AcademicPlatformMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var cache = _cacheMetrics.GetMetrics();
        var perf = _performance.GetReport();
        var health = await _health.GetHealthAsync(cancellationToken);
        var trends = await _trends.GetReportAsync(10, cancellationToken);
        var tree = _performance.GetOperation(AcademicOperations.TreeBuild);
        var search = _performance.GetOperation(AcademicOperations.Search);
        var crumb = _performance.GetOperation(AcademicOperations.Breadcrumb);

        var totalHits = cache.HierarchyHits + cache.StatisticsHits;
        var totalOps = totalHits + cache.HierarchyMisses + cache.StatisticsMisses;

        return new AcademicPlatformMetricsDto
        {
            GeneratedUtc = DateTime.UtcNow,
            CacheHitPercent = totalOps == 0 ? 0 : Math.Round(100.0 * totalHits / totalOps, 2),
            AverageTreeBuildMs = tree.AverageMs,
            AverageSearchMs = search.AverageMs,
            AverageBreadcrumbMs = crumb.AverageMs,
            ArchitectureScore = trends.LatestScore,
            HierarchySize = _store.HierarchySize,
            StatisticsCacheSize = _store.StatisticsCacheSize,
            DomainEvents = _events.GetMetrics(),
            Health = health,
            Cache = cache,
            Performance = perf,
        };
    }

    public AcademicPerformanceReportDto GetPerformanceReport() => _performance.GetReport();
}
