using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IConflictDetectionService
{
    Task<ConflictAnalysisReportDto> AnalyzeAsync(RunConflictDetectionRequest request, CancellationToken cancellationToken = default);
    Task<ConflictWorkspaceDto> GetWorkspaceAsync(ConflictWorkspaceQuery query, CancellationToken cancellationToken = default);
    Task<ConflictDashboardDto> GetDashboardAsync(int? academicYearId, int? timetableId, CancellationToken cancellationToken = default);
    Task<HeatMapDto> GetFacultyHeatMapAsync(int academicYearId, int? staffId, int? timetableId, CancellationToken cancellationToken = default);
    Task<HeatMapDto> GetRoomHeatMapAsync(int academicYearId, int? roomId, int? timetableId, CancellationToken cancellationToken = default);
    Task<HeatMapDto> GetDepartmentHeatMapAsync(int academicYearId, int? departmentId, int? timetableId, CancellationToken cancellationToken = default);
}

public interface IAttendanceSessionResolver
{
    Task<AttendanceSessionResolutionDto> ResolveAsync(int? staffId, DateOnly? date, CancellationToken cancellationToken = default);
}
