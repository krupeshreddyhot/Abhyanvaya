using Abhyanvaya.Application.DTOs.Scheduling;



namespace Abhyanvaya.Application.Scheduling;



public interface IFacultyAvailabilityService

{

    Task<IReadOnlyList<FacultyAvailabilityDto>> ListAsync(int? academicYearId, int? staffId, CancellationToken cancellationToken = default);

    Task<FacultyAvailabilityDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<FacultyAvailabilityDto> CreateAsync(CreateFacultyAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<FacultyAvailabilityDto> UpdateAsync(UpdateFacultyAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

}

