using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3L (package 3I1) —
/// Legacy Semester disposition journal + operational wildcard retirement verification.
/// Reuses Prompt 3D/3E/3H frameworks; does not mutate TG / TimetableSection / CAP.
/// </summary>
public interface ILegacySemesterWildcardRetirementService
{
    Task<LegacySemesterWildcardRetirementPreviewDto> PreviewAsync(CancellationToken cancellationToken = default);
    Task<LegacySemesterWildcardRetirementResultDto> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>P1-4 Prompt 3I package 3I3 — read-only readiness contract (no mutations).</summary>
    Task<LegacySemesterWildcardRetirementReadinessDto> BuildReadinessAsync(
        CancellationToken cancellationToken = default);
}
