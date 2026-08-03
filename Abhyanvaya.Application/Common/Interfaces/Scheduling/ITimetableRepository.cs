using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface ITimetableRepository
{
    Task<IReadOnlyList<Timetable>> ListAsync(int tenantId, int? academicYearId, TimetableStatus? status, int? departmentId, bool includeArchived, CancellationToken cancellationToken = default);
    Task<Timetable?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<Timetable?> GetByIdWithEntriesAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(int tenantId, int academicYearId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(Timetable entity, CancellationToken cancellationToken = default);
    Task<int> CountEntriesAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimetableEntry>> ListEntriesAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableEntry>> ListEntriesByStaffAsync(int tenantId, int timetableId, int staffId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableEntry>> ListEntriesByRoomAsync(int tenantId, int timetableId, int roomId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableEntry>> ListEntriesByStudentAsync(int tenantId, int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableEntry>> ListEntriesByDepartmentAsync(int tenantId, int timetableId, int departmentId, CancellationToken cancellationToken = default);
    Task<TimetableEntry?> GetEntryByIdAsync(int tenantId, int entryId, CancellationToken cancellationToken = default);
    Task AddEntryAsync(TimetableEntry entity, CancellationToken cancellationToken = default);
    Task AddEntriesAsync(IEnumerable<TimetableEntry> entities, CancellationToken cancellationToken = default);
}
