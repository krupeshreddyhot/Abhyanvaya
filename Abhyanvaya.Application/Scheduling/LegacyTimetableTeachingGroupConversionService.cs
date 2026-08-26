using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 7 — Controlled, explicitly invoked TimetableEntry → TeachingGroup conversion
/// for disposable pre-production test data. Never creates TeachingGroups, never infers from
/// SubjectAllocation, never runs on GET/startup/attendance resolve.
/// </summary>
public sealed class LegacyTimetableTeachingGroupConversionService : ILegacyTimetableTeachingGroupConversionService
{
    public const string OutcomeConverted = "Converted";
    public const string OutcomeSkipped = "Skipped";
    public const string OutcomeRejected = "Rejected";

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITeachingGroupSectionApplicationService _teachingGroupSections;

    public LegacyTimetableTeachingGroupConversionService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITeachingGroupSectionApplicationService teachingGroupSections)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _teachingGroupSections = teachingGroupSections;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<LegacyTimetableEntryWithoutTeachingGroupDto>> ListEntriesWithoutTeachingGroupAsync(
        int? timetableId = null,
        CancellationToken cancellationToken = default)
    {
        var q =
            from e in _db.SchedulingTimetableEntries.AsNoTracking()
            join t in _db.SchedulingTimetables.AsNoTracking() on e.TimetableId equals t.Id
            where e.TenantId == TenantId && e.TeachingGroupId == null
            select new { Entry = e, Timetable = t };

        if (timetableId is > 0)
            q = q.Where(x => x.Entry.TimetableId == timetableId.Value);

        var rows = await q
            .OrderBy(x => x.Entry.TimetableId)
            .ThenBy(x => x.Entry.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new LegacyTimetableEntryWithoutTeachingGroupDto
        {
            TimetableEntryId = x.Entry.Id,
            TimetableId = x.Entry.TimetableId,
            TimetableStatus = x.Timetable.Status.ToString(),
            TimetableIsFrozen = x.Timetable.IsFrozen,
            SubjectAllocationId = x.Entry.SubjectAllocationId,
            CourseId = x.Entry.CourseId,
            GroupId = x.Entry.GroupId,
            SemesterId = x.Entry.SemesterId,
            SubjectId = x.Entry.SubjectId,
        }).ToList();
    }

    public async Task<LegacyTimetableConversionReportDto> ConvertAsync(
        ConvertLegacyTimetableEntriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dryRun = request.DryRun;
        var results = new List<LegacyTimetableConversionItemResultDto>();

        foreach (var item in request.Items ?? Array.Empty<LegacyTimetableEntryConversionItem>())
        {
            results.Add(await ConvertOneAsync(item, dryRun, cancellationToken));
        }

        return new LegacyTimetableConversionReportDto
        {
            DryRun = dryRun,
            ConvertedCount = results.Count(r => r.Outcome == OutcomeConverted),
            SkippedCount = results.Count(r => r.Outcome == OutcomeSkipped),
            RejectedCount = results.Count(r => r.Outcome == OutcomeRejected),
            Results = results,
        };
    }

    private async Task<LegacyTimetableConversionItemResultDto> ConvertOneAsync(
        LegacyTimetableEntryConversionItem item,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (item.TimetableEntryId <= 0)
            return Reject(item.TimetableEntryId, null, "TimetableEntryId is required.");

        if (item.TeachingGroupId <= 0)
            return Reject(item.TimetableEntryId, null, "TeachingGroupId is required. Teaching Groups are never inferred.");

        var sectionIds = (item.SectionIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();

        var entry = await _db.SchedulingTimetableEntries
            .FirstOrDefaultAsync(e => e.Id == item.TimetableEntryId && e.TenantId == TenantId, cancellationToken);
        if (entry is null)
            return Reject(item.TimetableEntryId, item.TeachingGroupId, "Timetable entry was not found.");

        var timetable = await _db.SchedulingTimetables
            .FirstOrDefaultAsync(t => t.Id == entry.TimetableId && t.TenantId == TenantId, cancellationToken);
        if (timetable is null)
            return Reject(item.TimetableEntryId, item.TeachingGroupId, "Timetable was not found.");

        try
        {
            TimetableService.EnsureDraft(timetable);
        }
        catch (DomainException ex)
        {
            return Reject(item.TimetableEntryId, item.TeachingGroupId, ex.Message);
        }

        var teachingGroup = await _db.SchedulingTeachingGroups
            .FirstOrDefaultAsync(tg => tg.Id == item.TeachingGroupId && tg.TenantId == TenantId, cancellationToken);
        if (teachingGroup is null)
            return Reject(item.TimetableEntryId, item.TeachingGroupId, "TeachingGroup was not found.");

        try
        {
            TeachingGroupRules.EnsureCompatibleWithTimetableEntry(teachingGroup, entry);
        }
        catch (DomainException ex)
        {
            return Reject(item.TimetableEntryId, item.TeachingGroupId, ex.Message);
        }

        if (entry.TeachingGroupId is int existingTg && existingTg != item.TeachingGroupId)
        {
            return Reject(
                item.TimetableEntryId,
                item.TeachingGroupId,
                $"Entry is already assigned to TeachingGroupId {existingTg}. Clear or remap explicitly before conversion.");
        }

        // Validate section type rules + academic scope without inferring sections.
        try
        {
            TeachingGroupRules.ValidateSectionLinks(teachingGroup.Type, sectionIds);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(item.TimetableEntryId, item.TeachingGroupId, ex.Message);
        }

        if (sectionIds.Count > 0)
        {
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id) && s.TenantId == TenantId)
                .ToListAsync(cancellationToken);
            if (sections.Count != sectionIds.Count)
                return Reject(item.TimetableEntryId, item.TeachingGroupId, "One or more Sections were not found.");

            foreach (var section in sections)
            {
                try
                {
                    TeachingGroupRules.EnsureSectionCompatibleWithTeachingGroup(
                        teachingGroup,
                        section.TenantId,
                        section.AcademicYearId,
                        section.CourseId,
                        section.GroupId,
                        section.SemesterId);
                }
                catch (DomainException ex)
                {
                    return Reject(item.TimetableEntryId, item.TeachingGroupId, ex.Message);
                }
            }
        }

        var currentSot = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TeachingGroupId == teachingGroup.Id)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);
        var sotMatches = currentSot.OrderBy(x => x).SequenceEqual(sectionIds.OrderBy(x => x));

        var projectionIds = await _db.TimetableSections.AsNoTracking()
            .Where(x => x.TenantId == TenantId
                        && x.TimetableEntryId == entry.Id
                        && x.TimetableId == entry.TimetableId)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);
        var projectionMatches = projectionIds.OrderBy(x => x).SequenceEqual(sectionIds.OrderBy(x => x));

        if (entry.TeachingGroupId == teachingGroup.Id && sotMatches && projectionMatches)
        {
            return new LegacyTimetableConversionItemResultDto
            {
                TimetableEntryId = entry.Id,
                TeachingGroupId = teachingGroup.Id,
                Outcome = OutcomeSkipped,
                Reason = dryRun
                    ? "Already converted (dry-run)."
                    : "Already converted; idempotent skip.",
            };
        }

        if (dryRun)
        {
            return new LegacyTimetableConversionItemResultDto
            {
                TimetableEntryId = entry.Id,
                TeachingGroupId = teachingGroup.Id,
                Outcome = OutcomeConverted,
                Reason = entry.TeachingGroupId is null
                    ? "Would assign TeachingGroup, replace TeachingGroupSection, and project TimetableSection."
                    : "Would replace TeachingGroupSection and project TimetableSection.",
            };
        }

        try
        {
            // Persist TeachingGroupId first so projection queries include this entry.
            entry.TeachingGroupId = teachingGroup.Id;
            entry.UpdatedDate = DateTime.UtcNow;
            entry.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

            // TeachingGroupSection + TimetableSection only via approved boundary.
            await _teachingGroupSections.ReplaceSectionsAndProjectAsync(
                teachingGroup.Id,
                sectionIds,
                cancellationToken);
        }
        catch (Exception ex) when (ex is DomainException or KeyNotFoundException or InvalidOperationException)
        {
            // Best-effort undo of TeachingGroupId if section/projection stage failed.
            if (entry.TeachingGroupId == teachingGroup.Id)
            {
                entry.TeachingGroupId = null;
                entry.UpdatedDate = DateTime.UtcNow;
                try
                {
                    await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
                }
                catch
                {
                    // Keep the original failure reason for the report.
                }
            }

            return Reject(entry.Id, teachingGroup.Id, ex.Message);
        }

        return new LegacyTimetableConversionItemResultDto
        {
            TimetableEntryId = entry.Id,
            TeachingGroupId = teachingGroup.Id,
            Outcome = OutcomeConverted,
            Reason = "Assigned TeachingGroup, updated TeachingGroupSection, projected TimetableSection.",
        };
    }

    private static LegacyTimetableConversionItemResultDto Reject(int entryId, int? tgId, string reason) => new()
    {
        TimetableEntryId = entryId,
        TeachingGroupId = tgId,
        Outcome = OutcomeRejected,
        Reason = reason,
    };
}
