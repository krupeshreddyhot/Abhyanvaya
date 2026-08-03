using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>CQRS-style academic calendar service (commands + queries).</summary>
public interface IAcademicCalendarService
{
    // Queries
    Task<IReadOnlyList<AcademicYearDto>> ListYearsAsync(CancellationToken cancellationToken = default);
    Task<AcademicYearDto?> GetYearByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicTermDto>> ListTermsAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<AcademicTermDto?> GetTermByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkingDayDto>> ListWorkingDaysAsync(int academicYearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<HolidayDto?> GetHolidayByIdAsync(int id, CancellationToken cancellationToken = default);

    // Commands
    Task<AcademicYearDto> CreateYearAsync(CreateAcademicYearRequest request, CancellationToken cancellationToken = default);
    Task<AcademicYearDto> UpdateYearAsync(UpdateAcademicYearRequest request, CancellationToken cancellationToken = default);
    Task DeleteYearAsync(int id, CancellationToken cancellationToken = default);
    Task SetCurrentYearAsync(int id, CancellationToken cancellationToken = default);
    Task<AcademicYearDto> ClonePreviousYearAsync(ClonePreviousYearRequest request, CancellationToken cancellationToken = default);
    Task<AcademicTermDto> CreateTermAsync(CreateAcademicTermRequest request, CancellationToken cancellationToken = default);
    Task<AcademicTermDto> UpdateTermAsync(UpdateAcademicTermRequest request, CancellationToken cancellationToken = default);
    Task DeleteTermAsync(int id, CancellationToken cancellationToken = default);
    Task<WorkingDayDto> UpsertWorkingDayAsync(UpsertWorkingDayRequest request, CancellationToken cancellationToken = default);
    Task DeleteWorkingDayAsync(int id, CancellationToken cancellationToken = default);
    Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default);
    Task<HolidayDto> UpdateHolidayAsync(UpdateHolidayRequest request, CancellationToken cancellationToken = default);
    Task DeleteHolidayAsync(int id, CancellationToken cancellationToken = default);
}
