using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;



public sealed class SubjectAllocationRepository : ISubjectAllocationRepository

{

    private readonly ApplicationDbContext _context;



    public SubjectAllocationRepository(ApplicationDbContext context) => _context = context;



    public Task<IReadOnlyList<SubjectAllocation>> ListAsync(int tenantId, int? academicYearId, int? staffId, int? departmentId, CancellationToken cancellationToken = default)

    {

        var query = _context.Set<SubjectAllocation>().AsNoTracking().Where(x => x.TenantId == tenantId);

        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);

        if (staffId.HasValue) query = query.Where(x => x.StaffId == staffId.Value);

        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId.Value);

        return query.OrderBy(x => x.SubjectId).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<SubjectAllocation>)t.Result, cancellationToken);

    }



    public Task<SubjectAllocation?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectAllocation>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<bool> DuplicateExistsAsync(int tenantId, int academicYearId, int subjectId, int courseId, int groupId, int semesterId, int departmentId, int? excludeId, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectAllocation>().AnyAsync(x =>

            x.TenantId == tenantId

            && x.AcademicYearId == academicYearId

            && x.SubjectId == subjectId

            && x.CourseId == courseId

            && x.GroupId == groupId

            && x.SemesterId == semesterId

            && x.DepartmentId == departmentId

            && (!excludeId.HasValue || x.Id != excludeId.Value),

            cancellationToken);



    public Task<decimal> SumWeeklyHoursForStaffAsync(int tenantId, int staffId, int? excludeId, CancellationToken cancellationToken = default) =>

        _context.Set<SubjectAllocation>()

            .Where(x => x.TenantId == tenantId && x.StaffId == staffId && (!excludeId.HasValue || x.Id != excludeId.Value))

            .SumAsync(x => x.WeeklyHours, cancellationToken);



    public async Task AddAsync(SubjectAllocation entity, CancellationToken cancellationToken = default) =>

        await _context.Set<SubjectAllocation>().AddAsync(entity, cancellationToken);

}

