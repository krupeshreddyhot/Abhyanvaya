using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1B.7 — Allocation context cache (separate from hierarchy/statistics caches).</summary>
public sealed class AllocationContextCache : IAllocationContextCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IAcademicCacheMetricsService _metrics;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly AcademicMetricsStore _store;

    public AllocationContextCache(
        ICacheService cache,
        ICurrentUserService currentUser,
        ISectionAllocationContextBuilder builder,
        IAcademicCacheMetricsService metrics,
        IAcademicTelemetryService telemetry,
        AcademicMetricsStore store)
    {
        _cache = cache;
        _currentUser = currentUser;
        _builder = builder;
        _metrics = metrics;
        _telemetry = telemetry;
        _store = store;
    }

    public async Task WarmAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
    {
        var ctx = await _builder.BuildAsync(scope, cancellationToken);
        await SetAsync(scope, ctx, cancellationToken);
        _metrics.RecordWarm();
        _store.Increment(AcademicOperations.AllocationCacheWarm);
    }

    public Task SetAsync(AllocationScopeRequest scope, SectionAllocationContext context, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key(scope), context, Ttl);

    public async Task RefreshAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
    {
        await InvalidateAsync(scope, cancellationToken);
        await WarmAsync(scope, cancellationToken);
        _metrics.RecordRefresh();
        _store.Increment(AcademicOperations.AllocationCacheRefresh);
    }

    public async Task InvalidateAsync(AllocationScopeRequest? scope = null, CancellationToken cancellationToken = default)
    {
        if (scope is not null)
            await _cache.RemoveAsync(Key(scope));
        _metrics.RecordInvalidate();
    }

    public async Task<SectionAllocationContext?> GetAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
    {
        var hit = await _cache.GetAsync<SectionAllocationContext>(Key(scope));
        if (hit is not null)
        {
            _telemetry.RecordCacheHit("allocation");
            _store.Increment(AcademicOperations.AllocationCacheHit);
            return hit;
        }
        _telemetry.RecordCacheMiss("allocation");
        _store.Increment(AcademicOperations.AllocationCacheMiss);
        return null;
    }

    public async Task<bool> ExistsAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
        => await GetAsync(scope, cancellationToken) is not null;

    private string Key(AllocationScopeRequest scope)
        => $"allocation-context:{_currentUser.TenantId}:{scope.AcademicYearId}:{scope.CourseId}:{scope.GroupId}:{scope.SemesterId}";
}
