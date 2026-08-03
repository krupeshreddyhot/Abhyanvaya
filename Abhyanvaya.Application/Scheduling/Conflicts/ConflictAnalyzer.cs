using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

/// <summary>Loads analysis context and runs the <see cref="ConflictEngine"/> pipeline.</summary>
public sealed class ConflictAnalyzer
{
    private readonly IApplicationDbContext _context;
    private readonly ConflictEngine _engine;
    private readonly IConflictRuleConfigurationService _ruleConfiguration;

    public ConflictAnalyzer(
        IApplicationDbContext context,
        ConflictEngine engine,
        IConflictRuleConfigurationService ruleConfiguration)
    {
        _context = context;
        _engine = engine;
        _ruleConfiguration = ruleConfiguration;
    }

    public async Task<(ConflictAnalysisContext Context, ConflictResultBag Bag)> AnalyzeAsync(
        int tenantId,
        int academicYearId,
        int? timetableId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var entriesQuery = _context.SchedulingTimetableEntries.Where(e => e.TenantId == tenantId);
        if (timetableId.HasValue)
            entriesQuery = entriesQuery.Where(e => e.TimetableId == timetableId.Value);
        else
        {
            var timetableIds = _context.SchedulingTimetables
                .Where(t => t.TenantId == tenantId && t.AcademicYearId == academicYearId)
                .Where(t => !departmentId.HasValue || t.DepartmentId == departmentId)
                .Select(t => t.Id);
            entriesQuery = entriesQuery.Where(e => timetableIds.Contains(e.TimetableId));
        }

        if (departmentId.HasValue)
            entriesQuery = entriesQuery.Where(e => e.DepartmentId == departmentId.Value);

        var entries = await entriesQuery.AsNoTracking().ToListAsync(cancellationToken);
        var slotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();
        var staffIds = entries.Select(e => e.StaffId).Distinct().ToList();
        var allocationIds = entries.Select(e => e.SubjectAllocationId).Distinct().ToList();
        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();

        var slots = await _context.SchedulingTimeSlots.Where(s => slotIds.Contains(s.Id)).AsNoTracking()
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var rooms = await _context.SchedulingRooms.Where(r => roomIds.Contains(r.Id)).AsNoTracking()
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        var floorIds = rooms.Values.Select(r => r.FloorId).Distinct().ToList();
        var floors = await _context.SchedulingFloors.Where(f => floorIds.Contains(f.Id)).AsNoTracking()
            .ToDictionaryAsync(f => f.Id, cancellationToken);
        var buildingIds = floors.Values.Select(f => f.BuildingId).Distinct().ToList();
        var buildings = await _context.SchedulingBuildings.Where(b => buildingIds.Contains(b.Id)).AsNoTracking()
            .ToDictionaryAsync(b => b.Id, cancellationToken);
        var campusIds = buildings.Values.Select(b => b.CampusId).Distinct().ToList();
        var campuses = await _context.SchedulingCampuses.Where(c => campusIds.Contains(c.Id)).AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var allocations = await _context.SchedulingSubjectAllocations.Where(a => allocationIds.Contains(a.Id)).AsNoTracking()
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var subjects = await _context.Subjects.Where(s => subjectIds.Contains(s.Id)).AsNoTracking()
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var staffNames = await _context.StaffMembers.Where(s => staffIds.Contains(s.Id)).AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), cancellationToken);

        var facultyAvail = await _context.SchedulingFacultyAvailabilities
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && staffIds.Contains(x.StaffId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var roomAvail = await _context.SchedulingRoomAvailabilities
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && roomIds.Contains(x.RoomId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var prefs = await _context.SchedulingFacultyTeachingPreferences
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.IsActive && staffIds.Contains(x.StaffId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var workingDays = await _context.SchedulingWorkingDays
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)
            .AsNoTracking().ToDictionaryAsync(x => x.DayOfWeek, cancellationToken);
        var holidays = await _context.SchedulingHolidays
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)
            .AsNoTracking().ToListAsync(cancellationToken);
        var academicYear = await _context.SchedulingAcademicYears
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == academicYearId && x.TenantId == tenantId, cancellationToken);
        var featureAssignments = await _context.SchedulingRoomFeatureAssignments
            .Where(x => roomIds.Contains(x.RoomId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var deliveryTypes = await _context.SchedulingSubjectDeliveryTypes.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var thresholds = await _ruleConfiguration.GetThresholdsAsync(tenantId, cancellationToken);

        var ctx = new ConflictAnalysisContext
        {
            TenantId = tenantId,
            AcademicYearId = academicYearId,
            DepartmentId = departmentId,
            TimetableId = timetableId,
            Thresholds = thresholds,
            Entries = entries,
            TimeSlots = slots,
            Rooms = rooms,
            Floors = floors,
            Buildings = buildings,
            Campuses = campuses,
            Allocations = allocations,
            Subjects = subjects,
            FacultyAvailabilities = facultyAvail,
            RoomAvailabilities = roomAvail,
            FacultyPreferences = prefs,
            WorkingDays = workingDays,
            Holidays = holidays,
            AcademicYear = academicYear,
            StaffNames = staffNames,
            RoomFeatureAssignments = featureAssignments,
            DeliveryTypes = deliveryTypes
        };

        var bag = await _engine.ExecuteAsync(ctx, cancellationToken);
        return (ctx, bag);
    }
}
