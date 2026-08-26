using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.5 — Catalog: masters and Program CRUD (no hierarchy tree).</summary>
public interface IAcademicCatalogService
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
    Task<IReadOnlyList<SubjectCatalogItemDto>> GetSubjectsAsync(CancellationToken cancellationToken = default);

    Task<ProgramPolicyDto?> GetProgramPolicyAsync(int programId, CancellationToken cancellationToken = default);
    Task<ProgramPolicyDto> UpsertProgramPolicyAsync(
        int programId,
        UpsertProgramPolicyRequest request,
        CancellationToken cancellationToken = default);
}
