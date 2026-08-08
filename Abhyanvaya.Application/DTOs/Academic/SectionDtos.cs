namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionDto
{
    public int Id { get; init; }
    public int CollegeId { get; init; }
    public int AcademicYearId { get; init; }
    public string? AcademicYearName { get; init; }
    public int CourseId { get; init; }
    public string? CourseName { get; init; }
    public int GroupId { get; init; }
    public string? GroupName { get; init; }
    public int SemesterId { get; init; }
    public string? SemesterName { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int DisplayOrder { get; init; }
    public int MaximumStrength { get; init; }
    public string Status { get; init; } = "Active";
    public int CurrentStrength { get; init; }
    public int RemainingCapacity { get; init; }

    // AI29.1B
    public string SectionTypeCode { get; init; } = "Regular";
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingListCount { get; init; }
    public int? ParentSectionId { get; init; }
    public int? SectionGroupId { get; init; }
    public double? OccupancyPercent { get; init; }
    public string? CapacityStatus { get; init; }
}

public sealed class CreateSectionRequest
{
    public int? CollegeId { get; init; }
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int DisplayOrder { get; init; }
    public int MaximumStrength { get; init; } = 60;
    public string Status { get; init; } = "Active";
    public string SectionTypeCode { get; init; } = "Regular";
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingListCount { get; init; }
}

public sealed class UpdateSectionRequest
{
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int DisplayOrder { get; init; }
    public int MaximumStrength { get; init; }
    /// <summary>Ignored for direct updates — use lifecycle transition API.</summary>
    public string Status { get; init; } = "Active";
    public string SectionTypeCode { get; init; } = "Regular";
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingListCount { get; init; }
}

public sealed class StudentSectionDto
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public string? StudentNumber { get; init; }
    public string? StudentName { get; init; }
    public int SectionId { get; init; }
    public string? SectionCode { get; init; }
    public string? SectionName { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public bool IsCurrent { get; init; }
    public string? TransferReason { get; init; }
}

public sealed class AssignStudentSectionRequest
{
    public int StudentId { get; init; }
    public int SectionId { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class TransferStudentSectionRequest
{
    public int StudentId { get; init; }
    public int TargetSectionId { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public string? Reason { get; init; }
}

public sealed class FacultySectionDto
{
    public int Id { get; init; }
    public int FacultyId { get; init; }
    public string? FacultyName { get; init; }
    public int SectionId { get; init; }
    public string? SectionCode { get; init; }
    public string? SectionName { get; init; }
    public int AcademicYearId { get; init; }
    public string Role { get; init; } = "Primary";
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class AssignFacultySectionRequest
{
    public int FacultyId { get; init; }
    public int SectionId { get; init; }
    public int AcademicYearId { get; init; }
    public string Role { get; init; } = "Primary";
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class TimetableSectionDto
{
    public int Id { get; init; }
    public int TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public int SectionId { get; init; }
    public string? SectionCode { get; init; }
    public string? SectionName { get; init; }
}

public sealed class SetTimetableSectionsRequest
{
    public int? TimetableEntryId { get; init; }
    public IReadOnlyList<int> SectionIds { get; init; } = [];
}

public sealed class AutoAllocateSectionsRequest
{
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    /// <summary>Alphabetical | GenderBalance | Merit | Random | CapacityBased</summary>
    public string? Strategy { get; init; }
}

public sealed class AutoAllocateSectionsResult
{
    public int AssignedCount { get; init; }
    public int SkippedCount { get; init; }
    public string Strategy { get; init; } = "";
    public IReadOnlyList<string> Messages { get; init; } = [];
}

public sealed class SectionStatisticsDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int MaximumStrength { get; init; }
    public int StudentCount { get; init; }
    public int FacultyCount { get; init; }
    public int RemainingCapacity { get; init; }
    public double UtilizationPercent { get; init; }
}

public sealed class SectionReportRowDto
{
    public string ReportKind { get; init; } = "";
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string? Detail { get; init; }
    public int Count { get; init; }
}
