using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 3/4 — TeachingGroupSection source-of-truth mutations.
/// Projection is delegated to <see cref="ITimetableSectionProjector"/>; bridge path commits once.
/// </summary>
public sealed class TeachingGroupSectionApplicationService : ITeachingGroupSectionApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimetableSectionProjector _projector;

    public TeachingGroupSectionApplicationService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITimetableSectionProjector projector)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _projector = projector;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<TeachingGroupSectionDto>> GetSectionsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        await RequireTeachingGroupAsync(teachingGroupId, forMutation: false, cancellationToken);
        return await MapCurrentLinksAsync(teachingGroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<TeachingGroupSectionDto>> ReplaceSectionsAsync(
        int teachingGroupId,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken = default)
    {
        await ApplyReplaceSectionsAsync(teachingGroupId, sectionIds, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapCurrentLinksAsync(teachingGroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<TeachingGroupSectionDto>> ReplaceSectionsAndProjectAsync(
        int teachingGroupId,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken = default)
    {
        // Single transaction for bridge / Attendance safety:
        // TeachingGroupSection SoT → TimetableSection projection → one SaveChanges.
        var desired = await ApplyReplaceSectionsAsync(teachingGroupId, sectionIds, cancellationToken);
        await _projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(
            teachingGroupId,
            desired,
            cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapCurrentLinksAsync(teachingGroupId, cancellationToken);
    }

    public async Task<TeachingGroupSectionDto> AddSectionAsync(
        int teachingGroupId,
        int sectionId,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        await ApplyAddSectionAsync(teachingGroupId, sectionId, isPrimary, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        var mapped = await MapCurrentLinksAsync(teachingGroupId, cancellationToken);
        return mapped.Single(x => x.SectionId == sectionId);
    }

    public async Task RemoveSectionAsync(
        int teachingGroupId,
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        await ApplyRemoveSectionAsync(teachingGroupId, sectionId, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<TeachingGroupSectionDto> AddSectionAndProjectAsync(
        int teachingGroupId,
        int sectionId,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        var canonical = await ApplyAddSectionAsync(teachingGroupId, sectionId, isPrimary, cancellationToken);
        await _projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(
            teachingGroupId,
            canonical,
            cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        var mapped = await MapCurrentLinksAsync(teachingGroupId, cancellationToken);
        return mapped.Single(x => x.SectionId == sectionId);
    }

    public async Task RemoveSectionAndProjectAsync(
        int teachingGroupId,
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        var remaining = await ApplyRemoveSectionAsync(teachingGroupId, sectionId, cancellationToken);
        await _projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(
            teachingGroupId,
            remaining,
            cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private async Task<IReadOnlyList<int>> ApplyAddSectionAsync(
        int teachingGroupId,
        int sectionId,
        bool isPrimary,
        CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            throw new DomainException("A valid Section must be specified.");

        var teachingGroup = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        var currentIds = await _db.SchedulingTeachingGroupSections
            .AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);

        if (currentIds.Contains(sectionId))
            throw new DomainException("This Section is already linked to the Teaching Group.");

        var proposed = currentIds.Append(sectionId).ToList();
        ValidateTypeRules(teachingGroup, proposed);
        await LoadAndValidateSectionsAsync(teachingGroup, [sectionId], cancellationToken);

        if (isPrimary)
        {
            var existing = await _db.SchedulingTeachingGroupSections
                .Where(x => x.TeachingGroupId == teachingGroupId)
                .ToListAsync(cancellationToken);
            foreach (var link in existing)
                link.IsPrimary = false;
        }

        await _db.AddAsync(new TeachingGroupSection
        {
            TenantId = TenantId,
            TeachingGroupId = teachingGroupId,
            SectionId = sectionId,
            IsPrimary = isPrimary || currentIds.Count == 0,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = UserIdOrNull(),
        });

        return proposed;
    }

    private async Task<IReadOnlyList<int>> ApplyRemoveSectionAsync(
        int teachingGroupId,
        int sectionId,
        CancellationToken cancellationToken)
    {
        if (sectionId <= 0)
            throw new DomainException("A valid Section must be specified.");

        var teachingGroup = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        var link = await _db.SchedulingTeachingGroupSections
            .FirstOrDefaultAsync(x => x.TeachingGroupId == teachingGroupId && x.SectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Teaching Group section link was not found.");

        var remaining = await _db.SchedulingTeachingGroupSections
            .AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroupId && x.SectionId != sectionId)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);

        ValidateTypeRules(teachingGroup, remaining);

        link.IsDeleted = true;
        link.UpdatedDate = DateTime.UtcNow;
        link.UpdatedBy = UserIdOrNull();
        await EnsurePrimaryFlagAsync(teachingGroupId, cancellationToken);
        return remaining;
    }

    /// <summary>Stages SoT replace without SaveChanges. Returns canonical section ids for projection.</summary>
    private async Task<IReadOnlyList<int>> ApplyReplaceSectionsAsync(
        int teachingGroupId,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken)
    {
        var teachingGroup = await RequireTeachingGroupAsync(teachingGroupId, forMutation: true, cancellationToken);
        var desired = NormalizeSectionIds(sectionIds);
        ValidateTypeRules(teachingGroup, desired);

        await LoadAndValidateSectionsAsync(teachingGroup, desired, cancellationToken);
        var existing = await _db.SchedulingTeachingGroupSections
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .ToListAsync(cancellationToken);

        var desiredSet = desired.ToHashSet();
        foreach (var link in existing)
        {
            if (!desiredSet.Contains(link.SectionId))
            {
                link.IsDeleted = true;
                link.UpdatedDate = DateTime.UtcNow;
                link.UpdatedBy = UserIdOrNull();
            }
        }

        var activeSectionIds = existing.Where(x => !x.IsDeleted).Select(x => x.SectionId).ToHashSet();
        var hasPrimary = existing.Any(x => !x.IsDeleted && x.IsPrimary);
        foreach (var sectionId in desired)
        {
            if (activeSectionIds.Contains(sectionId))
                continue;

            await _db.AddAsync(new TeachingGroupSection
            {
                TenantId = TenantId,
                TeachingGroupId = teachingGroupId,
                SectionId = sectionId,
                IsPrimary = !hasPrimary,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            });
            hasPrimary = true;
        }

        await EnsurePrimaryFlagAsync(teachingGroupId, cancellationToken);
        return desired;
    }

    private async Task EnsurePrimaryFlagAsync(int teachingGroupId, CancellationToken cancellationToken)
    {
        var active = await _db.SchedulingTeachingGroupSections
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        if (active.Count == 0)
            return;
        if (active.Any(x => x.IsPrimary))
            return;
        active[0].IsPrimary = true;
        active[0].UpdatedDate = DateTime.UtcNow;
    }

    private static IReadOnlyList<int> NormalizeSectionIds(IReadOnlyList<int>? sectionIds)
        => (sectionIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();

    private static void ValidateTypeRules(TeachingGroup teachingGroup, IReadOnlyList<int> sectionIds)
    {
        try
        {
            TeachingGroupRules.ValidateSectionLinks(teachingGroup.Type, sectionIds);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }
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

        var teachingGroup = await query.FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
            ?? throw new KeyNotFoundException("TeachingGroup was not found or is not available.");

        if (forMutation)
        {
            try
            {
                teachingGroup.EnsureCanMutate();
            }
            catch (InvalidOperationException ex)
            {
                throw new DomainException(ex.Message, ex);
            }
        }

        return teachingGroup;
    }

    private async Task<IReadOnlyList<Section>> LoadAndValidateSectionsAsync(
        TeachingGroup teachingGroup,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken)
    {
        if (sectionIds.Count == 0)
            return [];

        var sections = await _db.Sections.AsNoTracking()
            .Where(s => sectionIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (sections.Count != sectionIds.Count)
            throw new DomainException("One or more Sections were not found or are not available for this Teaching Group.");

        foreach (var section in sections)
        {
            TeachingGroupRules.EnsureSectionCompatibleWithTeachingGroup(
                teachingGroup,
                section.TenantId,
                section.AcademicYearId,
                section.CourseId,
                section.GroupId,
                section.SemesterId);
        }

        return sections;
    }

    private async Task<IReadOnlyList<TeachingGroupSectionDto>> MapCurrentLinksAsync(
        int teachingGroupId,
        CancellationToken cancellationToken)
    {
        var links = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SectionId)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
            return [];

        var sectionIds = links.Select(x => x.SectionId).Distinct().ToList();
        var sections = await _db.Sections.AsNoTracking()
            .Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s, cancellationToken);

        return links.Select(link =>
        {
            sections.TryGetValue(link.SectionId, out var section);
            return new TeachingGroupSectionDto
            {
                Id = link.Id,
                TeachingGroupId = link.TeachingGroupId,
                SectionId = link.SectionId,
                IsPrimary = link.IsPrimary,
                SectionCode = section?.SectionCode,
                SectionName = section?.SectionName,
            };
        }).ToList();
    }

    private int? UserIdOrNull() => _currentUser.UserId > 0 ? _currentUser.UserId : null;
}
