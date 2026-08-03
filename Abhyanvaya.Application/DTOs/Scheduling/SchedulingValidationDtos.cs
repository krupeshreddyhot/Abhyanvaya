namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class NamedCountDto
{
    public string Name { get; init; } = null!;
    public int Count { get; init; }
}

public sealed class SchedulingValidationReportDto
{
    public int MissingFacultyPreferencesCount { get; init; }
    public int SubjectsMissingDeliveryTypeCount { get; init; }
    public int DuplicateRoomFeatureAssignmentCount { get; init; }
    public int RoomsWithoutFeaturesCount { get; init; }
    public int HolidaysMissingCatalogTypeCount { get; init; }
}
