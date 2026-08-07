using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.5 — Hierarchy: relationships, tree, statistics, navigation.</summary>
public interface IAcademicHierarchyService
{
    Task<AcademicHierarchyDto> GetAcademicHierarchyAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default);

    Task<AcademicHierarchyStatisticsDto> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramStatisticsDto>> GetProgramStatisticsAsync(CancellationToken cancellationToken = default);
    Task<ProgramStatisticsDto?> GetProgramStatisticsAsync(int programId, CancellationToken cancellationToken = default);

    Task<ProgramDto?> GetProgramSummaryAsync(int programId, CancellationToken cancellationToken = default);
    Task<AcademicHierarchyDto> GetProgramHierarchyAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetProgramCoursesAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Group>> GetProgramGroupsAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Semester>> GetProgramSemestersAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionDto>> GetProgramSectionsAsync(int programId, CancellationToken cancellationToken = default);

    Task<int> GetProgramStudentCountAsync(int programId, CancellationToken cancellationToken = default);
    Task<int> GetProgramFacultyCountAsync(int programId, CancellationToken cancellationToken = default);
    Task<int> GetProgramCourseCountAsync(int programId, CancellationToken cancellationToken = default);
}
