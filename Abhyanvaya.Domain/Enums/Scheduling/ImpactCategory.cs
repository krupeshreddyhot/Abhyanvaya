namespace Abhyanvaya.Domain.Enums.Scheduling;

public enum ImpactCategory : byte
{
    Faculty = 1,
    Students = 2,
    Room = 3,
    Department = 4,
    PublishedVersion = 5,
    Workload = 6,
    Availability = 7,
    Attendance = 8,
    Other = 99,
}

public enum ResolutionDifficulty : byte
{
    Easy = 1,
    Moderate = 2,
    Hard = 3,
}

public enum ResolutionImpactLevel : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
}
