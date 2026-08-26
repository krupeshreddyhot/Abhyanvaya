using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5 — Side-effect-free Teaching Group membership resolver (Model B).
/// Never mutates the database; never creates TeachingGroups.
/// </summary>
public interface ITeachingGroupMembershipResolver
{
    Task<IReadOnlyList<ResolvedTeachingGroupMemberDto>> ResolveAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<int> ResolveCountAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);
}
