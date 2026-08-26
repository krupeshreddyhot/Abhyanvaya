using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G.1 —
/// Read-only Section Semester remediation audit &amp; readiness (zero mutations).
/// </summary>
public interface ISectionSemesterRemediationAuditService
{
    Task<SectionSemesterRemediationAuditResultDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
