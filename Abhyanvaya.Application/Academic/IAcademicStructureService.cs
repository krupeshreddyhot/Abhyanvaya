using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1A facade — delegates to <see cref="IAcademicCatalogService"/> and <see cref="IAcademicHierarchyService"/>.
/// Kept for backward-compatible controller injection.
/// </summary>
public interface IAcademicStructureService
{
    Task<TenantAcademicConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<TenantAcademicConfigurationDto> UpdateConfigurationAsync(
        UpdateTenantAcademicConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProgramDto?> GetProgramAsync(int id, CancellationToken cancellationToken = default);
    Task<ProgramDto> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default);
    Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramRequest request, CancellationToken cancellationToken = default);
    Task ArchiveProgramAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteProgramAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramDepartmentOptionDto>> GetProgramDepartmentOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<CourseProgramAssignmentOutcome> AssignCourseToProgramAsync(
        AssignCourseProgramRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Group>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Semester>> GetSemestersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<object>> GetSubjectsAsync(CancellationToken cancellationToken = default);

    Task<AcademicHierarchyDto> GetAcademicHierarchyAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default);

    Task<AcademicHierarchyStatisticsDto> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramStatisticsDto>> GetProgramStatisticsAsync(CancellationToken cancellationToken = default);
    Task<ProgramDto?> GetProgramSummaryAsync(int programId, CancellationToken cancellationToken = default);
    Task<int> GetProgramStudentCountAsync(int programId, CancellationToken cancellationToken = default);
    Task<int> GetProgramFacultyCountAsync(int programId, CancellationToken cancellationToken = default);
    Task<int> GetProgramCourseCountAsync(int programId, CancellationToken cancellationToken = default);

    // AI29.1A.5 dashboard-ready extensions
    Task<AcademicHierarchyDto> GetProgramHierarchyAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetProgramCoursesAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Group>> GetProgramGroupsAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Semester>> GetProgramSemestersAsync(int programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionDto>> GetProgramSectionsAsync(int programId, CancellationToken cancellationToken = default);
    Task<ProgramPolicyDto?> GetProgramPolicyAsync(int programId, CancellationToken cancellationToken = default);
    Task<ProgramPolicyDto> UpsertProgramPolicyAsync(int programId, UpsertProgramPolicyRequest request, CancellationToken cancellationToken = default);
}
