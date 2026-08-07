using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6 — Reusable academic breadcrumbs.</summary>
public interface IAcademicBreadcrumbService
{
    Task<AcademicBreadcrumb> BuildBreadcrumbAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildProgramBreadcrumbAsync(int programId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildCourseBreadcrumbAsync(int courseId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildSectionBreadcrumbAsync(int sectionId, CancellationToken cancellationToken = default);
}
