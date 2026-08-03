using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class FacultyTeachingPreferenceRepository : IFacultyTeachingPreferenceRepository
{
    private readonly ApplicationDbContext _context;

    public FacultyTeachingPreferenceRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<FacultyTeachingPreference>> ListAsync(int tenantId, int? academicYearId, int? staffId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<FacultyTeachingPreference>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        if (staffId.HasValue) query = query.Where(x => x.StaffId == staffId.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return query.OrderBy(x => x.Priority).ThenBy(x => x.StaffId).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<FacultyTeachingPreference>)t.Result, cancellationToken);
    }

    public Task<FacultyTeachingPreference?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyTeachingPreference>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<bool> ActiveExistsAsync(int tenantId, int staffId, int academicYearId, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyTeachingPreference>().AnyAsync(x =>
            x.TenantId == tenantId
            && x.StaffId == staffId
            && x.AcademicYearId == academicYearId
            && x.IsActive
            && !x.IsDeleted
            && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddAsync(FacultyTeachingPreference entity, CancellationToken cancellationToken = default) =>
        await _context.Set<FacultyTeachingPreference>().AddAsync(entity, cancellationToken);
}
