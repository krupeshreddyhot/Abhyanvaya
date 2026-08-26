using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
/// Controlled legacy Semester disposition finalization (Teaching Groups out of scope).
/// </summary>
public interface ILegacySemesterFinalizationExecutionService
{
    Task<LegacySemesterFinalizationExecutionResultDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<LegacySemesterFinalizationExecutionResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
