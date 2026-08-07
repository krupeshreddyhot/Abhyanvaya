using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.Extensions.Options;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1A.7 — Academic platform observability contracts & regressions.</summary>
public class AI29_1A7_ObservabilityTests
{
    [Fact]
    public void MetricsStore_Calculates_Cache_Hit_Rate()
    {
        var store = new AcademicMetricsStore();
        store.Increment("cache.hierarchy.hit", 8);
        store.Increment("cache.hierarchy.miss", 2);
        store.RecordDurationMs("cache.hierarchy.retrieval", 12);
        var metrics = store.GetCacheMetrics();
        Assert.Equal(80, metrics.HierarchyHitRatePercent);
        Assert.Equal(8, metrics.HierarchyHits);
        Assert.Equal(2, metrics.HierarchyMisses);
    }

    [Fact]
    public void MetricsStore_Records_Percentiles()
    {
        var store = new AcademicMetricsStore();
        foreach (var ms in new[] { 10d, 20d, 30d, 40d, 50d, 60d, 70d, 80d, 90d, 100d })
            store.RecordDurationMs(AcademicOperations.TreeBuild, ms);

        var op = store.GetOperationMetrics(AcademicOperations.TreeBuild, AcademicPerformanceBudgets.TreeBuildMs);
        Assert.Equal(10, op.ExecutionCount);
        Assert.True(op.P95Ms >= op.AverageMs);
        Assert.True(op.P99Ms >= op.P95Ms);
    }

    [Fact]
    public void DomainEventMetrics_Tracks_Published_Succeeded()
    {
        var store = new AcademicMetricsStore();
        var svc = new AcademicDomainEventMetrics(store, Options.Create(new AcademicPlatformOptions
        {
            EnablePerformanceMetrics = true,
        }));
        svc.RecordPublished("ProgramCreated");
        svc.RecordSucceeded("ProgramCreated", TimeSpan.FromMilliseconds(3));
        var row = svc.GetMetrics().First(m => m.EventName == "ProgramCreated");
        Assert.Equal(1, row.Published);
        Assert.Equal(1, row.Succeeded);
        Assert.Equal(0, row.Failed);
    }

    [Fact]
    public void PerformanceBudgets_Are_Documented()
    {
        Assert.Equal(50, AcademicPerformanceBudgets.HierarchyCacheMs);
        Assert.Equal(30, AcademicPerformanceBudgets.StatisticsCacheMs);
        Assert.Equal(100, AcademicPerformanceBudgets.TreeBuildMs);
        Assert.Equal(40, AcademicPerformanceBudgets.SearchMs);
        Assert.Equal(20, AcademicPerformanceBudgets.BreadcrumbMs);
        Assert.Equal(150, AcademicPerformanceBudgets.AcademicStructureApiMs);
    }

    [Fact]
    public void PerformanceMonitor_WithinBudget_When_Empty()
    {
        var monitor = new AcademicPerformanceMonitor(new AcademicMetricsStore());
        var report = monitor.GetReport();
        Assert.True(report.AllWithinBudget);
        Assert.Contains(report.Operations, o => o.Operation == AcademicOperations.TreeBuild);
    }

    [Fact]
    public void PlatformOptions_Default_Flags()
    {
        var options = new AcademicPlatformOptions();
        Assert.True(options.EnableTelemetry);
        Assert.True(options.EnablePerformanceMetrics);
        Assert.True(options.EnableArchitectureMetrics);
        Assert.False(options.EnableSnapshots);
    }

    [Fact]
    public void ArchitectureTrend_Entity_Exists()
    {
        var trend = new AcademicArchitectureTrend
        {
            Score = 100,
            RecordedUtc = DateTime.UtcNow,
            DependencyViolations = 0,
            ForbiddenReferences = 0,
            LayerViolations = 0,
            Summary = "ok",
        };
        Assert.Equal(100, trend.Score);
        Assert.True(typeof(AcademicArchitectureTrend).IsSubclassOf(typeof(Abhyanvaya.Domain.Common.BaseEntity)));
    }

    [Fact]
    public void HealthLevels_Are_Advisory_Only()
    {
        var levels = Enum.GetNames<AcademicHealthLevel>();
        Assert.Contains("Healthy", levels);
        Assert.Contains("Warning", levels);
        Assert.Contains("Critical", levels);
        Assert.Null(typeof(IAcademicHealthService).GetMethod("InvalidateCaches"));
        Assert.Null(typeof(IAcademicHealthService).GetMethod("Repair"));
    }

    [Fact]
    public void ActivitySource_Is_Vendor_Neutral()
    {
        Assert.Equal("Abhyanvaya.Academic", AcademicTelemetryService.ActivitySource.Name);
    }

    [Fact]
    public void Metrics_Api_Route_Contract()
    {
        const string route = "api/v1/academic-platform/metrics";
        Assert.StartsWith("api/v1/academic-platform", route);
        Assert.DoesNotContain("dashboard", route, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Regression_AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
        Assert.Null(typeof(Program).GetProperty("AttendanceSessionId"));
    }

    [Fact]
    public void CacheMetricsService_Records_Warm_Refresh_Invalidate()
    {
        var store = new AcademicMetricsStore();
        var telemetry = new NoOpTelemetry();
        var svc = new AcademicCacheMetricsService(
            store,
            telemetry,
            Options.Create(new AcademicPlatformOptions { EnablePerformanceMetrics = true }));
        svc.RecordWarm();
        svc.RecordRefresh();
        svc.RecordInvalidate();
        var m = svc.GetMetrics();
        Assert.Equal(1, m.WarmCount);
        Assert.Equal(1, m.RefreshCount);
        Assert.Equal(1, m.InvalidateCount);
    }

    private sealed class NoOpTelemetry : IAcademicTelemetryService
    {
        public Task<T> TrackAsync<T>(string operationName, string spanName, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);
        public Task TrackAsync(string operationName, string spanName, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);
        public void RecordCacheHit(string cacheKind) { }
        public void RecordCacheMiss(string cacheKind) { }
        public void RecordDuration(string metricName, TimeSpan duration) { }
    }
}
