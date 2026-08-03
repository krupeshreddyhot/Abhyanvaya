using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class RoomAllocationRuleDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public RoomType? RoomType { get; init; }
    public int? MinCapacity { get; init; }
    public int? MaxCapacity { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public bool RequireComputerLab { get; init; }
    public bool RequireScienceLab { get; init; }
    public bool RequireCommerceLab { get; init; }
    public bool RequireAiCamera { get; init; }
    public bool RequireProjector { get; init; }
    public bool RequireSmartBoard { get; init; }
    public int? PreferredRoomId { get; init; }
    public int Priority { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateRoomAllocationRuleRequest
{
    public string Name { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public RoomType? RoomType { get; init; }
    public int? MinCapacity { get; init; }
    public int? MaxCapacity { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public bool RequireComputerLab { get; init; }
    public bool RequireScienceLab { get; init; }
    public bool RequireCommerceLab { get; init; }
    public bool RequireAiCamera { get; init; }
    public bool RequireProjector { get; init; }
    public bool RequireSmartBoard { get; init; }
    public int? PreferredRoomId { get; init; }
    public int Priority { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateRoomAllocationRuleRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public RoomType? RoomType { get; init; }
    public int? MinCapacity { get; init; }
    public int? MaxCapacity { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public bool RequireComputerLab { get; init; }
    public bool RequireScienceLab { get; init; }
    public bool RequireCommerceLab { get; init; }
    public bool RequireAiCamera { get; init; }
    public bool RequireProjector { get; init; }
    public bool RequireSmartBoard { get; init; }
    public int? PreferredRoomId { get; init; }
    public int Priority { get; init; }
    public string? Notes { get; init; }
}

public sealed class SchedulingDashboardDto
{
    public int AcademicYearCount { get; init; }
    public int CampusCount { get; init; }
    public int BuildingCount { get; init; }
    public int RoomCount { get; init; }
    public int SubjectCount { get; init; }
    public int FacultyCount { get; init; }
    public decimal TotalWeeklyHours { get; init; }
    public int TotalRoomCapacity { get; init; }
    public int TimeSlotSetCount { get; init; }
    public int FacultyWorkloadCount { get; init; }
    public int SubjectAllocationCount { get; init; }
    public int RoomRuleCount { get; init; }
    public int HolidayCount { get; init; }
    public int DepartmentCount { get; init; }
    public int FacultyAvailabilityCount { get; init; }
    public int RoomAvailabilityCount { get; init; }
    public int SubjectCategoryCount { get; init; }
    public int TimeSlotTemplateCount { get; init; }
    public int FacultyUnavailableCount { get; init; }
    public int RoomsBlockedCount { get; init; }
    public int SubjectsMissingCategoryCount { get; init; }
    public int UnusedTemplateCount { get; init; }
    public int DepartmentsWithoutAllocationCount { get; init; }
    public int FacultyPreferenceCount { get; init; }
    public int RoomFeatureCount { get; init; }
    public int RoomFeatureAssignmentCount { get; init; }
    public int SubjectDeliveryTypeCount { get; init; }
    public int HolidayTypeCatalogCount { get; init; }
    public int MissingFacultyPreferencesCount { get; init; }
    public int RoomsWithFeaturesCount { get; init; }
    public int RoomsWithoutFeaturesCount { get; init; }
    public decimal RoomFeatureCoveragePercent { get; init; }
    public IReadOnlyList<NamedCountDto> HolidayDistribution { get; init; } = [];
    public IReadOnlyList<NamedCountDto> DeliveryTypeDistribution { get; init; } = [];
}
