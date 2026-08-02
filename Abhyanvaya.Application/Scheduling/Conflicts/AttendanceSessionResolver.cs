using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

/// <summary>
/// Resolves attendance session context from timetable when available; otherwise Legacy mode.
/// Does not modify attendance APIs or force timetable usage.
/// </summary>
public sealed class AttendanceSessionResolver : IAttendanceSessionResolver
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AttendanceSessionResolver(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AttendanceSessionResolutionDto> ResolveAsync(int? staffId, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var attendanceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var resolvedStaffId = staffId
            ?? await ResolveStaffIdFromUserAsync(cancellationToken);

        if (!resolvedStaffId.HasValue)
        {
            return Legacy("No staff identity available; use Course → Group → Semester → Subject → Period.");
        }

        var dayOfWeek = (byte)attendanceDate.DayOfWeek;
        var now = DateTime.UtcNow.TimeOfDay;

        var yearId = await _context.SchedulingAcademicYears.AsNoTracking()
            .Where(y => y.TenantId == _currentUser.TenantId && y.IsCurrent)
            .Select(y => (int?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!yearId.HasValue)
        {
            return Legacy("No current academic year; use legacy attendance workflow.", attendanceDate);
        }

        var timetableIds = await _context.SchedulingTimetables.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.AcademicYearId == yearId.Value)
            .Where(t => t.Status == TimetableStatus.Published || t.Status == TimetableStatus.Locked)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (timetableIds.Count == 0)
        {
            return Legacy("Faculty has no published/locked timetable; use legacy attendance workflow.", attendanceDate);
        }

        var entries = await _context.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == _currentUser.TenantId
                        && timetableIds.Contains(e.TimetableId)
                        && e.StaffId == resolvedStaffId.Value
                        && e.DayOfWeek == dayOfWeek)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return Legacy("No timetable entries for this faculty today; use legacy attendance workflow.", attendanceDate);
        }

        var slotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var slots = await _context.SchedulingTimeSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var current = entries
            .Select(e => (Entry: e, Slot: slots.GetValueOrDefault(e.TimeSlotId)))
            .Where(x => x.Slot is not null && x.Slot.SlotKind == SlotKind.Period)
            .Where(x => x.Slot!.StartTime <= now && now < x.Slot.EndTime)
            .OrderBy(x => x.Slot!.StartTime)
            .FirstOrDefault();

        // If outside a period, pick the next upcoming period today
        if (current.Entry is null)
        {
            current = entries
                .Select(e => (Entry: e, Slot: slots.GetValueOrDefault(e.TimeSlotId)))
                .Where(x => x.Slot is not null && x.Slot.SlotKind == SlotKind.Period && x.Slot.StartTime >= now)
                .OrderBy(x => x.Slot!.StartTime)
                .FirstOrDefault();
        }

        if (current.Entry is null)
        {
            return Legacy("No current/upcoming timetable period for faculty today; use legacy workflow.", attendanceDate);
        }

        var entry = current.Entry;
        var slot = current.Slot!;
        var roomName = await _context.SchedulingRooms.AsNoTracking()
            .Where(r => r.Id == entry.RoomId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var subjectName = await (
            from s in _context.Subjects.AsNoTracking()
            join ts in _context.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where s.Id == entry.SubjectId
            select ts.Name).FirstOrDefaultAsync(cancellationToken);

        return new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            Message = "Timetable mode: today's class context resolved from published timetable.",
            TimetableId = entry.TimetableId,
            TimetableEntryId = entry.Id,
            CourseId = entry.CourseId,
            GroupId = entry.GroupId,
            SemesterId = entry.SemesterId,
            SubjectId = entry.SubjectId,
            PeriodNumber = slot.PeriodNumber,
            TimeSlotId = slot.Id,
            RoomId = entry.RoomId,
            SubjectName = subjectName,
            RoomName = roomName,
            AttendanceDate = attendanceDate
        };
    }

    private Task<int?> ResolveStaffIdFromUserAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_currentUser.StaffId > 0 ? (int?)_currentUser.StaffId : null);
    }

    private static AttendanceSessionResolutionDto Legacy(string message, DateOnly? date = null) => new()
    {
        Mode = "Legacy",
        HasTimetable = false,
        Message = message,
        AttendanceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow)
    };
}
