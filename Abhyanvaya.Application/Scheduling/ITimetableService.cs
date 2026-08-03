using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ITimetableService
{
    Task<IReadOnlyList<TimetableDto>> ListTimetablesAsync(int? academicYearId, TimetableStatus? status, int? departmentId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<TimetableDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TimetableGridDto?> GetGridAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<TimetableProjectionDto?> GetFacultyProjectionAsync(int timetableId, int staffId, CancellationToken cancellationToken = default);
    Task<TimetableProjectionDto?> GetStudentProjectionAsync(int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default);
    Task<TimetableProjectionDto?> GetRoomProjectionAsync(int timetableId, int roomId, CancellationToken cancellationToken = default);
    Task<TimetableProjectionDto?> GetDepartmentProjectionAsync(int timetableId, int departmentId, CancellationToken cancellationToken = default);
    Task<TimetableDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default);

    Task<TimetableDto> CreateTimetableAsync(CreateTimetableRequest request, CancellationToken cancellationToken = default);
    Task<TimetableDto> UpdateTimetableAsync(UpdateTimetableRequest request, CancellationToken cancellationToken = default);
    Task DeleteTimetableAsync(int id, CancellationToken cancellationToken = default);
    Task<TimetableDto> LockAsync(int id, CancellationToken cancellationToken = default);
    Task<TimetableDto> UnlockAsync(int id, CancellationToken cancellationToken = default);

    Task<TimetableEntryDto> CreateEntryAsync(int timetableId, CreateTimetableEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimetableEntryDto> UpdateEntryAsync(int entryId, UpdateTimetableEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(int entryId, CancellationToken cancellationToken = default);
    Task<TimetableEntryDto> MoveEntryAsync(int entryId, MoveTimetableEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimetableEntryDto> CopyEntryAsync(int entryId, CopyTimetableEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimetableEntryDto> DuplicateEntryAsync(int entryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableEntryDto>> BulkUpsertEntriesAsync(int timetableId, BulkPasteEntriesRequest request, CancellationToken cancellationToken = default);
}

public interface ITimetableExportService
{
    Task<byte[]> ExportFacultyExcelAsync(int timetableId, int staffId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportStudentExcelAsync(int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportRoomExcelAsync(int timetableId, int roomId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportDepartmentExcelAsync(int timetableId, int departmentId, CancellationToken cancellationToken = default);
}
