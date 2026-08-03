using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IFacultyWorkloadService
{
    Task<FacultyWorkloadDto?> GetByStaffIdAsync(int staffId, CancellationToken cancellationToken = default);
    Task<FacultyWorkloadDto> UpsertAsync(UpsertFacultyWorkloadRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int staffId, CancellationToken cancellationToken = default);
    Task<FacultyDayPreferenceDto> UpsertDayPreferenceAsync(UpsertFacultyDayPreferenceRequest request, CancellationToken cancellationToken = default);
    Task DeleteDayPreferenceAsync(int id, CancellationToken cancellationToken = default);
    Task<FacultyTimeSlotPreferenceDto> UpsertTimeSlotPreferenceAsync(UpsertFacultyTimeSlotPreferenceRequest request, CancellationToken cancellationToken = default);
    Task DeleteTimeSlotPreferenceAsync(int id, CancellationToken cancellationToken = default);
}
