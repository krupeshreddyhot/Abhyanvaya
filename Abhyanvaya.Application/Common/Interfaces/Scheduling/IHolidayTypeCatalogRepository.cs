using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IHolidayTypeCatalogRepository
{
    Task<IReadOnlyList<HolidayTypeCatalog>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<HolidayTypeCatalog?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(HolidayTypeCatalog entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<HolidayTypeCatalog> entities, CancellationToken cancellationToken = default);
}
