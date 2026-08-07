using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29 — Section master, allocations, timetable mapping, dashboard preparation APIs.</summary>
public interface ISectionManagementService
{
    Task<IReadOnlyList<SectionDto>> GetSectionsAsync(
        int? academicYearId = null,
        int? courseId = null,
        int? groupId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);

    Task<SectionDto?> GetSectionAsync(int id, CancellationToken cancellationToken = default);
    Task<SectionDto> CreateSectionAsync(CreateSectionRequest request, CancellationToken cancellationToken = default);
    Task<SectionDto> UpdateSectionAsync(int id, UpdateSectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteSectionAsync(int id, CancellationToken cancellationToken = default);

    Task EnsureDefaultGeneralSectionAsync(
        int academicYearId,
        int courseId,
        int groupId,
        int semesterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentSectionDto>> GetStudentSectionsAsync(
        int? sectionId = null,
        int? studentId = null,
        bool currentOnly = true,
        CancellationToken cancellationToken = default);

    Task<StudentSectionDto> AssignStudentAsync(AssignStudentSectionRequest request, CancellationToken cancellationToken = default);
    Task<StudentSectionDto> TransferStudentAsync(TransferStudentSectionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacultySectionDto>> GetFacultySectionsAsync(
        int? sectionId = null,
        int? facultyId = null,
        bool currentOnly = true,
        CancellationToken cancellationToken = default);

    Task<FacultySectionDto> AssignFacultyAsync(AssignFacultySectionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimetableSectionDto>> GetTimetableSectionsAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableSectionDto>> SetTimetableSectionsAsync(
        int timetableId,
        SetTimetableSectionsRequest request,
        CancellationToken cancellationToken = default);

    Task<AutoAllocateSectionsResult> AutoAllocateAsync(AutoAllocateSectionsRequest request, CancellationToken cancellationToken = default);

    // AI29.16 — Dashboard preparation (consumed later by AI31.x)
    Task<IReadOnlyList<SectionDto>> GetSectionsForDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionStatisticsDto>> GetSectionStatisticsAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FacultySectionDto>> GetFacultyPerSectionAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentSectionDto>> GetStudentsPerSectionAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableSectionDto>> GetCombinedSessionsAsync(int? timetableId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SectionReportRowDto>> GetReportAsync(string kind, CancellationToken cancellationToken = default);
}
