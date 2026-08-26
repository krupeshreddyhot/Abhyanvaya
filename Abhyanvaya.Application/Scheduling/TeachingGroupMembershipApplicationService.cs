using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5 — Explicit/Hybrid membership mutations + reads.
/// Section / Combined / StudentSubject sources reject mutation.
/// Never writes StudentSection, StudentSubject, Attendance, or TimetableSection.
/// </summary>
public sealed class TeachingGroupMembershipApplicationService : ITeachingGroupMembershipApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITeachingGroupMembershipResolver _resolver;
    private readonly TeachingGroupMembershipResolver _resolverImpl;
    private readonly IAuditService? _audit;

    public TeachingGroupMembershipApplicationService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITeachingGroupMembershipResolver resolver,
        IAuditService? audit = null)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _resolver = resolver;
        _resolverImpl = resolver as TeachingGroupMembershipResolver
            ?? throw new InvalidOperationException(
                "ITeachingGroupMembershipResolver must be TeachingGroupMembershipResolver.");
        _audit = audit;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<TeachingGroupMembershipDto>> GetMembershipsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        await RequireTeachingGroupAsync(teachingGroupId, forMutation: false, cancellationToken);
        var rows = await _db.SchedulingTeachingGroupMemberships.AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .OrderByDescending(x => x.IsCurrent)
            .ThenBy(x => x.StudentId)
            .ToListAsync(cancellationToken);

        return rows.Select(MapOverlay).ToList();
    }

    public Task<IReadOnlyList<ResolvedTeachingGroupMemberDto>> GetResolvedMembersAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
        => _resolver.ResolveAsync(teachingGroupId, cancellationToken);

    public async Task<TeachingGroupMembershipMutationResultDto> AddMembersAsync(
        int teachingGroupId,
        AddTeachingGroupMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tg = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        EnsureMutationAllowed(tg);

        var studentIds = NormalizeIds(request.StudentIds);
        if (studentIds.Count == 0)
            return await BuildResultAsync(tg.Id, cancellationToken);

        await EnsureEligibleAsync(tg, studentIds, cancellationToken);

        var effectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await LoadCurrentOverlaysAsync(tg.Id, cancellationToken);

        foreach (var studentId in studentIds)
        {
            if (current.Any(x => x.StudentId == studentId
                                 && x.Inclusion == TeachingGroupMembershipInclusion.Include
                                 && x.IsCurrent
                                 && !x.IsDeleted))
                continue;

            var currentExclude = current.FirstOrDefault(x =>
                x.StudentId == studentId
                && x.Inclusion == TeachingGroupMembershipInclusion.Exclude
                && x.IsCurrent
                && !x.IsDeleted);
            if (currentExclude is not null)
                EndOverlay(currentExclude, effectiveFrom);

            var includeRow = new TeachingGroupMembership
            {
                TenantId = TenantId,
                TeachingGroupId = tg.Id,
                StudentId = studentId,
                Inclusion = TeachingGroupMembershipInclusion.Include,
                EffectiveFrom = effectiveFrom,
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            };
            await _db.AddAsync(includeRow);
            current.Add(includeRow);
        }

        await ValidateProposedStateAsync(tg, current, cancellationToken);
        await SaveMembershipChangesAsync(cancellationToken);
        await AuditAsync("AddMembers", tg.Id, new { studentIds }, cancellationToken);
        return await BuildResultAsync(tg.Id, cancellationToken);
    }

    public async Task<TeachingGroupMembershipMutationResultDto> RemoveMembersAsync(
        int teachingGroupId,
        RemoveTeachingGroupMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tg = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        EnsureMutationAllowed(tg);

        var studentIds = NormalizeIds(request.StudentIds);
        if (studentIds.Count == 0)
            return await BuildResultAsync(tg.Id, cancellationToken);

        var effectiveTo = request.EffectiveTo ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await LoadCurrentOverlaysAsync(tg.Id, cancellationToken);
        var baseIds = tg.MembershipSource == TeachingGroupMembershipSource.Hybrid
            ? await _resolverImpl.LoadEligibleBaseStudentIdsAsync(tg, cancellationToken)
            : new HashSet<int>();

        foreach (var studentId in studentIds)
        {
            var include = current.FirstOrDefault(x =>
                x.StudentId == studentId
                && x.Inclusion == TeachingGroupMembershipInclusion.Include
                && x.IsCurrent);
            if (include is not null)
                EndOverlay(include, effectiveTo);

            if (tg.MembershipSource != TeachingGroupMembershipSource.Hybrid || !baseIds.Contains(studentId))
                continue;

            if (current.Any(x => x.StudentId == studentId
                                 && x.Inclusion == TeachingGroupMembershipInclusion.Exclude
                                 && x.IsCurrent))
                continue;

            var excludeRow = new TeachingGroupMembership
            {
                TenantId = TenantId,
                TeachingGroupId = tg.Id,
                StudentId = studentId,
                Inclusion = TeachingGroupMembershipInclusion.Exclude,
                EffectiveFrom = effectiveTo,
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            };
            await _db.AddAsync(excludeRow);
            current.Add(excludeRow);
        }

        await SaveMembershipChangesAsync(cancellationToken);
        await AuditAsync("RemoveMembers", tg.Id, new { studentIds }, cancellationToken);
        return await BuildResultAsync(tg.Id, cancellationToken);
    }

    public async Task<TeachingGroupMembershipMutationResultDto> ReplaceMembershipsAsync(
        int teachingGroupId,
        ReplaceTeachingGroupMembershipsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tg = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        EnsureMutationAllowed(tg);

        var includes = NormalizeIds(request.IncludeStudentIds).ToHashSet();
        var excludes = NormalizeIds(request.ExcludeStudentIds).ToHashSet();

        if (tg.MembershipSource == TeachingGroupMembershipSource.ExplicitStudents && excludes.Count > 0)
            throw new DomainException("Exclude memberships are not used for Explicit Teaching Groups.");

        if (tg.MembershipSource == TeachingGroupMembershipSource.ExplicitStudents)
            excludes.Clear();

        if (includes.Overlaps(excludes))
            throw new DomainException("A student cannot be both included and excluded in the same replace set.");

        var allIds = includes.Concat(excludes).Distinct().ToList();
        await EnsureEligibleAsync(tg, allIds, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await LoadCurrentOverlaysAsync(tg.Id, cancellationToken);

        foreach (var row in current.Where(x => x.IsCurrent).ToList())
        {
            var keep = row.Inclusion == TeachingGroupMembershipInclusion.Include
                ? includes.Contains(row.StudentId)
                : excludes.Contains(row.StudentId);
            if (!keep)
                EndOverlay(row, today);
        }

        foreach (var studentId in includes)
        {
            if (current.Any(x => x.StudentId == studentId
                                 && x.Inclusion == TeachingGroupMembershipInclusion.Include
                                 && x.IsCurrent
                                 && !x.IsDeleted))
                continue;

            var includeRow = new TeachingGroupMembership
            {
                TenantId = TenantId,
                TeachingGroupId = tg.Id,
                StudentId = studentId,
                Inclusion = TeachingGroupMembershipInclusion.Include,
                EffectiveFrom = today,
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            };
            await _db.AddAsync(includeRow);
            current.Add(includeRow);
        }

        foreach (var studentId in excludes)
        {
            if (current.Any(x => x.StudentId == studentId
                                 && x.Inclusion == TeachingGroupMembershipInclusion.Exclude
                                 && x.IsCurrent
                                 && !x.IsDeleted))
                continue;

            var excludeRow = new TeachingGroupMembership
            {
                TenantId = TenantId,
                TeachingGroupId = tg.Id,
                StudentId = studentId,
                Inclusion = TeachingGroupMembershipInclusion.Exclude,
                EffectiveFrom = today,
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            };
            await _db.AddAsync(excludeRow);
            current.Add(excludeRow);
        }

        await ValidateProposedStateAsync(tg, current, cancellationToken);
        await SaveMembershipChangesAsync(cancellationToken);
        await AuditAsync("ReplaceMemberships", tg.Id, new { includes, excludes }, cancellationToken);
        return await BuildResultAsync(tg.Id, cancellationToken);
    }

    public Task<TeachingGroupMembershipMutationResultDto> RemoveMemberAsync(
        int teachingGroupId,
        int studentId,
        CancellationToken cancellationToken = default)
        => RemoveMembersAsync(
            teachingGroupId,
            new RemoveTeachingGroupMembersRequest { StudentIds = [studentId] },
            cancellationToken);

    private async Task ValidateProposedStateAsync(
        TeachingGroup tg,
        IReadOnlyList<TeachingGroupMembership> workingOverlays,
        CancellationToken cancellationToken)
    {
        EnsureMaxCapacityConfigured(tg);
        var proposedIds = await ComputeProposedResolvedIdsAsync(tg, workingOverlays, cancellationToken);
        await EnsureExclusionAgainstResolvedPeersAsync(tg, proposedIds, cancellationToken);
        try
        {
            tg.EnsureResolvedWithinMaxCapacity(proposedIds.Count);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException("Adding these students would exceed MaxTeachingCapacity.", ex);
        }
    }

    private static void EnsureMaxCapacityConfigured(TeachingGroup tg)
    {
        if (tg.MaxTeachingCapacity is int max && max <= 0)
            throw new DomainException("MaxTeachingCapacity must be a positive integer when configured; use null when unset.");
        if (tg.ExpectedStudentCount is int expected
            && tg.MaxTeachingCapacity is int ceiling
            && expected > ceiling)
        {
            throw new DomainException("ExpectedStudentCount cannot exceed MaxTeachingCapacity.");
        }
    }

    /// <summary>
    /// AI-SCHED-TG.5 Prompt 5A — ExclusionGroupKey uses peer resolved rosters (not Include rows only).
    /// 1. Skip when key empty or proposed set empty.
    /// 2. Load peers: same SubjectAllocationId + ExclusionGroupKey, not Archived, not self (tenant via filters).
    /// 3. Resolve each peer via ITeachingGroupMembershipResolver (read-only; no exclusion recursion).
    /// 4. Conflict when any proposed StudentId appears in a peer resolved set. Never mutates peers.
    /// </summary>
    private async Task EnsureExclusionAgainstResolvedPeersAsync(
        TeachingGroup tg,
        IReadOnlyCollection<int> proposedResolvedStudentIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tg.ExclusionGroupKey) || proposedResolvedStudentIds.Count == 0)
            return;

        var peers = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(x => x.SubjectAllocationId == tg.SubjectAllocationId
                        && x.Id != tg.Id
                        && x.ExclusionGroupKey == tg.ExclusionGroupKey
                        && x.Status != TeachingGroupStatus.Archived)
            .Select(x => new { x.Id, x.TenantId, x.SubjectAllocationId, x.ExclusionGroupKey, x.Status })
            .ToListAsync(cancellationToken);
        if (peers.Count == 0)
            return;

        var peerTuples = new List<(int TeachingGroupId, int TenantId, int SubjectAllocationId, string? ExclusionGroupKey, TeachingGroupStatus Status, IReadOnlyCollection<int> MemberStudentIds)>();
        foreach (var peer in peers)
        {
            var resolved = await _resolver.ResolveAsync(peer.Id, cancellationToken);
            peerTuples.Add((
                peer.Id,
                peer.TenantId,
                peer.SubjectAllocationId,
                peer.ExclusionGroupKey,
                peer.Status,
                resolved.Select(r => r.StudentId).ToHashSet()));
        }

        foreach (var studentId in proposedResolvedStudentIds)
        {
            try
            {
                TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
                    tg.TenantId,
                    tg.SubjectAllocationId,
                    studentId,
                    tg.ExclusionGroupKey,
                    tg.Id,
                    peerTuples);
            }
            catch (InvalidOperationException)
            {
                throw new ConcurrencyConflictException(
                    "This student already belongs to another Teaching Group in the same exclusion group.");
            }
        }
    }

    private async Task<HashSet<int>> ComputeProposedResolvedIdsAsync(
        TeachingGroup tg,
        IReadOnlyList<TeachingGroupMembership> workingOverlays,
        CancellationToken cancellationToken)
    {
        var includes = workingOverlays
            .Where(x => x.IsCurrent && !x.IsDeleted && x.Inclusion == TeachingGroupMembershipInclusion.Include)
            .Select(x => x.StudentId)
            .ToHashSet();
        var excludes = workingOverlays
            .Where(x => x.IsCurrent && !x.IsDeleted && x.Inclusion == TeachingGroupMembershipInclusion.Exclude)
            .Select(x => x.StudentId)
            .ToHashSet();

        if (tg.MembershipSource == TeachingGroupMembershipSource.ExplicitStudents)
            return includes;

        var baseIds = await _resolverImpl.LoadEligibleBaseStudentIdsAsync(tg, cancellationToken);

        if (tg.MembershipSource == TeachingGroupMembershipSource.Hybrid)
        {
            var set = baseIds.ToHashSet();
            foreach (var id in includes) set.Add(id);
            foreach (var id in excludes) set.Remove(id);
            return set;
        }

        return baseIds;
    }

    private static void EnsureMutationAllowed(TeachingGroup tg)
    {
        switch (tg.MembershipSource)
        {
            case TeachingGroupMembershipSource.ExplicitStudents:
            case TeachingGroupMembershipSource.Hybrid:
                return;
            case TeachingGroupMembershipSource.Section:
            case TeachingGroupMembershipSource.CombinedSections:
                throw new DomainException(
                    "Membership mutation is not allowed for Section-derived Teaching Groups. Change section links instead.");
            case TeachingGroupMembershipSource.StudentSubject:
                throw new DomainException(
                    "Membership mutation is not allowed for StudentSubject-derived Teaching Groups.");
            default:
                throw new DomainException(
                    "Membership mutation is not allowed for this Teaching Group membership source.");
        }
    }

    private async Task EnsureEligibleAsync(
        TeachingGroup tg,
        IReadOnlyList<int> studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
            return;

        var students = await _db.Students.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.SemesterId, s.TenantId })
            .ToListAsync(cancellationToken);

        if (students.Count != studentIds.Distinct().Count())
            throw new DomainException("One or more students were not found or are not available.");

        foreach (var s in students)
        {
            if (s.TenantId != tg.TenantId)
                throw new DomainException("Student belongs to another tenant.");
            if (s.CourseId != tg.CourseId || s.GroupId != tg.GroupId || s.SemesterId != tg.SemesterId)
                throw new DomainException("This student is outside the Teaching Group's academic scope.");
        }
    }

    private async Task<List<TeachingGroupMembership>> LoadCurrentOverlaysAsync(
        int teachingGroupId,
        CancellationToken cancellationToken)
        => await _db.SchedulingTeachingGroupMemberships
            .Where(x => x.TeachingGroupId == teachingGroupId && x.IsCurrent)
            .ToListAsync(cancellationToken);

    private void EndOverlay(TeachingGroupMembership row, DateOnly effectiveTo)
    {
        row.IsCurrent = false;
        row.EffectiveTo = effectiveTo;
        row.IsDeleted = true;
        row.UpdatedDate = DateTime.UtcNow;
        row.UpdatedBy = UserIdOrNull();
    }

    private async Task SaveMembershipChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            TeachingGroupMembershipPersistenceExceptionMapper.RethrowUnlessCurrentMembershipUniqueViolation(ex);
        }
    }

    private async Task<TeachingGroupMembershipMutationResultDto> BuildResultAsync(
        int teachingGroupId,
        CancellationToken cancellationToken)
    {
        var memberships = await GetMembershipsAsync(teachingGroupId, cancellationToken);
        var resolved = await _resolver.ResolveAsync(teachingGroupId, cancellationToken);
        return new TeachingGroupMembershipMutationResultDto
        {
            TeachingGroupId = teachingGroupId,
            ResolvedStudentCount = resolved.Count,
            Memberships = memberships.Where(m => m.IsCurrent).ToList(),
            ResolvedMembers = resolved,
        };
    }

    private async Task<TeachingGroup> RequireTeachingGroupAsync(
        int teachingGroupId,
        bool forMutation,
        CancellationToken cancellationToken)
    {
        if (teachingGroupId <= 0)
            throw new DomainException("A valid Teaching Group must be specified.");

        var query = forMutation
            ? _db.SchedulingTeachingGroups.AsQueryable()
            : _db.SchedulingTeachingGroups.AsNoTracking();

        var tg = await query.FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Teaching Group was not found.");

        if (forMutation)
        {
            try
            {
                tg.EnsureCanMutate();
            }
            catch (InvalidOperationException ex)
            {
                var message = tg.Status switch
                {
                    TeachingGroupStatus.Locked => "Locked Teaching Groups cannot change membership.",
                    TeachingGroupStatus.Archived => "Archived Teaching Groups cannot change membership.",
                    _ => ex.Message,
                };
                throw new DomainException(message, ex);
            }
        }

        return tg;
    }

    private static IReadOnlyList<int> NormalizeIds(IReadOnlyList<int>? ids)
        => (ids ?? Array.Empty<int>()).Where(id => id > 0).Distinct().OrderBy(id => id).ToList();

    private static TeachingGroupMembershipDto MapOverlay(TeachingGroupMembership x) => new()
    {
        Id = x.Id,
        TeachingGroupId = x.TeachingGroupId,
        StudentId = x.StudentId,
        Inclusion = x.Inclusion,
        EffectiveFrom = x.EffectiveFrom,
        EffectiveTo = x.EffectiveTo,
        IsCurrent = x.IsCurrent,
    };

    private async Task AuditAsync(string operation, int teachingGroupId, object payload, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;
        await _audit.RecordAsync(
            "TeachingGroupMembership",
            teachingGroupId.ToString(),
            AuditAction.Updated,
            oldValues: null,
            newValues: new { operation, payload },
            cancellationToken);
    }

    private int? UserIdOrNull() => _currentUser.UserId > 0 ? _currentUser.UserId : null;
}
