using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A (PromptCode P1-4-3JA) —
/// Controlled historical archive / disposition for legacy Semesters. No Group guessing.
/// </summary>
public interface ILegacySemesterHistoricalDispositionService
{
    Task<LegacySemesterHistoricalDispositionPreviewDto> PreviewAsync(
        CancellationToken cancellationToken = default);

    Task<LegacySemesterHistoricalDispositionResultDto> ExecuteAsync(
        LegacySemesterHistoricalDispositionExecuteRequest request,
        CancellationToken cancellationToken = default);
}
