using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

/// <summary>
/// Catalog Department lookups used by Scheduling. Does not own Department CRUD.
/// </summary>
public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly ApplicationDbContext _context;

    public DepartmentRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<Department>> ListAsync(int tenantId, int? collegeId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Department>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (collegeId.HasValue) query = query.Where(x => x.CollegeId == collegeId.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Department>)t.Result, cancellationToken);
    }

    public Task<Department?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Department>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task<bool> IsReferencedBySchedulingAsync(int tenantId, int departmentId, CancellationToken cancellationToken = default)
    {
        if (await _context.Set<SubjectAllocation>().AnyAsync(x => x.TenantId == tenantId && x.DepartmentId == departmentId, cancellationToken))
            return true;
        if (await _context.Set<Room>().AnyAsync(x => x.TenantId == tenantId && x.DepartmentId == departmentId, cancellationToken))
            return true;
        if (await _context.Set<Timetable>().AnyAsync(x => x.TenantId == tenantId && x.DepartmentId == departmentId, cancellationToken))
            return true;
        return await _context.Set<TimetableEntry>().AnyAsync(x => x.TenantId == tenantId && x.DepartmentId == departmentId, cancellationToken);
    }
}
