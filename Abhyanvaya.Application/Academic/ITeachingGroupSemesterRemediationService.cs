using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3F —
/// Controlled Teaching Group Semester remediation for the two audited residuals only.
/// </summary>
public interface ITeachingGroupSemesterRemediationService
{
    Task<TeachingGroupSemesterRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<TeachingGroupSemesterRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
