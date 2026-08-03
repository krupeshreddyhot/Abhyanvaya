using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;



public sealed class FacultyAvailabilityRepository : IFacultyAvailabilityRepository

{

    private readonly ApplicationDbContext _context;



    public FacultyAvailabilityRepository(ApplicationDbContext context) => _context = context;



    public Task<IReadOnlyList<FacultyAvailability>> ListAsync(int tenantId, int? academicYearId, int? staffId, CancellationToken cancellationToken = default)

    {

        var query = _context.Set<FacultyAvailability>().AsNoTracking().Where(x => x.TenantId == tenantId);

        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);

        if (staffId.HasValue) query = query.Where(x => x.StaffId == staffId.Value);

        return query.OrderBy(x => x.StartDate).ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<FacultyAvailability>)t.Result, cancellationToken);

    }



    public Task<FacultyAvailability?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<FacultyAvailability>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<IReadOnlyList<FacultyAvailability>> GetOverlappingAsync(int tenantId, int staffId, int academicYearId, DateOnly startDate, DateOnly endDate, int? startSlotId, int? endSlotId, int? excludeId, CancellationToken cancellationToken = default) =>

        _context.Set<FacultyAvailability>()

            .AsNoTracking()

            .Where(x => x.TenantId == tenantId

                && x.StaffId == staffId

                && x.AcademicYearId == academicYearId

                && x.StartDate <= endDate

                && x.EndDate >= startDate

                && (!excludeId.HasValue || x.Id != excludeId.Value))

            .ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<FacultyAvailability>)t.Result, cancellationToken);



    public async Task AddAsync(FacultyAvailability entity, CancellationToken cancellationToken = default) =>

        await _context.Set<FacultyAvailability>().AddAsync(entity, cancellationToken);

}

