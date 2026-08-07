using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6 — Hierarchy search returning paths (no duplicated queries).</summary>
public interface IAcademicSearchService
{
    Task<AcademicSearchResult?> FindNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicSearchResult>> FindCourseAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicSearchResult>> FindSemesterAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicSearchResult>> FindSectionAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicSearchResult>> FindSubjectAsync(string query, CancellationToken cancellationToken = default);
}
