using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Enums.Scheduling;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableSoftValidationService : ITimetableSoftValidationService
{
    private static readonly RoomType[] LabRoomTypes = [RoomType.ComputerLab, RoomType.ScienceLab, RoomType.CommerceLab];

    private readonly ITimetableRepository _timetableRepository;
    private readonly ITimetableWarningDismissalRepository _dismissalRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<DismissSoftWarningRequest> _dismissValidator;

    public TimetableSoftValidationService(
        ITimetableRepository timetableRepository,
        ITimetableWarningDismissalRepository dismissalRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<DismissSoftWarningRequest> dismissValidator)
    {
        _timetableRepository = timetableRepository;
        _dismissalRepository = dismissalRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _dismissValidator = dismissValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<SoftWarningDto>> ValidateAsync(int timetableId, CancellationToken cancellationToken = default)
    {
        var timetable = await _timetableRepository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        var entries = await _timetableRepository.ListEntriesAsync(TenantId, timetableId, cancellationToken);
        var dismissals = await _dismissalRepository.ListForTimetableAsync(TenantId, timetableId, cancellationToken);

        var warnings = new List<SoftWarningDto>();
        var staffIds = entries.Select(e => e.StaffId).Distinct().ToList();
        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();
        var allocationIds = entries.Select(e => e.SubjectAllocationId).Distinct().ToList();

        var facultyAvailabilities = await _context.SchedulingFacultyAvailabilities
            .Where(x => x.TenantId == TenantId && x.AcademicYearId == timetable.AcademicYearId && staffIds.Contains(x.StaffId))
            .Where(x => x.AvailabilityType == FacultyAvailabilityType.Unavailable || x.AvailabilityType == FacultyAvailabilityType.ApprovedLeave)
            .ToListAsync(cancellationToken);

        var roomAvailabilities = await _context.SchedulingRoomAvailabilities
            .Where(x => x.TenantId == TenantId && x.AcademicYearId == timetable.AcademicYearId && roomIds.Contains(x.RoomId))
            .Where(x => x.AvailabilityType == RoomAvailabilityType.Blocked || x.AvailabilityType == RoomAvailabilityType.Maintenance)
            .ToListAsync(cancellationToken);

        var rooms = await _context.SchedulingRooms.Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var allocations = await _context.SchedulingSubjectAllocations.Where(a => allocationIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var subjects = await _context.Subjects.Where(s => entries.Select(e => e.SubjectId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        var workingDays = await _context.SchedulingWorkingDays
            .Where(x => x.TenantId == TenantId && x.AcademicYearId == timetable.AcademicYearId)
            .ToDictionaryAsync(x => x.DayOfWeek, cancellationToken);
        var reschedulingHolidayCount = await _context.SchedulingHolidays
            .CountAsync(x => x.TenantId == TenantId && x.AcademicYearId == timetable.AcademicYearId && x.RequiresRescheduling, cancellationToken);

        foreach (var entry in entries)
        {
            if (facultyAvailabilities.Any(a => a.StaffId == entry.StaffId))
            {
                warnings.Add(Warn("FACULTY_UNAVAILABLE", "Faculty has unavailable/leave records in this academic year.", entry, dismissals));
            }

            if (roomAvailabilities.Any(a => a.RoomId == entry.RoomId))
            {
                warnings.Add(Warn("ROOM_UNAVAILABLE", "Room has blocked/maintenance availability in this academic year.", entry, dismissals));
            }

            if (rooms.TryGetValue(entry.RoomId, out var room) && allocations.TryGetValue(entry.SubjectAllocationId, out var allocation))
            {
                if (subjects.TryGetValue(entry.SubjectId, out var subject) && subject.ExpectedCapacity.HasValue && room.Capacity < subject.ExpectedCapacity.Value)
                {
                    warnings.Add(Warn("ROOM_CAPACITY", $"Room capacity ({room.Capacity}) is below expected subject capacity ({subject.ExpectedCapacity}).", entry, dismissals));
                }

                if (allocation.PreferredRoomId.HasValue && allocation.PreferredRoomId.Value != entry.RoomId)
                {
                    warnings.Add(Warn("PREFERRED_ROOM_MISSING", "Entry room differs from allocation preferred room.", entry, dismissals));
                }

                if (allocation.LabRequired && !LabRoomTypes.Contains(room.RoomType))
                {
                    warnings.Add(Warn("LAB_RECOMMENDED", "Lab is recommended for this subject but assigned room is not a lab.", entry, dismissals));
                }
            }

            if (workingDays.TryGetValue(entry.DayOfWeek, out var workingDay) && !workingDay.IsWorking)
            {
                warnings.Add(Warn("NON_WORKING_DAY", "Entry is placed on a non-working day.", entry, dismissals));
            }
        }

        if (reschedulingHolidayCount > 0)
        {
            warnings.Add(new SoftWarningDto
            {
                Code = "HOLIDAY_RESCHEDULE",
                Message = "Holiday calendar has RequiresRescheduling holidays in this academic year.",
                Dismissed = dismissals.Any(d => d.WarningCode == "HOLIDAY_RESCHEDULE" && d.EntryId == null)
            });
        }

        var staffDupes = entries.GroupBy(e => new { e.StaffId, e.DayOfWeek, e.TimeSlotId }).Where(g => g.Count() > 1);
        foreach (var group in staffDupes)
        {
            foreach (var entry in group)
                warnings.Add(Warn("DUPLICATE_FACULTY_SESSION", "Duplicate faculty session on same day and time slot.", entry, dismissals));
        }

        var roomDupes = entries.GroupBy(e => new { e.RoomId, e.DayOfWeek, e.TimeSlotId }).Where(g => g.Count() > 1);
        foreach (var group in roomDupes)
        {
            foreach (var entry in group)
                warnings.Add(Warn("DUPLICATE_ROOM_SESSION", "Duplicate room session on same day and time slot.", entry, dismissals));
        }

        return warnings;
    }

    public async Task DismissWarningAsync(int timetableId, DismissSoftWarningRequest request, CancellationToken cancellationToken = default)
    {
        await _dismissValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _dismissalRepository.ExistsAsync(TenantId, timetableId, request.Code, request.EntryId, request.StaffId, request.RoomId, request.DayOfWeek, request.TimeSlotId, cancellationToken))
            return;

        await _dismissalRepository.AddAsync(new Domain.Entities.Scheduling.TimetableWarningDismissal
        {
            TenantId = TenantId,
            TimetableId = timetableId,
            WarningCode = request.Code.Trim(),
            EntryId = request.EntryId,
            StaffId = request.StaffId,
            RoomId = request.RoomId,
            DayOfWeek = request.DayOfWeek,
            TimeSlotId = request.TimeSlotId,
            DismissedBy = _currentUser.UserId,
            DismissedUtc = DateTime.UtcNow
        }, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private static SoftWarningDto Warn(string code, string message, Domain.Entities.Scheduling.TimetableEntry entry, IReadOnlyList<Domain.Entities.Scheduling.TimetableWarningDismissal> dismissals) => new()
    {
        Code = code,
        Message = message,
        EntryId = entry.Id,
        StaffId = entry.StaffId,
        RoomId = entry.RoomId,
        DayOfWeek = entry.DayOfWeek,
        TimeSlotId = entry.TimeSlotId,
        Dismissed = dismissals.Any(d =>
            d.WarningCode == code
            && d.EntryId == entry.Id
            && d.StaffId == entry.StaffId
            && d.RoomId == entry.RoomId
            && d.DayOfWeek == entry.DayOfWeek
            && d.TimeSlotId == entry.TimeSlotId)
    };
}
