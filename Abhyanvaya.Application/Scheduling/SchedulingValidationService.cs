using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class SchedulingValidationService : ISchedulingValidationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SchedulingValidationService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SchedulingValidationReportDto> GetReportAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var currentYearId = await _context.SchedulingAcademicYears
            .Where(x => x.TenantId == tenantId && x.IsCurrent)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var missingFacultyPreferences = 0;
        if (currentYearId.HasValue)
        {
            var allocatedStaffIds = _context.SchedulingSubjectAllocations
                .Where(x => x.TenantId == tenantId && x.AcademicYearId == currentYearId.Value)
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

        var subjectsMissingDeliveryType = await _context.Subjects
            .CountAsync(x => x.TenantId == tenantId && x.DeliveryTypeId == null, cancellationToken);

        var duplicateAssignments = await _context.SchedulingRoomFeatureAssignments
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .GroupBy(x => new { x.RoomId, x.RoomFeatureId })
            .Where(g => g.Count() > 1)
            .CountAsync(cancellationToken);

        var totalRooms = await _context.SchedulingRooms.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var roomsWithFeatures = await _context.SchedulingRoomFeatureAssignments
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => x.RoomId)
            .Distinct()
            .CountAsync(cancellationToken);

        var holidaysMissingCatalog = await _context.SchedulingHolidays
            .CountAsync(x => x.TenantId == tenantId && x.HolidayTypeCatalogId == null, cancellationToken);

        return new SchedulingValidationReportDto
        {
            MissingFacultyPreferencesCount = missingFacultyPreferences,
            SubjectsMissingDeliveryTypeCount = subjectsMissingDeliveryType,
            DuplicateRoomFeatureAssignmentCount = duplicateAssignments,
            RoomsWithoutFeaturesCount = totalRooms - roomsWithFeatures,
            HolidaysMissingCatalogTypeCount = holidaysMissingCatalog,
        };
    }
}
