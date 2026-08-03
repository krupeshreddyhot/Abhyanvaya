using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface ISubjectAllocationRepository
{
    Task<IReadOnlyList<SubjectAllocation>> ListAsync(int tenantId, int? academicYearId, int? staffId, int? departmentId, CancellationToken cancellationToken = default);
    Task<SubjectAllocation?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> DuplicateExistsAsync(int tenantId, int academicYearId, int subjectId, int courseId, int groupId, int semesterId, int departmentId, int? excludeId, CancellationToken cancellationToken = default);
    Task<decimal> SumWeeklyHoursForStaffAsync(int tenantId, int staffId, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(SubjectAllocation entity, CancellationToken cancellationToken = default);
}
