using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 2 — Teaching Group management application boundary (CRUD/archive).
/// Does not infer or auto-create TeachingGroups from SubjectAllocation.
/// Section SoT remains <see cref="ITeachingGroupSectionApplicationService"/>.
/// </summary>
public interface ITeachingGroupManagementApplicationService
{
    Task<IReadOnlyList<TeachingGroupSummaryDto>> ListBySubjectAllocationAsync(
        int subjectAllocationId,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupDetailDto> GetByIdAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupDetailDto> CreateAsync(
        CreateTeachingGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupDetailDto> UpdateAsync(
        int teachingGroupId,
        UpdateTeachingGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupDetailDto> ArchiveAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only membership rows. Mutation API deferred until Explicit/Hybrid write rules are finalized.
    /// </summary>
    Task<IReadOnlyList<TeachingGroupMembershipDto>> GetMembershipsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);
}
