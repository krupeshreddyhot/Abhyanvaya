using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IAcademicCalendarRepository
{
    Task<IReadOnlyList<AcademicYear>> ListYearsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<AcademicYear?> GetYearByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<AcademicYear?> GetYearWithDetailsAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<AcademicYear?> GetCurrentYearAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<bool> YearCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddYearAsync(AcademicYear entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicTerm>> ListTermsAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default);
    Task<AcademicTerm?> GetTermByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddTermAsync(AcademicTerm entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkingDay>> ListWorkingDaysAsync(int tenantId, int academicYearId, CancellationToken cancellationToken = default);
    Task<WorkingDay?> GetWorkingDayByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddWorkingDayAsync(WorkingDay entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Holiday>> ListHolidaysAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default);
    Task<Holiday?> GetHolidayByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddHolidayAsync(Holiday entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
}
