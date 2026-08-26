using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6 — Reusable academic breadcrumbs.</summary>
public interface IAcademicBreadcrumbService
{
    Task<AcademicBreadcrumb> BuildBreadcrumbAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildProgramBreadcrumbAsync(int programId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildCourseBreadcrumbAsync(int courseId, CancellationToken cancellationToken = default);
    Task<AcademicBreadcrumb> BuildSectionBreadcrumbAsync(int sectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AI29.1D Prompt 16 / 16A — operational context trail (Program? → Course → Group → Semester → Section? → Subject?).
    /// When Programs are disabled the tree omits Program, so the trail starts at Course.
    /// Prompt 16A validates ID relationships against <see cref="IAcademicTreeService"/> before compose.
    /// </summary>
    Task<AcademicOperationalBreadcrumbOutcome> BuildOperationalContextBreadcrumbAsync(
        AcademicOperationalContext context,
        CancellationToken cancellationToken = default);
}
