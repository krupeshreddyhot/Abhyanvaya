using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class SchedulingDashboardService : ISchedulingDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SchedulingDashboardService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SchedulingDashboardDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rooms = _context.SchedulingRooms.Where(x => x.TenantId == tenantId);
        var allocations = _context.SchedulingSubjectAllocations.Where(x => x.TenantId == tenantId);
        var departments = _context.Departments.Where(x => x.TenantId == tenantId);
        var facultyAvailability = _context.SchedulingFacultyAvailabilities.Where(x => x.TenantId == tenantId);
        var roomAvailability = _context.SchedulingRoomAvailabilities.Where(x => x.TenantId == tenantId);
        var templates = _context.SchedulingTimeSlotTemplates.Where(x => x.TenantId == tenantId);

        var allocatedDepartmentIds = allocations.Select(x => x.DepartmentId).Distinct();
        var departmentsWithoutAllocation = await departments
            .Where(d => d.IsActive && !allocatedDepartmentIds.Contains(d.Id))
            .CountAsync(cancellationToken);

        var facultyUnavailable = await facultyAvailability.CountAsync(x =>
            x.AvailabilityType == FacultyAvailabilityType.Unavailable
            && x.StartDate <= today
            && x.EndDate >= today, cancellationToken);

        var roomsBlocked = await roomAvailability.CountAsync(x =>
            (x.AvailabilityType == RoomAvailabilityType.Blocked || x.AvailabilityType == RoomAvailabilityType.Maintenance)
            && x.StartDate <= today
            && x.EndDate >= today, cancellationToken);

        var subjectsMissingCategory = await _context.Subjects
            .CountAsync(x => x.TenantId == tenantId && x.SubjectCategoryId == null, cancellationToken);

        var unusedTemplates = await templates
            .CountAsync(t => !t.IsDeleted && !_context.SchedulingTimeSlotSets.Any(s => s.TimeSlotTemplateId == t.Id && s.TenantId == tenantId), cancellationToken);

        var currentYearId = await _context.SchedulingAcademicYears
            .Where(x => x.TenantId == tenantId && x.IsCurrent)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var missingFacultyPreferences = 0;
        if (currentYearId.HasValue)
        {
            var allocatedStaffIds = allocations
                .Where(x => x.AcademicYearId == currentYearId.Value)
                .Select(x => x.StaffId)
                .Distinct();
            var staffWithPrefs = _context.SchedulingFacultyTeachingPreferences
                .Where(x => x.TenantId == tenantId && x.AcademicYearId == currentYearId.Value && x.IsActive && !x.IsDeleted)
                .Select(x => x.StaffId)
                .Distinct();
            missingFacultyPreferences = await allocatedStaffIds
                .Where(id => !staffWithPrefs.Contains(id))
                .CountAsync(cancellationToken);
        }

        var roomCount = await rooms.CountAsync(cancellationToken);
        var roomsWithFeatures = await _context.SchedulingRoomFeatureAssignments
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => x.RoomId)
            .Distinct()
            .CountAsync(cancellationToken);
        var roomsWithoutFeatures = roomCount - roomsWithFeatures;
        var roomFeatureCoverage = roomCount > 0 ? Math.Round(roomsWithFeatures * 100m / roomCount, 2) : 0m;

        var holidayDistribution = await _context.SchedulingHolidays
            .Where(x => x.TenantId == tenantId && x.HolidayTypeCatalogId != null)
            .Join(
                _context.SchedulingHolidayTypeCatalogs.Where(c => c.TenantId == tenantId),
                h => h.HolidayTypeCatalogId,
                c => c.Id,
                (_, c) => c.Name)
            .GroupBy(name => name)
            .Select(g => new NamedCountDto { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var deliveryTypeDistribution = await _context.Subjects
            .Where(x => x.TenantId == tenantId && x.DeliveryTypeId != null)
            .Join(
                _context.SchedulingSubjectDeliveryTypes.Where(d => d.TenantId == tenantId),
                s => s.DeliveryTypeId,
                d => d.Id,
                (_, d) => d.Name)
            .GroupBy(name => name)
            .Select(g => new NamedCountDto { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new SchedulingDashboardDto
        {
            AcademicYearCount = await _context.SchedulingAcademicYears.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            CampusCount = await _context.SchedulingCampuses.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            BuildingCount = await _context.SchedulingBuildings.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            RoomCount = roomCount,
            SubjectCount = await _context.Subjects.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            FacultyCount = await _context.StaffMembers.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            TotalWeeklyHours = await allocations.SumAsync(x => (decimal?)x.WeeklyHours, cancellationToken) ?? 0m,
            TotalRoomCapacity = await rooms.SumAsync(x => (int?)x.Capacity, cancellationToken) ?? 0,
            TimeSlotSetCount = await _context.SchedulingTimeSlotSets.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            FacultyWorkloadCount = await _context.SchedulingFacultyWorkloads.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            SubjectAllocationCount = await allocations.CountAsync(cancellationToken),
            RoomRuleCount = await _context.SchedulingRoomAllocationRules.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            HolidayCount = await _context.SchedulingHolidays.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            DepartmentCount = await departments.CountAsync(cancellationToken),
            FacultyAvailabilityCount = await facultyAvailability.CountAsync(cancellationToken),
            RoomAvailabilityCount = await roomAvailability.CountAsync(cancellationToken),
            SubjectCategoryCount = await _context.SchedulingSubjectCategories.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            TimeSlotTemplateCount = await templates.CountAsync(cancellationToken),
            FacultyUnavailableCount = facultyUnavailable,
            RoomsBlockedCount = roomsBlocked,
            SubjectsMissingCategoryCount = subjectsMissingCategory,
            UnusedTemplateCount = unusedTemplates,
            DepartmentsWithoutAllocationCount = departmentsWithoutAllocation,
            FacultyPreferenceCount = await _context.SchedulingFacultyTeachingPreferences.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            RoomFeatureCount = await _context.SchedulingRoomFeatures.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            RoomFeatureAssignmentCount = await _context.SchedulingRoomFeatureAssignments.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            SubjectDeliveryTypeCount = await _context.SchedulingSubjectDeliveryTypes.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            HolidayTypeCatalogCount = await _context.SchedulingHolidayTypeCatalogs.CountAsync(x => x.TenantId == tenantId, cancellationToken),
            MissingFacultyPreferencesCount = missingFacultyPreferences,
            RoomsWithFeaturesCount = roomsWithFeatures,
            RoomsWithoutFeaturesCount = roomsWithoutFeatures,
            RoomFeatureCoveragePercent = roomFeatureCoverage,
            HolidayDistribution = holidayDistribution,
            DeliveryTypeDistribution = deliveryTypeDistribution,
        };
    }
}
