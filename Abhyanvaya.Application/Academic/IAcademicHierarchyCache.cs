using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.5 — Hierarchy cache over existing <see cref="Common.Interfaces.ICacheService"/>.</summary>
public interface IAcademicHierarchyCache
{
    Task<IReadOnlyList<Program>?> GetProgramsAsync(CancellationToken cancellationToken = default);
    Task SetProgramsAsync(IReadOnlyList<Program> programs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Course>?> GetCoursesAsync(CancellationToken cancellationToken = default);
    Task SetCoursesAsync(IReadOnlyList<Course> courses, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Group>?> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task SetGroupsAsync(IReadOnlyList<Group> groups, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Semester>?> GetSemestersAsync(CancellationToken cancellationToken = default);
    Task SetSemestersAsync(IReadOnlyList<Semester> semesters, CancellationToken cancellationToken = default);

    Task InvalidateHierarchyAsync(CancellationToken cancellationToken = default);
    Task WarmCacheAsync(CancellationToken cancellationToken = default);
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
}
