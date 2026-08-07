using System.Diagnostics;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.5/7 — Tenant-scoped hierarchy cache with observability.</summary>
public sealed class AcademicHierarchyCache : IAcademicHierarchyCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    private readonly ICacheService _cache;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicCacheMetricsService _metrics;
    private readonly AcademicMetricsStore _store;
    private readonly ILogger<AcademicHierarchyCache> _logger;

    public AcademicHierarchyCache(
        ICacheService cache,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicCacheMetricsService metrics,
        AcademicMetricsStore store,
        ILogger<AcademicHierarchyCache> logger)
    {
        _cache = cache;
        _db = db;
        _currentUser = currentUser;
        _metrics = metrics;
        _store = store;
        _logger = logger;
    }

    public Task<IReadOnlyList<Program>?> GetProgramsAsync(CancellationToken cancellationToken = default)
        => GetTrackedAsync<IReadOnlyList<Program>>("programs", cancellationToken);

    public Task SetProgramsAsync(IReadOnlyList<Program> programs, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key("programs"), programs, DefaultTtl);

    public Task<IReadOnlyList<Course>?> GetCoursesAsync(CancellationToken cancellationToken = default)
        => GetTrackedAsync<IReadOnlyList<Course>>("courses", cancellationToken);

    public Task SetCoursesAsync(IReadOnlyList<Course> courses, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key("courses"), courses, DefaultTtl);

    public Task<IReadOnlyList<Group>?> GetGroupsAsync(CancellationToken cancellationToken = default)
        => GetTrackedAsync<IReadOnlyList<Group>>("groups", cancellationToken);

    public Task SetGroupsAsync(IReadOnlyList<Group> groups, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key("groups"), groups, DefaultTtl);

    public Task<IReadOnlyList<Semester>?> GetSemestersAsync(CancellationToken cancellationToken = default)
        => GetTrackedAsync<IReadOnlyList<Semester>>("semesters", cancellationToken);

    public Task SetSemestersAsync(IReadOnlyList<Semester> semesters, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key("semesters"), semesters, DefaultTtl);

    public async Task InvalidateHierarchyAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(Key("programs"));
        await _cache.RemoveAsync(Key("courses"));
        await _cache.RemoveAsync(Key("groups"));
        await _cache.RemoveAsync(Key("semesters"));
        _metrics.RecordInvalidate();
        _logger.LogInformation("Academic hierarchy cache invalidated TenantId={TenantId}", _currentUser.TenantId);
    }

    public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var programs = await _db.Programs.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.ProgramName)
            .ToListAsync(cancellationToken);
        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);

        await SetProgramsAsync(programs, cancellationToken);
        await SetCoursesAsync(courses, cancellationToken);
        await SetGroupsAsync(groups, cancellationToken);
        await SetSemestersAsync(semesters, cancellationToken);
        _store.SetHierarchySize(programs.Count + courses.Count + groups.Count + semesters.Count);
        _metrics.RecordWarm();
        _logger.LogInformation(
            "Academic hierarchy cache warmed TenantId={TenantId} Programs={Programs} Courses={Courses}",
            tenantId, programs.Count, courses.Count);
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        await InvalidateHierarchyAsync(cancellationToken);
        await WarmCacheAsync(cancellationToken);
        _metrics.RecordRefresh();
        _logger.LogInformation("Academic hierarchy cache refreshed TenantId={TenantId}", _currentUser.TenantId);
    }

    private async Task<T?> GetTrackedAsync<T>(string segment, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var value = await _cache.GetAsync<T>(Key(segment));
        sw.Stop();
        if (value is null)
            _metrics.RecordHierarchyMiss(sw.Elapsed);
        else
            _metrics.RecordHierarchyHit(sw.Elapsed);
        return value;
    }

    private string Key(string segment) => $"academic-hierarchy:{_currentUser.TenantId}:{segment}";
}
