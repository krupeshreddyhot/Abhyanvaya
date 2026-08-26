using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (package 3I2) / PromptCode P1-4-3N —
/// Final legacy Semester disposition + schema hardening readiness gate (read-only).
/// </summary>
public interface ILegacySemesterFinalDispositionReadinessService
{
    Task<LegacySemesterFinalDispositionReadinessResultDto> BuildAsync(CancellationToken cancellationToken = default);
}
