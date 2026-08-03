using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IFacultyWorkloadRepository
{
    Task<FacultyWorkload?> GetByStaffIdAsync(int tenantId, int staffId, CancellationToken cancellationToken = default);
    Task<FacultyWorkload?> GetByIdWithPreferencesAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddAsync(FacultyWorkload entity, CancellationToken cancellationToken = default);
    Task<FacultyDayPreference?> GetDayPreferenceByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddDayPreferenceAsync(FacultyDayPreference entity, CancellationToken cancellationToken = default);
    Task<FacultyTimeSlotPreference?> GetTimeSlotPreferenceByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddTimeSlotPreferenceAsync(FacultyTimeSlotPreference entity, CancellationToken cancellationToken = default);
}
