namespace Abhyanvaya.Application.Academic.Observability;

public enum AcademicHealthLevel
{
    Healthy = 0,
    Warning = 1,
    Critical = 2,
}

public sealed class AcademicComponentHealth
{
    public string Component { get; init; } = "";
    public AcademicHealthLevel Level { get; init; }
    public string Message { get; init; } = "";
    public double? DurationMs { get; init; }
}

public sealed class AcademicHealthReport
{
    public AcademicHealthLevel Overall { get; init; }
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<AcademicComponentHealth> Components { get; init; } = [];
}

public sealed class AcademicCacheMetricsDto
{
    public long HierarchyHits { get; init; }
    public long HierarchyMisses { get; init; }
    public double HierarchyHitRatePercent { get; init; }
    public long StatisticsHits { get; init; }
    public long StatisticsMisses { get; init; }
    public double StatisticsHitRatePercent { get; init; }
    public long RefreshCount { get; init; }
    public long WarmCount { get; init; }
    public long InvalidateCount { get; init; }
    public double AverageHierarchyRetrievalMs { get; init; }
    public double AverageStatisticsRetrievalMs { get; init; }
}

public sealed class AcademicOperationMetricsDto
{
    public string Operation { get; init; } = "";
    public long ExecutionCount { get; init; }
    public long FailureCount { get; init; }
    public double AverageMs { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double BudgetMs { get; init; }
    public bool WithinBudget { get; init; }
}

public sealed class AcademicDomainEventMetricsDto
{
    public string EventName { get; init; } = "";
    public long Published { get; init; }
    public long Succeeded { get; init; }
    public long Failed { get; init; }
    public double AverageProcessingMs { get; init; }
}

public sealed class AcademicPerformanceReportDto
{
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<AcademicOperationMetricsDto> Operations { get; init; } = [];
    public bool AllWithinBudget { get; init; }
}

public sealed class ArchitectureTrendReportDto
{
    public DateTime GeneratedUtc { get; init; }
    public int LatestScore { get; init; }
    public int LatestViolationCount { get; init; }
    public IReadOnlyList<ArchitectureTrendPointDto> History { get; init; } = [];
}

public sealed class ArchitectureTrendPointDto
{
    public DateTime RecordedUtc { get; init; }
    public int Score { get; init; }
    public int DependencyViolations { get; init; }
    public int ForbiddenReferences { get; init; }
    public int LayerViolations { get; init; }
    public string? Summary { get; init; }
}

public sealed class AcademicPlatformMetricsDto
{
    public DateTime GeneratedUtc { get; init; }
    public double CacheHitPercent { get; init; }
    public double AverageTreeBuildMs { get; init; }
    public double AverageSearchMs { get; init; }
    public double AverageBreadcrumbMs { get; init; }
    public int ArchitectureScore { get; init; }
    public int HierarchySize { get; init; }
    public int StatisticsCacheSize { get; init; }
    public IReadOnlyList<AcademicDomainEventMetricsDto> DomainEvents { get; init; } = [];
    public AcademicHealthReport Health { get; init; } = new();
    public AcademicCacheMetricsDto Cache { get; init; } = new();
    public AcademicPerformanceReportDto Performance { get; init; } = new();
}
