using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1A.5/7 — Backward-compatible facade over catalog + hierarchy (no duplicated business logic).
/// </summary>
public sealed class AcademicStructureService : IAcademicStructureService
{
    private readonly IAcademicCatalogService _catalog;
    private readonly IAcademicHierarchyService _hierarchy;
    private readonly IAcademicTelemetryService _telemetry;

    public AcademicStructureService(
        IAcademicCatalogService catalog,
        IAcademicHierarchyService hierarchy,
        IAcademicTelemetryService telemetry)
    {
        _catalog = catalog;
        _hierarchy = hierarchy;
        _telemetry = telemetry;
    }

    public Task<TenantAcademicConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
        => _catalog.GetConfigurationAsync(cancellationToken);

    public Task<TenantAcademicConfigurationDto> UpdateConfigurationAsync(
        UpdateTenantAcademicConfigurationRequest request,
        CancellationToken cancellationToken = default)
        => _catalog.UpdateConfigurationAsync(request, cancellationToken);

    public Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        => _catalog.GetProgramsAsync(includeInactive, cancellationToken);

    public Task<ProgramDto?> GetProgramAsync(int id, CancellationToken cancellationToken = default)
        => _catalog.GetProgramAsync(id, cancellationToken);

    public Task<ProgramDto> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default)
        => _catalog.CreateProgramAsync(request, cancellationToken);

    public Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramRequest request, CancellationToken cancellationToken = default)
        => _catalog.UpdateProgramAsync(id, request, cancellationToken);

    public Task ArchiveProgramAsync(int id, CancellationToken cancellationToken = default)
        => _catalog.ArchiveProgramAsync(id, cancellationToken);

    public Task DeleteProgramAsync(int id, CancellationToken cancellationToken = default)
        => _catalog.DeleteProgramAsync(id, cancellationToken);

    public Task<IReadOnlyList<ProgramDepartmentOptionDto>> GetProgramDepartmentOptionsAsync(
        CancellationToken cancellationToken = default)
        => _catalog.GetProgramDepartmentOptionsAsync(cancellationToken);

    public Task<CourseProgramAssignmentOutcome> AssignCourseToProgramAsync(
        AssignCourseProgramRequest request,
        CancellationToken cancellationToken = default)
        => _catalog.AssignCourseToProgramAsync(request, cancellationToken);

    public Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default)
        => _catalog.GetCoursesAsync(cancellationToken);

    public Task<IReadOnlyList<Group>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => _catalog.GetGroupsAsync(cancellationToken);

    public Task<IReadOnlyList<Semester>> GetSemestersAsync(CancellationToken cancellationToken = default)
        => _catalog.GetSemestersAsync(cancellationToken);

    public Task<IReadOnlyList<SectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default)
        => _catalog.GetSectionsAsync(cancellationToken);

    public async Task<IReadOnlyList<object>> GetSubjectsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _catalog.GetSubjectsAsync(cancellationToken);
        return rows.Cast<object>().ToList();
    }

    public Task<AcademicHierarchyDto> GetAcademicHierarchyAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.StructureApi,
            "AcademicStructure.Api",
            ct => _hierarchy.GetAcademicHierarchyAsync(includeInactive, includeSections, includeSubjects, ct),
            cancellationToken);

    public Task<AcademicHierarchyStatisticsDto> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default)
        => _hierarchy.GetHierarchyStatisticsAsync(cancellationToken);

    public Task<IReadOnlyList<ProgramStatisticsDto>> GetProgramStatisticsAsync(CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramStatisticsAsync(cancellationToken);

    public Task<ProgramDto?> GetProgramSummaryAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramSummaryAsync(programId, cancellationToken);

    public Task<int> GetProgramStudentCountAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramStudentCountAsync(programId, cancellationToken);

    public Task<int> GetProgramFacultyCountAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramFacultyCountAsync(programId, cancellationToken);

    public Task<int> GetProgramCourseCountAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramCourseCountAsync(programId, cancellationToken);

    public Task<AcademicHierarchyDto> GetProgramHierarchyAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramHierarchyAsync(programId, cancellationToken);

    public Task<IReadOnlyList<Course>> GetProgramCoursesAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramCoursesAsync(programId, cancellationToken);

    public Task<IReadOnlyList<Group>> GetProgramGroupsAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramGroupsAsync(programId, cancellationToken);

    public Task<IReadOnlyList<Semester>> GetProgramSemestersAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramSemestersAsync(programId, cancellationToken);

    public Task<IReadOnlyList<SectionDto>> GetProgramSectionsAsync(int programId, CancellationToken cancellationToken = default)
        => _hierarchy.GetProgramSectionsAsync(programId, cancellationToken);

    public Task<ProgramPolicyDto?> GetProgramPolicyAsync(int programId, CancellationToken cancellationToken = default)
        => _catalog.GetProgramPolicyAsync(programId, cancellationToken);

    public Task<ProgramPolicyDto> UpsertProgramPolicyAsync(
        int programId,
        UpsertProgramPolicyRequest request,
        CancellationToken cancellationToken = default)
        => _catalog.UpsertProgramPolicyAsync(programId, request, cancellationToken);
}
