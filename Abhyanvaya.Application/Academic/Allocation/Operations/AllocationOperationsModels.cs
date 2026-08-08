namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationHistoryFilter
{
    public int? AcademicYearId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? CreatedBy { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Status { get; init; }
    public string? LifecycleStatus { get; init; }
}

public sealed class AllocationHistoryRow
{
    public Guid SessionId { get; init; }
    public Guid ScenarioId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Status { get; init; } = "";
    public string LifecycleStatus { get; init; } = "";
    public double Score { get; init; }
    public string GroupingMode { get; init; } = "";
    public int VersionNumber { get; init; }
    public string ContextChecksum { get; init; } = "";
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int? CreatedBy { get; init; }
    public string Kind { get; init; } = "Scenario";
}

public sealed class AllocationScenarioVersionDto
{
    public Guid ScenarioId { get; init; }
    public int VersionNumber { get; init; }
    public string ContextVersion { get; init; } = "";
    public string ContextChecksum { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int? CreatedBy { get; init; }
    public string Reason { get; init; } = "";
    public string Operation { get; init; } = "";
    public string StrategyConfigurationVersion { get; init; } = "";
    public string ConstraintConfigurationVersion { get; init; } = "";
    public double Score { get; init; }
    public string Status { get; init; } = "";
    public string Checksum { get; init; } = "";
}

public sealed class AllocationExplanationReport
{
    public Guid ScenarioId { get; init; }
    public IReadOnlyList<AllocationStudentExplanation> Assigned { get; init; } = [];
    public IReadOnlyList<AllocationStudentExplanation> Unallocated { get; init; } = [];
    public AllocationScoreBreakdown Score { get; init; } = new();
    public IReadOnlyList<AllocationConstraintEvaluation> Constraints { get; init; } = [];
}

public sealed class AllocationStudentExplanation
{
    public int StudentId { get; init; }
    public string? StudentNumber { get; init; }
    public string? StudentName { get; init; }
    public string? RecommendedSectionCode { get; init; }
    public bool Assigned { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
}

public sealed class AllocationMultiCompareReport
{
    public double OriginalScore { get; init; }
    public IReadOnlyList<AllocationScenarioCompareSide> Scenarios { get; init; } = [];
    public Guid? BestScenarioId { get; init; }
    public string? BestScenarioLabel { get; init; }
    public double ImprovementVsOriginal { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class AllocationScenarioCompareSide
{
    public Guid ScenarioId { get; init; }
    public string Label { get; init; } = "";
    public double Score { get; init; }
    public int StudentsMoved { get; init; }
    public int SectionsAffected { get; init; }
    public int UnallocatedStudents { get; init; }
    public int MandatoryViolations { get; init; }
    public int PreferredViolations { get; init; }
    public double CapacityUtilization { get; init; }
    public double ConstraintCompliance { get; init; }
    public AllocationScoreBreakdown ScoreBreakdown { get; init; } = new();
}

public sealed class AllocationConstraintDashboardDto
{
    public int TotalConstraints { get; init; }
    public int MandatoryViolations { get; init; }
    public int PreferredViolations { get; init; }
    public int InformationalFindings { get; init; }
    /// <summary>Deprecated aggregate — prefer MandatoryCompliance / PreferredCompliance.</summary>
    public double CompliancePercent { get; init; }
    public double MandatoryCompliance { get; init; }
    public double PreferredCompliance { get; init; }
    public IReadOnlyList<AllocationConstraintEvaluation> Rows { get; init; } = [];
}

public sealed class AllocationHeatmapDto
{
    public string Title { get; init; } = "Latest Scenario – Section Utilization";
    public string ScopeNote { get; init; } =
        "Latest Scenario only — not Current Institutional Allocation / live production state.";
    public Guid? ScenarioId { get; init; }
    public string? LifecycleStatus { get; init; }
    public IReadOnlyList<AllocationHeatmapCell> Cells { get; init; } = [];
    public double AverageOccupancy { get; init; }
    public int WarningPercent { get; init; } = 90;
    public int UnderCapacityPercent { get; init; } = 40;
}

public sealed class AllocationHeatmapCell
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public int StudentCount { get; init; }
    public int MaximumCapacity { get; init; }
    public int AvailableCapacity { get; init; }
    public double OccupancyPercent { get; init; }
    /// <summary>OverCapacity | NearCapacity | Healthy | Underused</summary>
    public string Band { get; init; } = "Healthy";
}

public sealed class AllocationAnalyticsDto
{
    public string Period { get; init; } = "AcademicYear";
    public int TotalRuns { get; init; }
    public double SuccessRate { get; init; }
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }
    public int CancelledRuns { get; init; }
    public int TimedOutRuns { get; init; }
    public int RunningRuns { get; init; }
    public int StudentsAllocated { get; init; }
    public int StudentsUnallocated { get; init; }
    public double AverageSectionOccupancy { get; init; }
    public double MandatoryCompliance { get; init; }
    public double PreferredCompliance { get; init; }
    public int InformationalFindings { get; init; }
    public double AverageScore { get; init; }
    public double AverageImprovement { get; init; }
    public double AverageDurationMs { get; init; }
}

public sealed class AllocationOpsDashboardDto
{
    public int TotalRuns { get; init; }
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }
    public int CancelledRuns { get; init; }
    public int TimedOutRuns { get; init; }
    public int RunningRuns { get; init; }
    public int StudentsAllocated { get; init; }
    public int StudentsUnallocated { get; init; }
    public double AverageScore { get; init; }
    public int OverCapacitySections { get; init; }
    public int NearCapacitySections { get; init; }
    public int UnderUtilizedSections { get; init; }
    public int OptimalSections { get; init; }
    public int MandatoryViolations { get; init; }
    public int PreferredWarnings { get; init; }
    public int InformationalFindings { get; init; }
    public double MandatoryCompliance { get; init; }
    public double PreferredCompliance { get; init; }
    /// <summary>Deprecated aggregate — prefer MandatoryCompliance / PreferredCompliance.</summary>
    public double CompliancePercent { get; init; }
    public int DraftCount { get; init; }
    public int UnderReviewCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
    public int ArchivedCount { get; init; }
    public IReadOnlyList<AllocationHistoryRow> RecentRuns { get; init; } = [];
    public IReadOnlyList<AllocationAuditDto> RecentActivity { get; init; } = [];
    public AllocationHeatmapDto Heatmap { get; init; } = new();
    public AllocationConstraintDashboardDto Constraints { get; init; } = new();
}

public sealed class AllocationAuditDto
{
    public Guid AuditId { get; init; }
    public string Action { get; init; } = "";
    public Guid? ScenarioId { get; init; }
    public Guid? SessionId { get; init; }
    public int? VersionNumber { get; init; }
    public string? ContextVersion { get; init; }
    public string Result { get; init; } = "";
    public string? Detail { get; init; }
    public DateTime OccurredAt { get; init; }
    public int? ActorUserId { get; init; }
}

public sealed class AllocationGovernanceResult
{
    public bool Success { get; init; }
    public string Operation { get; init; } = "";
    public Guid? ScenarioId { get; init; }
    public int? ScenarioVersion { get; init; }
    public string Message { get; init; } = "";
    public bool CanApprove { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool ContextStale { get; init; }
    public bool ChecksumInvalid { get; init; }
    public bool ConcurrencyConflict { get; init; }
    public bool AuthorizationFailure { get; init; }
    public AllocationDraft? Draft { get; init; }
    public string? ScenarioContextVersion { get; init; }
    public string? CurrentContextVersion { get; init; }
    public bool ContextCurrent { get; init; }

    public static AllocationGovernanceResult Failure(
        string operation,
        Guid? scenarioId,
        string message,
        IReadOnlyList<string>? errors = null,
        bool concurrencyConflict = false,
        bool contextStale = false,
        bool checksumInvalid = false,
        bool authorizationFailure = false,
        int? version = null)
        => new()
        {
            Success = false,
            Operation = operation,
            ScenarioId = scenarioId,
            ScenarioVersion = version,
            Message = message,
            Errors = errors ?? [message],
            BlockingReasons = errors ?? [message],
            ConcurrencyConflict = concurrencyConflict,
            ContextStale = contextStale,
            ChecksumInvalid = checksumInvalid,
            AuthorizationFailure = authorizationFailure,
            CanApprove = false,
            Warnings = [],
        };
}

public sealed class AllocationScenarioDetailDto
{
    public Guid ScenarioId { get; init; }
    public Guid SessionId { get; init; }
    public string LifecycleStatus { get; init; } = "";
    public string Status { get; init; } = "";
    public int CurrentVersionNumber { get; init; }
    public double TotalScore { get; init; }
    public string ContextChecksum { get; init; } = "";
    public string ContextVersion { get; init; } = "";
    public string? CurrentContextVersion { get; init; }
    public bool ContextCurrent { get; init; }
    public string ScenarioChecksum { get; init; } = "";
    public DateTime GeneratedAt { get; init; }
    public AllocationScenario Scenario { get; init; } = new();
    public AllocationGovernanceResult Governance { get; init; } = new();
    public IReadOnlyList<AllocationScenarioVersionDto> Versions { get; init; } = [];
}
