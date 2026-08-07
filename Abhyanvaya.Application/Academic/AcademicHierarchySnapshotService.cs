using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic;

public sealed class AcademicHierarchySnapshotService : IAcademicHierarchySnapshotService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicCatalogService _catalog;
    private readonly AcademicHierarchyOptions _options;
    private readonly AcademicPlatformOptions _platformOptions;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly ILogger<AcademicHierarchySnapshotService> _logger;

    public AcademicHierarchySnapshotService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTreeService tree,
        IAcademicCatalogService catalog,
        IOptions<AcademicHierarchyOptions> options,
        IOptions<AcademicPlatformOptions> platformOptions,
        IAcademicTelemetryService telemetry,
        ILogger<AcademicHierarchySnapshotService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _tree = tree;
        _catalog = catalog;
        _options = options.Value;
        _platformOptions = platformOptions.Value;
        _telemetry = telemetry;
        _logger = logger;
    }

    public bool IsEnabled => _options.EnableDailySnapshots || _platformOptions.EnableSnapshots;

    public async Task<AcademicHierarchySnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return null;
        return await _db.AcademicHierarchySnapshots.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AcademicHierarchySnapshot?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return null;
        return await _db.AcademicHierarchySnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SnapshotDate == date, cancellationToken);
    }

    public Task<AcademicHierarchySnapshot?> GenerateTodayAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return Task.FromResult<AcademicHierarchySnapshot?>(null);

        return _telemetry.TrackAsync(
            AcademicOperations.Snapshot,
            "AcademicHierarchy.Snapshot",
            GenerateTodayCoreAsync,
            cancellationToken);
    }

    private async Task<AcademicHierarchySnapshot?> GenerateTodayCoreAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await _db.AcademicHierarchySnapshots
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SnapshotDate == today, cancellationToken);

        var model = await _tree.BuildTreeAsync(includeInactive: true, cancellationToken: cancellationToken);
        _ = await _catalog.GetConfigurationAsync(cancellationToken);

        var programs = await _db.Programs.CountAsync(p => p.TenantId == _currentUser.TenantId, cancellationToken);
        var courses = await _db.Courses.CountAsync(c => c.TenantId == _currentUser.TenantId, cancellationToken);
        var groups = await _db.Groups.CountAsync(g => g.TenantId == _currentUser.TenantId, cancellationToken);
        var semesters = await _db.Semesters.CountAsync(s => s.TenantId == _currentUser.TenantId, cancellationToken);
        var sections = await _db.Sections.CountAsync(s => s.TenantId == _currentUser.TenantId, cancellationToken);
        var subjects = await _db.Subjects.CountAsync(s => s.TenantId == _currentUser.TenantId, cancellationToken);

        var json = JsonSerializer.Serialize(new
        {
            model.EnablePrograms,
            model.TotalNodes,
            model.GeneratedUtc,
            Roots = model.Roots,
        });

        if (existing is null)
        {
            existing = new AcademicHierarchySnapshot
            {
                TenantId = _currentUser.TenantId,
                SnapshotDate = today,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            };
            await _db.AddAsync(existing);
        }

        existing.Programs = programs;
        existing.Courses = courses;
        existing.Groups = groups;
        existing.Semesters = semesters;
        existing.Sections = sections;
        existing.Subjects = subjects;
        existing.HierarchyJson = json;
        existing.GeneratedDate = DateTime.UtcNow;
        existing.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Academic hierarchy snapshot generated SnapshotDate={SnapshotDate} Nodes={Nodes}",
            today, model.TotalNodes);
        return existing;
    }
}
