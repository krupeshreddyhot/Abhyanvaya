using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (TG readiness) / PromptCode P1-4-3H2 —
/// Read-only post-Section audit for Prompt 3F Teaching Group remediation eligibility.
/// </summary>
public interface ITeachingGroupRemediationReadinessService
{
    Task<TeachingGroupRemediationReadinessResultDto> BuildAsync(CancellationToken cancellationToken = default);
}
