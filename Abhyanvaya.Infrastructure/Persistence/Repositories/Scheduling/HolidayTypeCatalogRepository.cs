using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class HolidayTypeCatalogRepository : IHolidayTypeCatalogRepository
{
    private readonly ApplicationDbContext _context;

    public HolidayTypeCatalogRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<HolidayTypeCatalog>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<HolidayTypeCatalog>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<HolidayTypeCatalog>)t.Result, cancellationToken);
    }

    public Task<HolidayTypeCatalog?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<HolidayTypeCatalog>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<HolidayTypeCatalog>().AnyAsync(x =>
            x.TenantId == tenantId && x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddAsync(HolidayTypeCatalog entity, CancellationToken cancellationToken = default) =>
        await _context.Set<HolidayTypeCatalog>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<HolidayTypeCatalog> entities, CancellationToken cancellationToken = default) =>
        await _context.Set<HolidayTypeCatalog>().AddRangeAsync(entities, cancellationToken);
}
