namespace Abhyanvaya.Application.TenantContext;

public sealed record RecentCollegeEntry
{
    public required int CollegeId { get; init; }
    public required int TenantId { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required DateTime SelectedUtc { get; init; }
    public bool IsPinned { get; init; }
    public bool IsFavorite { get; init; }
}

public sealed record RecentCollegesResult
{
    public required IReadOnlyList<RecentCollegeEntry> Recent { get; init; }
    public required IReadOnlyList<AvailableCollegeDto> Popular { get; init; }
}

public sealed record ContextDiagnosticsReport
{
    public required int UserId { get; init; }
    public required string Role { get; init; }
    public required int JwtTenantId { get; init; }
    public TenantContextSnapshot? OperationalContext { get; init; }
    public required string PersistenceProvider { get; init; }
    public required bool ContextExists { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    public TimeSpan? RemainingTime { get; init; }
    public required bool IsExpired { get; init; }
    public required bool IsValid { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();
}

public sealed record ContextOperationalMetricsSnapshot
{
    public long ContextSwitchCount { get; init; }
    public long ExpiredContextCount { get; init; }
    public long FailedValidationCount { get; init; }
    public double AverageContextDurationMinutes { get; init; }
    public IReadOnlyList<CollegeUsageMetric> MostUsedColleges { get; init; } = Array.Empty<CollegeUsageMetric>();
}

public sealed record CollegeUsageMetric
{
    public required int CollegeId { get; init; }
    public required long SwitchCount { get; init; }
}

public sealed record ContextArchitectureValidationReport
{
    public required bool IsCompliant { get; init; }
    public required IReadOnlyList<string> Findings { get; init; }
    public required IReadOnlyList<string> VerifiedModules { get; init; }
}

/// <summary>
/// Future hierarchy seam — University → College → Campus → Department → Course → Section.
/// College scope only today; no business implementation.
/// </summary>
public interface IOperationalContextHierarchyResolver
{
    ContextHierarchyLevel SupportedLevel { get; }
    Task<TenantContextValidationResult> ValidateNodeAsync(int nodeId, CancellationToken cancellationToken = default);
}

public enum ContextHierarchyLevel
{
    University = 1,
    College = 2,
    Campus = 3,
    Department = 4,
    Course = 5,
    Section = 6,
}
