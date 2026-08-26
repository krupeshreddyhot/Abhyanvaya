using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4 Prompt 3 — Explicit TeachingGroup assignment to TimetableEntry.
/// AI-SCHED-TG.6 Final Gate Prompt 21 — After assign/clear, stage TimetableSection projection via
/// <see cref="ITimetableSectionProjector"/> then <b>SaveChanges once</b>. Projector never commits.
/// Tenant-scoped loads only; never bypasses query filters; no SubjectAllocation inference; no TG create.
/// </summary>
public sealed class TeachingGroupApplicationService : ITeachingGroupApplicationService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimetableSectionProjector _projector;
    private readonly ITimetableChangeHistoryService? _historyService;

    public TeachingGroupApplicationService(
        ITimetableRepository timetableRepository,
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITimetableSectionProjector projector,
        ITimetableChangeHistoryService? historyService = null)
    {
        _timetableRepository = timetableRepository;
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _projector = projector;
        _historyService = historyService;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetableEntryDto> AssignToTimetableEntryAsync(
        int timetableEntryId,
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        if (timetableEntryId <= 0 || teachingGroupId <= 0)
            throw new DomainException("A valid TimetableEntry and TeachingGroup must be specified.");

        var entry = await RequireEntryAsync(timetableEntryId, cancellationToken);
        var timetable = await RequireTimetableAsync(entry.TimetableId, cancellationToken);
        TimetableService.EnsureDraft(timetable);

        var teachingGroup = await LoadTeachingGroupForAssignmentAsync(teachingGroupId, cancellationToken);
        TeachingGroupRules.EnsureCompatibleWithTimetableEntry(teachingGroup, entry);

        var previous = entry.TeachingGroupId;
        entry.TeachingGroupId = teachingGroup.Id;
        entry.UpdatedDate = DateTime.UtcNow;
        entry.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

        // Stage projection from TeachingGroupSection SoT for this entry (projector does not SaveChanges).
        await _projector.SyncTeachingGroupSectionsToTimetableEntryAsync(entry.Id, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        if (_historyService is not null)
        {
            await _historyService.RecordAsync(
                timetable.Id,
                TimetableChangeOperation.Update,
                entry.Id,
                previous is null ? null : new { TeachingGroupId = previous },
                new { TeachingGroupId = entry.TeachingGroupId },
                "Explicit TeachingGroup assignment",
                cancellationToken);
        }

        return MapEntry(entry);
    }

    public async Task<TimetableEntryDto> ClearFromTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default)
    {
        if (timetableEntryId <= 0)
            throw new DomainException("A valid TimetableEntry must be specified.");

        var entry = await RequireEntryAsync(timetableEntryId, cancellationToken);
        var timetable = await RequireTimetableAsync(entry.TimetableId, cancellationToken);
        TimetableService.EnsureDraft(timetable);

        var previous = entry.TeachingGroupId;

        // TG-derived TimetableSection rows are no longer applicable for this entry.
        // Soft-delete projection only — TeachingGroupSection SoT and other entries are untouched.
        if (previous is not null)
            await _projector.ClearTimetableEntryProjectionAsync(entry.Id, cancellationToken);

        entry.TeachingGroupId = null;
        entry.UpdatedDate = DateTime.UtcNow;
        entry.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        if (_historyService is not null && previous is not null)
        {
            await _historyService.RecordAsync(
                timetable.Id,
                TimetableChangeOperation.Update,
                entry.Id,
                new { TeachingGroupId = previous },
                new { TeachingGroupId = (int?)null },
                "Explicit TeachingGroup clear",
                cancellationToken);
        }

        return MapEntry(entry);
    }

    /// <summary>
    /// Tenant-filtered load only. Missing / other-tenant TeachingGroups surface as not found
    /// (no cross-tenant leakage). Never creates a TeachingGroup.
    /// </summary>
    private async Task<TeachingGroup> LoadTeachingGroupForAssignmentAsync(int teachingGroupId, CancellationToken cancellationToken)
    {
        var teachingGroup = await _db.SchedulingTeachingGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken);

        return teachingGroup
            ?? throw new KeyNotFoundException("TeachingGroup was not found or is not available for assignment.");
    }

    private async Task<TimetableEntry> RequireEntryAsync(int entryId, CancellationToken cancellationToken)
    {
        var entry = await _timetableRepository.GetEntryByIdAsync(TenantId, entryId, cancellationToken);
        return entry ?? throw new KeyNotFoundException("Timetable entry was not found.");
    }

    private async Task<Timetable> RequireTimetableAsync(int timetableId, CancellationToken cancellationToken)
    {
        var timetable = await _timetableRepository.GetByIdAsync(TenantId, timetableId, cancellationToken);
        return timetable ?? throw new KeyNotFoundException("Timetable was not found.");
    }

    private static TimetableEntryDto MapEntry(TimetableEntry entry) => new()
    {
        Id = entry.Id,
        TimetableId = entry.TimetableId,
        DayOfWeek = entry.DayOfWeek,
        TimeSlotId = entry.TimeSlotId,
        SubjectAllocationId = entry.SubjectAllocationId,
        TeachingGroupId = entry.TeachingGroupId,
        StaffId = entry.StaffId,
        RoomId = entry.RoomId,
        DepartmentId = entry.DepartmentId,
        CourseId = entry.CourseId,
        GroupId = entry.GroupId,
        SemesterId = entry.SemesterId,
        SubjectId = entry.SubjectId,
        Remarks = entry.Remarks,
    };
}
