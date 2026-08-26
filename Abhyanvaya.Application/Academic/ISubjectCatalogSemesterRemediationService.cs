using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J —
/// Controlled Subject Catalog Semester remediation (legacy NULL-group → Group-specific).
/// </summary>
public interface ISubjectCatalogSemesterRemediationService
{
    Task<SubjectCatalogRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<SubjectCatalogRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default);
}
