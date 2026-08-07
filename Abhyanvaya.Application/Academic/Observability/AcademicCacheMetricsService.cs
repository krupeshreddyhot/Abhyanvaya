using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicCacheMetricsService : IAcademicCacheMetricsService
{
    private readonly AcademicMetricsStore _store;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly AcademicPlatformOptions _options;

    public AcademicCacheMetricsService(
        AcademicMetricsStore store,
        IAcademicTelemetryService telemetry,
        IOptions<AcademicPlatformOptions> options)
    {
        _store = store;
        _telemetry = telemetry;
        _options = options.Value;
    }

    public void RecordHierarchyHit(TimeSpan retrieval)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _telemetry.RecordCacheHit("hierarchy");
        _store.RecordDurationMs("cache.hierarchy.retrieval", retrieval.TotalMilliseconds);
    }

    public void RecordHierarchyMiss(TimeSpan retrieval)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _telemetry.RecordCacheMiss("hierarchy");
        _store.RecordDurationMs("cache.hierarchy.retrieval", retrieval.TotalMilliseconds);
    }

    public void RecordStatisticsHit(TimeSpan retrieval)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _telemetry.RecordCacheHit("statistics");
        _store.RecordDurationMs("cache.statistics.retrieval", retrieval.TotalMilliseconds);
    }

    public void RecordStatisticsMiss(TimeSpan retrieval)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _telemetry.RecordCacheMiss("statistics");
        _store.RecordDurationMs("cache.statistics.retrieval", retrieval.TotalMilliseconds);
    }

    public void RecordWarm()
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment("cache.warm");
    }

    public void RecordRefresh()
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment("cache.refresh");
    }

    public void RecordInvalidate()
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment("cache.invalidate");
    }

    public AcademicCacheMetricsDto GetMetrics() => _store.GetCacheMetrics();
}
