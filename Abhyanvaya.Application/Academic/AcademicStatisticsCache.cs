using System.Diagnostics;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6/7 — Statistics cache with observability (keys distinct from hierarchy cache).</summary>
public sealed class AcademicStatisticsCache : IAcademicStatisticsCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IServiceProvider _services;
    private readonly IAcademicCacheMetricsService _metrics;
    private readonly AcademicMetricsStore _store;
    private readonly ILogger<AcademicStatisticsCache> _logger;

    public AcademicStatisticsCache(
        ICacheService cache,
        ICurrentUserService currentUser,
        IServiceProvider services,
        IAcademicCacheMetricsService metrics,
        AcademicMetricsStore store,
        ILogger<AcademicStatisticsCache> logger)
    {
        _cache = cache;
        _currentUser = currentUser;
        _services = services;
        _metrics = metrics;
        _store = store;
        _logger = logger;
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        var hierarchy = _services.GetRequiredService<IAcademicHierarchyService>();
        var programStats = await hierarchy.GetProgramStatisticsAsync(cancellationToken);
        var hierarchyStats = await hierarchy.GetHierarchyStatisticsAsync(cancellationToken);
        await SetStatisticsAsync(programStats, cancellationToken);
        await SetHierarchyStatisticsAsync(hierarchyStats, cancellationToken);
        _metrics.RecordWarm();
        _logger.LogInformation("Academic statistics cache warmed TenantId={TenantId}", _currentUser.TenantId);
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(Key("program-stats"));
        await _cache.RemoveAsync(Key("hierarchy-stats"));
        _metrics.RecordInvalidate();
        _logger.LogInformation("Academic statistics cache invalidated TenantId={TenantId}", _currentUser.TenantId);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await InvalidateAsync(cancellationToken);
        await WarmAsync(cancellationToken);
        _metrics.RecordRefresh();
        _logger.LogInformation("Academic statistics cache refreshed TenantId={TenantId}", _currentUser.TenantId);
    }

    public async Task<IReadOnlyList<ProgramStatisticsDto>?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var value = await _cache.GetAsync<IReadOnlyList<ProgramStatisticsDto>>(Key("program-stats"));
        sw.Stop();
        if (value is null) _metrics.RecordStatisticsMiss(sw.Elapsed);
        else
        {
            _metrics.RecordStatisticsHit(sw.Elapsed);
            _store.SetStatisticsCacheSize(value.Count);
        }
        return value;
    }

    public Task SetStatisticsAsync(IReadOnlyList<ProgramStatisticsDto> statistics, CancellationToken cancellationToken = default)
    {
        _store.SetStatisticsCacheSize(statistics.Count);
        return _cache.SetAsync(Key("program-stats"), statistics, DefaultTtl);
    }

    public async Task<AcademicHierarchyStatisticsDto?> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var value = await _cache.GetAsync<AcademicHierarchyStatisticsDto>(Key("hierarchy-stats"));
        sw.Stop();
        if (value is null) _metrics.RecordStatisticsMiss(sw.Elapsed);
        else _metrics.RecordStatisticsHit(sw.Elapsed);
        return value;
    }

    public Task SetHierarchyStatisticsAsync(AcademicHierarchyStatisticsDto statistics, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key("hierarchy-stats"), statistics, DefaultTtl);

    private string Key(string segment) => $"academic-statistics:{_currentUser.TenantId}:{segment}";
}
