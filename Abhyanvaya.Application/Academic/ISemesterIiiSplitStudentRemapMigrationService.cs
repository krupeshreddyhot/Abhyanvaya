using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B —
/// Controlled Semester III split + Student.SemesterId remapping (approved scope only).
/// </summary>
public interface ISemesterIiiSplitStudentRemapMigrationService
{
    Task<SemesterIiiSplitMigrationResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
