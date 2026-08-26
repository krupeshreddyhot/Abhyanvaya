using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5 — Teaching Group membership overlays + mutations.
/// Does not write StudentSection, StudentSubject, Attendance, or TimetableSection.
/// </summary>
public interface ITeachingGroupMembershipApplicationService
{
    Task<IReadOnlyList<TeachingGroupMembershipDto>> GetMembershipsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResolvedTeachingGroupMemberDto>> GetResolvedMembersAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupMembershipMutationResultDto> AddMembersAsync(
        int teachingGroupId,
        AddTeachingGroupMembersRequest request,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupMembershipMutationResultDto> RemoveMembersAsync(
        int teachingGroupId,
        RemoveTeachingGroupMembersRequest request,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupMembershipMutationResultDto> ReplaceMembershipsAsync(
        int teachingGroupId,
        ReplaceTeachingGroupMembershipsRequest request,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupMembershipMutationResultDto> RemoveMemberAsync(
        int teachingGroupId,
        int studentId,
        CancellationToken cancellationToken = default);
}
