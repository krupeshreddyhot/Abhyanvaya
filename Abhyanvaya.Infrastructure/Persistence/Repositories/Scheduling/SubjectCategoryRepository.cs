using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;



public sealed class SubjectCategoryRepository : ISubjectCategoryRepository

{

    private readonly ApplicationDbContext _context;



    public SubjectCategoryRepository(ApplicationDbContext context) => _context = context;



    public Task<IReadOnlyList<SubjectCategory>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default)

    {

        var query = _context.Set<SubjectCategory>().AsNoTracking().Where(x => x.TenantId == tenantId);

        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);

        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<SubjectCategory>)t.Result, cancellationToken);

    }



    public Task<SubjectCategory?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectCategory>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<SubjectCategory?> GetByCodeAsync(int tenantId, string code, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectCategory>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);



    public Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectCategory>().AnyAsync(x =>

            x.TenantId == tenantId

            && x.Code == code

            && (!excludeId.HasValue || x.Id != excludeId.Value),

            cancellationToken);



    public async Task AddAsync(SubjectCategory entity, CancellationToken cancellationToken = default) =>

        await _context.Set<SubjectCategory>().AddAsync(entity, cancellationToken);



    public async Task AddRangeAsync(IEnumerable<SubjectCategory> entities, CancellationToken cancellationToken = default) =>

        await _context.Set<SubjectCategory>().AddRangeAsync(entities, cancellationToken);

}

