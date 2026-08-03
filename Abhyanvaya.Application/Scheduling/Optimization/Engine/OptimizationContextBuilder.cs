using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Engine;

public interface IOptimizationContextBuilder
{
    Task<OptimizationContext> BuildAsync(
        int academicYearId,
        int? timetableId,
        int? departmentId,
        CancellationToken cancellationToken = default);
}

public sealed class OptimizationContextBuilder : IOptimizationContextBuilder
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IConflictDetectionService _conflicts;

    public OptimizationContextBuilder(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IConflictDetectionService conflicts)
    {
        _db = db;
        _currentUser = currentUser;
        _conflicts = conflicts;
    }

    public async Task<OptimizationContext> BuildAsync(
        int academicYearId,
        int? timetableId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var entryQuery = _db.SchedulingTimetableEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted);

        if (timetableId.HasValue)
            entryQuery = entryQuery.Where(e => e.TimetableId == timetableId.Value);
        else
        {
            var timetableIds = await _db.SchedulingTimetables.AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.AcademicYearId == academicYearId && !t.IsDeleted)
                .Where(t => !departmentId.HasValue || t.DepartmentId == departmentId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
            entryQuery = entryQuery.Where(e => timetableIds.Contains(e.TimetableId));
        }

        if (departmentId.HasValue)
            entryQuery = entryQuery.Where(e => e.DepartmentId == departmentId.Value);

        var entries = await entryQuery
            .Select(e => new OptimizationEntrySnapshot
            {
                EntryId = e.Id,
                TimetableId = e.TimetableId,
                DayOfWeek = e.DayOfWeek,
                TimeSlotId = e.TimeSlotId,
                StaffId = e.StaffId,
                RoomId = e.RoomId,
                DepartmentId = e.DepartmentId,
                SubjectId = e.SubjectId,
                GroupId = e.GroupId,
                SubjectAllocationId = e.SubjectAllocationId
            })
            .ToListAsync(cancellationToken);

        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();
        var rooms = await (
            from r in _db.SchedulingRooms.AsNoTracking()
            join f in _db.SchedulingFloors.AsNoTracking() on r.FloorId equals f.Id into fj
            from f in fj.DefaultIfEmpty()
            where r.TenantId == tenantId && roomIds.Contains(r.Id) && !r.IsDeleted
            select new OptimizationRoomSnapshot
            {
                RoomId = r.Id,
                Name = r.Name,
                Capacity = r.Capacity,
                BuildingId = f != null ? f.BuildingId : null
            }).ToDictionaryAsync(r => r.RoomId, cancellationToken);

        var slotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var slots = await _db.SchedulingTimeSlots.AsNoTracking()
            .Where(s => s.TenantId == tenantId && slotIds.Contains(s.Id) && !s.IsDeleted)
            .Select(s => new OptimizationSlotSnapshot
            {
                TimeSlotId = s.Id,
                Name = s.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsPeriod = true
            })
            .ToDictionaryAsync(s => s.TimeSlotId, cancellationToken);

        // Preferred room heuristic: most frequent room per staff in current timetable
        var preferred = entries
            .GroupBy(e => e.StaffId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.RoomId).OrderByDescending(x => x.Count()).Select(x => x.Key).First());

        var workspace = await _conflicts.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = academicYearId,
            TimetableId = timetableId,
            DepartmentId = departmentId,
            UseLatestRun = true
        }, cancellationToken);

        var conflictCount = workspace.Summary.TotalConflicts;
        if (conflictCount == 0)
            conflictCount = OptimizationWorkingSet.CountHardConflicts(entries);

        var metrics = OptimizationWorkingSet.BuildMetrics(entries, rooms, slots, preferred, conflictCount);

        return new OptimizationContext
        {
            TenantId = tenantId,
            AcademicYearId = academicYearId,
            TimetableId = timetableId,
            DepartmentId = departmentId,
            EntryCount = entries.Count,
            ConflictCount = conflictCount,
            BaselineMetrics = metrics,
            WorkingEntries = entries,
            Rooms = rooms,
            TimeSlots = slots,
            FacultyPreferredRoomIds = preferred,
            SubjectExpectedCapacities = new Dictionary<int, int>()
        };
    }
}
