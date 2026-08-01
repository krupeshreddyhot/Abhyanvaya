using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IFacultyTeachingPreferenceRepository
{
    Task<IReadOnlyList<FacultyTeachingPreference>> ListAsync(int tenantId, int? academicYearId, int? staffId, bool? isActive, CancellationToken cancellationToken = default);
    Task<FacultyTeachingPreference?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> ActiveExistsAsync(int tenantId, int staffId, int academicYearId, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(FacultyTeachingPreference entity, CancellationToken cancellationToken = default);
}
