namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionCapacitySnapshotDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string LifecycleStatus { get; init; } = "";
    public string SectionTypeCode { get; init; } = "";

    public int MaximumCapacity { get; init; }
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int CurrentStrength { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingList { get; init; }
    public int AvailableSeats { get; init; }
    public double OccupancyPercent { get; init; }
    public string CapacityStatus { get; init; } = "Ok";

    public bool IsOverCapacity { get; init; }
    public bool IsUnderCapacity { get; init; }
    public bool IsHardLimitBreached { get; init; }
    public bool HasWarning { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class SectionCapacitySummaryDto
{
    public int SectionCount { get; init; }
    public int TotalMaximumCapacity { get; init; }
    public int TotalCurrentStrength { get; init; }
    public int TotalAvailableSeats { get; init; }
    public int OverCapacityCount { get; init; }
    public int UnderCapacityCount { get; init; }
    public int WarningCount { get; init; }
    public double AverageOccupancyPercent { get; init; }
}

public sealed class UpdateSectionCapacityRequest
{
    public int MaximumCapacity { get; init; }
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingListCount { get; init; }
}

public sealed class TenantSectionCapacityPolicyDto
{
    public int Id { get; init; }
    public bool EnforceHardLimit { get; init; }
    public bool SoftLimitEnabled { get; init; }
    public int WarningPercent { get; init; }
    public bool AutoWarningEnabled { get; init; }
    public int UnderCapacityPercent { get; init; }
}

public sealed class UpsertTenantSectionCapacityPolicyRequest
{
    public bool EnforceHardLimit { get; init; } = true;
    public bool SoftLimitEnabled { get; init; } = true;
    public int WarningPercent { get; init; } = 90;
    public bool AutoWarningEnabled { get; init; } = true;
    public int UnderCapacityPercent { get; init; } = 40;
}

public sealed class SectionCapacityAnalyticsDto
{
    public double AverageOccupancyPercent { get; init; }
    public double UtilizationPercent { get; init; }
    public int SectionGrowthCount { get; init; }
    public IReadOnlyList<SectionCapacitySnapshotDto> MergeCandidates { get; init; } = [];
    public IReadOnlyList<SectionCapacitySnapshotDto> SplitCandidates { get; init; } = [];
    public IReadOnlyList<SectionCapacityTrendPointDto> CapacityTrend { get; init; } = [];
}

public sealed class SectionCapacityTrendPointDto
{
    public DateOnly Date { get; init; }
    public double AverageOccupancyPercent { get; init; }
    public int TotalCurrentStrength { get; init; }
}
