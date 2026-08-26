using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.6 Prompt 4 / Prompt 2A — Efficient read-only compatibility query.
/// Query predicates mirror <see cref="TeachingGroupRules.EnsureCompatibleWithTimetableEntry"/> scope
/// dimensions plus lifecycle attachability; assignment mutations still call the domain rule.
/// </summary>
public sealed class CompatibleTeachingGroupQueryService : ICompatibleTeachingGroupQueryService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITeachingGroupMembershipResolver _membershipResolver;

    public CompatibleTeachingGroupQueryService(
        ITimetableRepository timetableRepository,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITeachingGroupMembershipResolver membershipResolver)
    {
        _timetableRepository = timetableRepository;
        _db = db;
        _currentUser = currentUser;
        _membershipResolver = membershipResolver;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<CompatibleTeachingGroupOptionDto>> GetCompatibleTeachingGroupsForTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default)
    {
        if (timetableEntryId <= 0)
            throw new KeyNotFoundException("Timetable entry was not found.");

        var entry = await _timetableRepository.GetEntryByIdAsync(TenantId, timetableEntryId, cancellationToken)
            ?? throw new KeyNotFoundException("Timetable entry was not found.");

        // Scope predicate = EnsureCompatibleWithTimetableEntry identity dimensions (tenant via filters).
        // Lifecycle: Archived cannot attach (EnsureCanAttachToTimetableEntry); Draft/Active/Locked can.
        var candidates = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(g =>
                g.SubjectAllocationId == entry.SubjectAllocationId
                && g.CourseId == entry.CourseId
                && g.GroupId == entry.GroupId
                && g.SemesterId == entry.SemesterId
                && g.SubjectId == entry.SubjectId
                && g.Status != TeachingGroupStatus.Archived)
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        // Transparency: currently assigned TG is never silently cleared. If it is Archived (or otherwise
        // missing from assignable candidates), still return it once with IsAssignedToEntry = true.
        TeachingGroup? assignedExtra = null;
        if (entry.TeachingGroupId is int assignedId
            && candidates.All(c => c.Id != assignedId))
        {
            assignedExtra = await _db.SchedulingTeachingGroups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == assignedId, cancellationToken);
        }

        var groups = assignedExtra is null
            ? candidates
            : candidates.Concat([assignedExtra]).ToList();

        if (groups.Count == 0)
            return [];

        var resolvedCounts = await ResolveCountsAsync(groups, cancellationToken);
        var assignedTeachingGroupId = entry.TeachingGroupId;

        return groups
            .Select(g =>
            {
                var resolved = resolvedCounts.GetValueOrDefault(g.Id);
                var overMax = g.MaxTeachingCapacity is int max && max > 0 && resolved > max;
                return new CompatibleTeachingGroupOptionDto
                {
                    Id = g.Id,
                    Code = g.Code,
                    Name = g.Name,
                    Type = g.Type,
                    Status = g.Status,
                    ResolvedStudentCount = resolved,
                    ExpectedStudentCount = g.ExpectedStudentCount,
                    MaxTeachingCapacity = g.MaxTeachingCapacity,
                    IsAssignedToEntry = assignedTeachingGroupId == g.Id,
                    IsOverMaxTeachingCapacity = overMax,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Uses the authoritative membership resolver. Per-TG count (same pattern as management summaries).
    /// Does not load all memberships into a second ad-hoc algorithm; does not mutate.
    /// Does not consult room capacity when selecting Teaching Groups.
    /// </summary>
    private async Task<Dictionary<int, int>> ResolveCountsAsync(
        IReadOnlyList<TeachingGroup> groups,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<int, int>(groups.Count);
        foreach (var g in groups)
            counts[g.Id] = await _membershipResolver.ResolveCountAsync(g.Id, cancellationToken);
        return counts;
    }
}
