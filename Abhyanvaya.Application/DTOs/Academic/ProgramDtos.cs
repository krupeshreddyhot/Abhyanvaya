namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class ProgramDto
{
    public int Id { get; init; }
    public int CollegeId { get; init; }
    public string ProgramCode { get; init; } = "";
    public string ProgramName { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public string Status { get; init; } = "Active";
    public string? Icon { get; init; }
    public string? ThemeColor { get; init; }
    public int? AcademicCalendarId { get; init; }
    public int CourseCount { get; init; }
    public int StudentCount { get; init; }
    public int FacultyCount { get; init; }
}

public sealed class CreateProgramRequest
{
    public string ProgramCode { get; init; } = "";
    public string ProgramName { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Icon { get; init; }
    public string? ThemeColor { get; init; }
    public int? AcademicCalendarId { get; init; }
}

public sealed class UpdateProgramRequest
{
    public string ProgramCode { get; init; } = "";
    public string ProgramName { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
    /// <summary>Active | Inactive | Archived</summary>
    public string Status { get; init; } = "Active";
    public string? Icon { get; init; }
    public string? ThemeColor { get; init; }
    public int? AcademicCalendarId { get; init; }
}

public sealed class TenantAcademicConfigurationDto
{
    public int Id { get; init; }
    public int CollegeId { get; init; }
    public bool EnablePrograms { get; init; }
}

public sealed class UpdateTenantAcademicConfigurationRequest
{
    public bool EnablePrograms { get; init; }
}

public sealed class ProgramStatisticsDto
{
    public int ProgramId { get; init; }
    public string ProgramCode { get; init; } = "";
    public string ProgramName { get; init; } = "";
    public string Status { get; init; } = "Active";

    // AI29.1A names (retained)
    public int CourseCount { get; init; }
    public int StudentCount { get; init; }
    public int FacultyCount { get; init; }

    // AI29.1A.5 enterprise names (aliases for clients that prefer Total*)
    public int TotalStudents => StudentCount;
    public int TotalFaculty => FacultyCount;
    public int TotalCourses => CourseCount;
    public int TotalGroups { get; init; }
    public int TotalSemesters { get; init; }
    public int TotalSections { get; init; }
    public int TotalSubjects { get; init; }
    public int RunningClasses { get; init; }
    public decimal AttendancePercentage { get; init; }
    public decimal RoomUtilization { get; init; }
}

public sealed class AcademicHierarchyNodeDto
{
    public string Kind { get; init; } = "";
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<AcademicHierarchyNodeDto> Children { get; init; } = [];
}

public sealed class AcademicHierarchyDto
{
    public bool EnablePrograms { get; init; }
    public IReadOnlyList<AcademicHierarchyNodeDto> Roots { get; init; } = [];
}

public sealed class AcademicHierarchyStatisticsDto
{
    public bool EnablePrograms { get; init; }
    public int ProgramCount { get; init; }
    public int CourseCount { get; init; }
    public int GroupCount { get; init; }
    public int SemesterCount { get; init; }
    public int SectionCount { get; init; }
    public int SubjectCount { get; init; }
}

public sealed class AssignCourseProgramRequest
{
    public int CourseId { get; init; }
    public int? ProgramId { get; init; }
}

public sealed class ProgramPolicyDto
{
    public int Id { get; init; }
    public int ProgramId { get; init; }
    public decimal? MinimumAttendancePercent { get; init; }
    public decimal? CreditsRequired { get; init; }
    public decimal? PassMarks { get; init; }
    public int? MaximumBacklogs { get; init; }
    public int? MaximumSubjects { get; init; }
    public string? AcademicRules { get; init; }
}

public sealed class UpsertProgramPolicyRequest
{
    public decimal? MinimumAttendancePercent { get; init; }
    public decimal? CreditsRequired { get; init; }
    public decimal? PassMarks { get; init; }
    public int? MaximumBacklogs { get; init; }
    public int? MaximumSubjects { get; init; }
    public string? AcademicRules { get; init; }
}

public sealed class SubjectCatalogItemDto
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int DisplayOrder { get; init; }
}
