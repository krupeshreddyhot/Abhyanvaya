using Abhyanvaya.Domain.Entities.Scheduling;



namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;



public interface IFacultyAvailabilityRepository

{

    Task<IReadOnlyList<FacultyAvailability>> ListAsync(int tenantId, int? academicYearId, int? staffId, CancellationToken cancellationToken = default);

    Task<FacultyAvailability?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacultyAvailability>> GetOverlappingAsync(int tenantId, int staffId, int academicYearId, DateOnly startDate, DateOnly endDate, int? startSlotId, int? endSlotId, int? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(FacultyAvailability entity, CancellationToken cancellationToken = default);

}

