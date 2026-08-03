using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class SubjectDeliveryTypeRepository : ISubjectDeliveryTypeRepository
{
    private readonly ApplicationDbContext _context;

    public SubjectDeliveryTypeRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<SubjectDeliveryType>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SubjectDeliveryType>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<SubjectDeliveryType>)t.Result, cancellationToken);
    }

    public Task<SubjectDeliveryType?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<SubjectDeliveryType>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<SubjectDeliveryType?> GetByCodeAsync(int tenantId, string code, CancellationToken cancellationToken = default) =>
        _context.Set<SubjectDeliveryType>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, cancellationToken);

    public Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<SubjectDeliveryType>().AnyAsync(x =>
            x.TenantId == tenantId && x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddAsync(SubjectDeliveryType entity, CancellationToken cancellationToken = default) =>
        await _context.Set<SubjectDeliveryType>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<SubjectDeliveryType> entities, CancellationToken cancellationToken = default) =>
        await _context.Set<SubjectDeliveryType>().AddRangeAsync(entities, cancellationToken);
}
