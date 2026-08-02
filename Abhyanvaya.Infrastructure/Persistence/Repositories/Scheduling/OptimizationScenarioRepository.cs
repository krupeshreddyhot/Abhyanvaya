using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class OptimizationScenarioRepository : IOptimizationScenarioRepository
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _uow;

    public OptimizationScenarioRepository(IApplicationDbContext db, IUnitOfWork uow)
    {
        _db = db;
        _uow = uow;
    }

    public async Task<OptimizationScenario?> GetByScenarioIdAsync(
        int tenantId,
        Guid scenarioId,
        bool includeSnapshots = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SchedulingOptimizationScenarios.Where(s => s.TenantId == tenantId && s.ScenarioId == scenarioId && !s.IsDeleted);
        var scenario = await query.FirstOrDefaultAsync(cancellationToken);
        if (scenario is null) return null;

        if (includeSnapshots)
        {
            scenario.Snapshots = await _db.SchedulingOptimizationSnapshots
                .Where(x => x.OptimizationScenarioId == scenario.Id && !x.IsDeleted)
                .OrderBy(x => x.Sequence)
                .ToListAsync(cancellationToken);
        }

        return scenario;
    }

    public async Task<IReadOnlyList<OptimizationScenario>> ListAsync(
        int tenantId,
        int? academicYearId,
        int? departmentId,
        int? ownerUserId,
        bool? favoritesOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SchedulingOptimizationScenarios.Where(s => s.TenantId == tenantId && !s.IsDeleted);
        if (academicYearId.HasValue) query = query.Where(s => s.AcademicYearId == academicYearId.Value);
        if (departmentId.HasValue) query = query.Where(s => s.DepartmentId == departmentId.Value);
        if (ownerUserId.HasValue) query = query.Where(s => s.OwnerUserId == ownerUserId.Value);
        if (favoritesOnly == true) query = query.Where(s => s.IsFavorite);

        return await query.OrderByDescending(s => s.IsPinned).ThenByDescending(s => s.CreatedDate).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<OptimizationScenario> AddAsync(OptimizationScenario scenario, CancellationToken cancellationToken = default)
    {
        await _db.AddAsync(scenario);
        await _uow.SaveChangesAsync(cancellationToken);
        return scenario;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _uow.SaveChangesAsync(cancellationToken);

    public async Task SoftDeleteAsync(OptimizationScenario scenario, CancellationToken cancellationToken = default)
    {
        scenario.IsDeleted = true;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task AddHistoryAsync(OptimizationScenarioHistory history, CancellationToken cancellationToken = default)
    {
        await _db.AddAsync(history);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OptimizationScenarioHistory>> ListHistoryAsync(int scenarioPk, CancellationToken cancellationToken = default) =>
        await _db.SchedulingOptimizationScenarioHistories
            .Where(h => h.OptimizationScenarioId == scenarioPk && !h.IsDeleted)
            .OrderByDescending(h => h.OccurredUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
