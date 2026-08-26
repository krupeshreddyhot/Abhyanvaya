using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 2 — Teaching Group management list/detail DTOs.</summary>
public class TeachingGroupSummaryDto
{
    public int Id { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = null!;
    public TeachingGroupType Type { get; init; }
    public TeachingGroupStatus Status { get; init; }
    public TeachingGroupMembershipSource MembershipSource { get; init; }
    public TeachingGroupActivityKind ActivityKind { get; init; }
    public int SubjectAllocationId { get; init; }
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int SubjectId { get; init; }
    public int? ExpectedStudentCount { get; init; }
    public int? MaxTeachingCapacity { get; init; }
    public int ResolvedStudentCount { get; init; }
    public int LinkedSectionCount { get; init; }
    public int TimetableEntryCount { get; init; }
    public string? ExclusionGroupKey { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}

public sealed class TeachingGroupDetailDto : TeachingGroupSummaryDto
{
    public int DisplayOrder { get; init; }
    public string? Notes { get; init; }
    public int MembershipCount { get; init; }
    public IReadOnlyList<TeachingGroupSectionDto> Sections { get; init; } = [];
}

public sealed class CreateTeachingGroupRequest
{
    public int SubjectAllocationId { get; init; }
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public TeachingGroupType Type { get; init; }
    public TeachingGroupMembershipSource MembershipSource { get; init; }
    public TeachingGroupActivityKind ActivityKind { get; init; } = TeachingGroupActivityKind.Lecture;
    public int? ExpectedStudentCount { get; init; }
    public int? MaxTeachingCapacity { get; init; }
    public string? ExclusionGroupKey { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Notes { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class UpdateTeachingGroupRequest
{
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public TeachingGroupActivityKind ActivityKind { get; init; }
    public int? ExpectedStudentCount { get; init; }
    public int? MaxTeachingCapacity { get; init; }
    public string? ExclusionGroupKey { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Notes { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class TeachingGroupMembershipDto
{
    public int Id { get; init; }
    public int TeachingGroupId { get; init; }
    public int StudentId { get; init; }
    public TeachingGroupMembershipInclusion Inclusion { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class AddTeachingGroupSectionRequest
{
    public bool IsPrimary { get; init; }
}
