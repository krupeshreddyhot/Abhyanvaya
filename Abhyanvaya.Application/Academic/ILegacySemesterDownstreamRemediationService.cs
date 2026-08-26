using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C —
/// Controlled remediation of downstream refs to legacy Semester III.
/// TeachingGroup = identify-only (never mutate).
/// </summary>
public interface ILegacySemesterDownstreamRemediationService
{
    Task<DownstreamRemediationReportDto> AuditAsync(CancellationToken cancellationToken = default);
    Task<DownstreamRemediationReportDto> PreviewAsync(CancellationToken cancellationToken = default);
    Task<DownstreamRemediationReportDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
