namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionReadinessDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    /// <summary>Ready | Warning | Blocked</summary>
    public string OverallStatus { get; init; } = "Ready";
    public IReadOnlyList<SectionReadinessCheckDto> Checks { get; init; } = [];
}

public sealed class SectionReadinessCheckDto
{
    public string Area { get; init; } = "";
    /// <summary>Ready | Warning | Blocked</summary>
    public string Status { get; init; } = "Ready";
    public string Message { get; init; } = "";
}

public sealed class SectionGroupDto
{
    public int Id { get; init; }
    public int CollegeId { get; init; }
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public string GroupCode { get; init; } = "";
    public string GroupName { get; init; } = "";
    public string Status { get; init; } = "Active";
    public string? Notes { get; init; }
    public IReadOnlyList<int> CurrentSectionIds { get; init; } = [];
}

public sealed class CreateSectionGroupRequest
{
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public string GroupCode { get; init; } = "";
    public string GroupName { get; init; } = "";
    public string? Notes { get; init; }
    public IReadOnlyList<int> SectionIds { get; init; } = [];
}

public sealed class UpdateSectionGroupMembersRequest
{
    public IReadOnlyList<int> SectionIds { get; init; } = [];
    public DateOnly? EffectiveFrom { get; init; }
}
