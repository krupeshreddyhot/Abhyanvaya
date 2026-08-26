using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G —
/// Controlled Section.SemesterId remediation (legacy Sem 3 → Sem 11) to unblock Prompt 3F.
/// </summary>
public interface ISectionSemesterRemediationService
{
    Task<SectionSemesterRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<SectionSemesterRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
