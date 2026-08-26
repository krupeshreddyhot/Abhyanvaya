using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 4 — Projects TeachingGroupSection (SoT) → TimetableSection.
/// Does not call the unit-of-work commit; does not create/infer TeachingGroups; does not mutate TeachingGroupId,
/// StudentSection, Attendance, or SubjectAllocation.
/// </summary>
public sealed class TimetableSectionProjector : ITimetableSectionProjector
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TimetableSectionProjector(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task SyncTeachingGroupSectionsToTimetableEntriesAsync(
        int teachingGroupId,
        IReadOnlyList<int>? canonicalSectionIds = null,
        CancellationToken cancellationToken = default)
    {
        if (teachingGroupId <= 0)
            throw new DomainException("A valid Teaching Group must be specified for projection.");

        var teachingGroup = await _db.SchedulingTeachingGroups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
            ?? throw new KeyNotFoundException("TeachingGroup was not found or is not available.");

        if (teachingGroup.IsDeleted)
            throw new DomainException("Deleted TeachingGroup cannot be projected to TimetableSection.");

        var sectionIds = Normalize(canonicalSectionIds)
            ?? await LoadCanonicalSectionIdsAsync(teachingGroupId, cancellationToken);

        var entries = await _db.SchedulingTimetableEntries
            .Where(e => e.TeachingGroupId == teachingGroupId)
            .Select(e => new { e.Id, e.TimetableId, e.TeachingGroupId })
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
            await ProjectEntryAsync(entry.TimetableId, entry.Id, sectionIds, cancellationToken);
    }

    public async Task SyncTeachingGroupSectionsToTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default)
    {
        if (timetableEntryId <= 0)
            throw new DomainException("A valid TimetableEntry must be specified for projection.");

        var entry = await _db.SchedulingTimetableEntries
            .FirstOrDefaultAsync(e => e.Id == timetableEntryId, cancellationToken)
            ?? throw new KeyNotFoundException("Timetable entry was not found.");

        if (entry.TeachingGroupId is not int teachingGroupId)
        {
            throw new DomainException(
                "This timetable entry has no Teaching Group assigned. Assign a Teaching Group before projecting sections.");
        }

        // Do not mutate TeachingGroupId — only read it.
        var sectionIds = await LoadCanonicalSectionIdsAsync(teachingGroupId, cancellationToken);
        await ProjectEntryAsync(entry.TimetableId, entry.Id, sectionIds, cancellationToken);
    }

    public async Task ClearTimetableEntryProjectionAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default)
    {
        if (timetableEntryId <= 0)
            throw new DomainException("A valid TimetableEntry must be specified for projection clear.");

        var entry = await _db.SchedulingTimetableEntries
            .FirstOrDefaultAsync(e => e.Id == timetableEntryId, cancellationToken)
            ?? throw new KeyNotFoundException("Timetable entry was not found.");

        // Soft-delete projected rows for this entry only — do not mutate TeachingGroupSection or TeachingGroupId.
        await ProjectEntryAsync(entry.TimetableId, entry.Id, Array.Empty<int>(), cancellationToken);
    }

    private async Task ProjectEntryAsync(
        int timetableId,
        int timetableEntryId,
        IReadOnlyList<int> canonicalSectionIds,
        CancellationToken cancellationToken)
    {
        var desired = Normalize(canonicalSectionIds) ?? Array.Empty<int>();
        var desiredSet = desired.ToHashSet();

        var existing = await _db.TimetableSections
            .Where(x => x.TimetableId == timetableId && x.TimetableEntryId == timetableEntryId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
        {
            if (!desiredSet.Contains(row.SectionId))
            {
                row.IsDeleted = true;
                row.UpdatedDate = DateTime.UtcNow;
                row.UpdatedBy = UserIdOrNull();
            }
        }

        var active = existing.Where(x => !x.IsDeleted).Select(x => x.SectionId).ToHashSet();
        foreach (var sectionId in desired)
        {
            if (active.Contains(sectionId))
                continue;

            await _db.AddAsync(new TimetableSection
            {
                TenantId = TenantId,
                TimetableId = timetableId,
                TimetableEntryId = timetableEntryId,
                SectionId = sectionId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserIdOrNull(),
            });
        }
    }

    private async Task<IReadOnlyList<int>> LoadCanonicalSectionIdsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken)
    {
        return await _db.SchedulingTeachingGroupSections
            .AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroupId)
            .OrderBy(x => x.SectionId)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<int>? Normalize(IReadOnlyList<int>? sectionIds)
    {
        if (sectionIds is null)
            return null;
        return sectionIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
    }

    private int? UserIdOrNull() => _currentUser.UserId > 0 ? _currentUser.UserId : null;
}
