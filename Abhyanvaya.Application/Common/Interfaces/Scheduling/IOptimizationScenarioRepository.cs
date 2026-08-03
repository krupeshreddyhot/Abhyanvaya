using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IOptimizationScenarioRepository
{
    Task<OptimizationScenario?> GetByScenarioIdAsync(int tenantId, Guid scenarioId, bool includeSnapshots = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationScenario>> ListAsync(int tenantId, int? academicYearId, int? departmentId, int? ownerUserId, bool? favoritesOnly, CancellationToken cancellationToken = default);
    Task<OptimizationScenario> AddAsync(OptimizationScenario scenario, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(OptimizationScenario scenario, CancellationToken cancellationToken = default);
    Task AddHistoryAsync(OptimizationScenarioHistory history, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationScenarioHistory>> ListHistoryAsync(int scenarioPk, CancellationToken cancellationToken = default);
}
