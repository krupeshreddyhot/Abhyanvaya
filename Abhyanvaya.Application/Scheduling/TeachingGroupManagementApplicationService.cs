using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 2 — Explicit Teaching Group management.
/// Never infers TG from SubjectAllocation; never writes TimetableSection.
/// </summary>
public sealed class TeachingGroupManagementApplicationService : ITeachingGroupManagementApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITeachingGroupSectionApplicationService _sections;
    private readonly ITeachingGroupMembershipResolver _membershipResolver;

    public TeachingGroupManagementApplicationService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITeachingGroupSectionApplicationService sections,
        ITeachingGroupMembershipResolver membershipResolver)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _sections = sections;
        _membershipResolver = membershipResolver;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<TeachingGroupSummaryDto>> ListBySubjectAllocationAsync(
        int subjectAllocationId,
        CancellationToken cancellationToken = default)
    {
        if (subjectAllocationId <= 0)
            throw new DomainException("SubjectAllocationId is required.");

        await RequireSubjectAllocationAsync(subjectAllocationId, cancellationToken);

        var groups = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(x => x.SubjectAllocationId == subjectAllocationId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return await MapSummariesAsync(groups, cancellationToken);
    }

    public async Task<TeachingGroupDetailDto> GetByIdAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireTeachingGroupAsync(teachingGroupId, forMutation: false, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<TeachingGroupDetailDto> CreateAsync(
        CreateTeachingGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Teaching Group name is required.");
        if (request.SubjectAllocationId <= 0)
            throw new DomainException("SubjectAllocationId is required.");

        var allocation = await RequireSubjectAllocationAsync(request.SubjectAllocationId, cancellationToken);

        var semesterHistorical = await _db.Semesters.AsNoTracking()
            .Where(s => s.Id == allocation.SemesterId && s.TenantId == TenantId)
            .Select(s => (bool?)s.IsHistoricalArchive)
            .FirstOrDefaultAsync(cancellationToken);
        if (semesterHistorical is null)
            throw new DomainException($"Semester '{allocation.SemesterId}' was not found.");
        if (semesterHistorical.Value)
            throw new DomainException(Academic.OperationalSemesterRules.HistoricalRejectedMessage);

        try
        {
            TeachingGroupRules.ValidateCapacitySplitExclusionKey(request.Type, request.ExclusionGroupKey);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }

        // Section cardinality is enforced on TeachingGroupSection mutations, not at create
        // (SectionDerived/CombinedSections may be created empty, then linked via section API).

        var code = NormalizeCode(request.Code);
        await EnsureCodeUniqueAsync(allocation.Id, code, excludingId: null, cancellationToken);

        var entity = new TeachingGroup
        {
            TenantId = TenantId,
            AcademicYearId = allocation.AcademicYearId,
            CourseId = allocation.CourseId,
            GroupId = allocation.GroupId,
            SemesterId = allocation.SemesterId,
            SubjectId = allocation.SubjectId,
            SubjectAllocationId = allocation.Id,
            Type = request.Type,
            MembershipSource = request.MembershipSource,
            Status = TeachingGroupStatus.Draft,
            ActivityKind = request.ActivityKind,
            Code = code,
            Name = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder,
            ExclusionGroupKey = string.IsNullOrWhiteSpace(request.ExclusionGroupKey)
                ? null
                : request.ExclusionGroupKey.Trim(),
            EffectiveFrom = request.EffectiveFrom ?? allocation.EffectiveFrom,
            EffectiveTo = request.EffectiveTo ?? allocation.EffectiveTo,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedDate = DateTime.UtcNow,
            CreatedBy = UserIdOrNull(),
        };

        try
        {
            entity.SetCapacity(request.ExpectedStudentCount, request.MaxTeachingCapacity);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }

        await _db.AddAsync(entity);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<TeachingGroupDetailDto> UpdateAsync(
        int teachingGroupId,
        UpdateTeachingGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Teaching Group name is required.");

        var entity = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);

        try
        {
            TeachingGroupRules.ValidateCapacitySplitExclusionKey(entity.Type, request.ExclusionGroupKey);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }

        var code = NormalizeCode(request.Code);
        await EnsureCodeUniqueAsync(entity.SubjectAllocationId, code, entity.Id, cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Code = code;
        entity.ActivityKind = request.ActivityKind;
        entity.DisplayOrder = request.DisplayOrder;
        entity.ExclusionGroupKey = string.IsNullOrWhiteSpace(request.ExclusionGroupKey)
            ? null
            : request.ExclusionGroupKey.Trim();
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = UserIdOrNull();

        try
        {
            entity.SetCapacity(request.ExpectedStudentCount, request.MaxTeachingCapacity);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }

        // Do not change SubjectAllocationId / academic scope / Type / MembershipSource here.
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<TeachingGroupDetailDto> ArchiveAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        // Soft archive via status — never hard-delete (references may exist on TimetableEntry / sections / membership).
        // TransitionTo is allowed from Draft/Active/Locked; EnsureCanMutate is intentionally not used here
        // because Locked TeachingGroups can still be archived.
        var tracked = await _db.SchedulingTeachingGroups
            .FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Teaching Group was not found.");

        if (tracked.Status == TeachingGroupStatus.Archived)
            return await MapDetailAsync(tracked, cancellationToken);

        try
        {
            tracked.TransitionTo(TeachingGroupStatus.Archived);
            tracked.UpdatedBy = UserIdOrNull();
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            return await MapDetailAsync(tracked, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }
    }

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

        return rows.Select(x => new TeachingGroupMembershipDto
        {
            Id = x.Id,
            TeachingGroupId = x.TeachingGroupId,
            StudentId = x.StudentId,
            Inclusion = x.Inclusion,
            EffectiveFrom = x.EffectiveFrom,
            EffectiveTo = x.EffectiveTo,
            IsCurrent = x.IsCurrent,
        }).ToList();
    }

    private async Task EnsureCodeUniqueAsync(
        int subjectAllocationId,
        string? code,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        var exists = await _db.SchedulingTeachingGroups.AsNoTracking()
            .AnyAsync(
                x => x.SubjectAllocationId == subjectAllocationId
                     && x.Code == code
                     && (excludingId == null || x.Id != excludingId.Value),
                cancellationToken);
        if (exists)
            throw new DomainException("A Teaching Group with this code already exists for the selected Subject Allocation.");
    }

    private static string? NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    private async Task<SubjectAllocation> RequireSubjectAllocationAsync(
        int subjectAllocationId,
        CancellationToken cancellationToken)
    {
        var allocation = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == subjectAllocationId, cancellationToken)
            ?? throw new KeyNotFoundException("Subject Allocation was not found.");
        return allocation;
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

        var entity = await query.FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Teaching Group was not found.");

        if (forMutation)
        {
            try
            {
                entity.EnsureCanMutate();
            }
            catch (InvalidOperationException ex)
            {
                throw new DomainException(ex.Message, ex);
            }
        }

        return entity;
    }

    private async Task<IReadOnlyList<TeachingGroupSummaryDto>> MapSummariesAsync(
        IReadOnlyList<TeachingGroup> groups,
        CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
            return [];

        var ids = groups.Select(g => g.Id).ToList();
        var sectionCounts = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => ids.Contains(x.TeachingGroupId))
            .GroupBy(x => x.TeachingGroupId)
            .Select(g => new { TeachingGroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeachingGroupId, x => x.Count, cancellationToken);

        var resolvedCounts = new Dictionary<int, int>();
        foreach (var id in ids)
            resolvedCounts[id] = await _membershipResolver.ResolveCountAsync(id, cancellationToken);

        var entryCounts = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(x => x.TeachingGroupId != null && ids.Contains(x.TeachingGroupId.Value))
            .GroupBy(x => x.TeachingGroupId!.Value)
            .Select(g => new { TeachingGroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeachingGroupId, x => x.Count, cancellationToken);

        return groups.Select(g => new TeachingGroupSummaryDto
        {
            Id = g.Id,
            Code = g.Code,
            Name = g.Name,
            Type = g.Type,
            Status = g.Status,
            MembershipSource = g.MembershipSource,
            ActivityKind = g.ActivityKind,
            SubjectAllocationId = g.SubjectAllocationId,
            AcademicYearId = g.AcademicYearId,
            CourseId = g.CourseId,
            GroupId = g.GroupId,
            SemesterId = g.SemesterId,
            SubjectId = g.SubjectId,
            ExpectedStudentCount = g.ExpectedStudentCount,
            MaxTeachingCapacity = g.MaxTeachingCapacity,
            ResolvedStudentCount = resolvedCounts.GetValueOrDefault(g.Id),
            LinkedSectionCount = sectionCounts.GetValueOrDefault(g.Id),
            TimetableEntryCount = entryCounts.GetValueOrDefault(g.Id),
            ExclusionGroupKey = g.ExclusionGroupKey,
            EffectiveFrom = g.EffectiveFrom,
            EffectiveTo = g.EffectiveTo,
        }).ToList();
    }

    private async Task<TeachingGroupDetailDto> MapDetailAsync(
        TeachingGroup entity,
        CancellationToken cancellationToken)
    {
        var summaries = await MapSummariesAsync([entity], cancellationToken);
        var summary = summaries[0];
        var sections = await _sections.GetSectionsAsync(entity.Id, cancellationToken);
        var membershipCount = await _db.SchedulingTeachingGroupMemberships.AsNoTracking()
            .CountAsync(x => x.TeachingGroupId == entity.Id && x.IsCurrent, cancellationToken);

        return new TeachingGroupDetailDto
        {
            Id = summary.Id,
            Code = summary.Code,
            Name = summary.Name,
            Type = summary.Type,
            Status = summary.Status,
            MembershipSource = summary.MembershipSource,
            ActivityKind = summary.ActivityKind,
            SubjectAllocationId = summary.SubjectAllocationId,
            AcademicYearId = summary.AcademicYearId,
            CourseId = summary.CourseId,
            GroupId = summary.GroupId,
            SemesterId = summary.SemesterId,
            SubjectId = summary.SubjectId,
            ExpectedStudentCount = summary.ExpectedStudentCount,
            MaxTeachingCapacity = summary.MaxTeachingCapacity,
            ResolvedStudentCount = summary.ResolvedStudentCount,
            LinkedSectionCount = summary.LinkedSectionCount,
            TimetableEntryCount = summary.TimetableEntryCount,
            ExclusionGroupKey = summary.ExclusionGroupKey,
            EffectiveFrom = summary.EffectiveFrom,
            EffectiveTo = summary.EffectiveTo,
            DisplayOrder = entity.DisplayOrder,
            Notes = entity.Notes,
            MembershipCount = membershipCount,
            Sections = sections,
        };
    }

    private int? UserIdOrNull() => _currentUser.UserId > 0 ? _currentUser.UserId : null;
}
