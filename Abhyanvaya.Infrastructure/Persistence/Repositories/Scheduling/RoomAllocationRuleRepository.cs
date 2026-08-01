using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class RoomAllocationRuleRepository : IRoomAllocationRuleRepository
{
    private readonly ApplicationDbContext _context;

    public RoomAllocationRuleRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<RoomAllocationRule>> ListAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RoomAllocationRule>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        return query.OrderByDescending(x => x.Priority).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<RoomAllocationRule>)t.Result, cancellationToken);
    }

    public Task<RoomAllocationRule?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<RoomAllocationRule>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddAsync(RoomAllocationRule entity, CancellationToken cancellationToken = default) =>
        await _context.Set<RoomAllocationRule>().AddAsync(entity, cancellationToken);
}
