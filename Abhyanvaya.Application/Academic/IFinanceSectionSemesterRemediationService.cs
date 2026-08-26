using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I —
/// Controlled Finance Section.SemesterId remediation (legacy Sem 3 → Finance Sem 10).
/// </summary>
public interface IFinanceSectionSemesterRemediationService
{
    Task<FinanceSectionRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<FinanceSectionRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
