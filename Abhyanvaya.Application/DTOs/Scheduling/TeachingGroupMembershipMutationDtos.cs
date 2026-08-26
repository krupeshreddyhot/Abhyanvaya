using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 5 — Resolved membership roster DTOs / mutation requests.</summary>
public enum TeachingGroupMemberProvenance
{
    Derived = 1,
    ExplicitInclude = 2,
}

public sealed class ResolvedTeachingGroupMemberDto
{
    public int StudentId { get; init; }
    public TeachingGroupMemberProvenance Provenance { get; init; }
}

public sealed class AddTeachingGroupMembersRequest
{
    public IReadOnlyList<int> StudentIds { get; init; } = [];
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class RemoveTeachingGroupMembersRequest
{
    public IReadOnlyList<int> StudentIds { get; init; } = [];
    public DateOnly? EffectiveTo { get; init; }
}

public sealed class ReplaceTeachingGroupMembershipsRequest
{
    public IReadOnlyList<int> IncludeStudentIds { get; init; } = [];
    /// <summary>Hybrid only. Ignored (must be empty) for ExplicitStudents.</summary>
    public IReadOnlyList<int> ExcludeStudentIds { get; init; } = [];
}

public sealed class TeachingGroupMembershipMutationResultDto
{
    public int TeachingGroupId { get; init; }
    public int ResolvedStudentCount { get; init; }
    public IReadOnlyList<TeachingGroupMembershipDto> Memberships { get; init; } = [];
    public IReadOnlyList<ResolvedTeachingGroupMemberDto> ResolvedMembers { get; init; } = [];
}
