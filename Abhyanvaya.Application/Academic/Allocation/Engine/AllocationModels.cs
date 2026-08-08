namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Pipeline configuration (enabled strategies + grouping mode).</summary>
public sealed class AllocationPipelineConfig
{
    public string GroupingMode { get; init; } = AllocationGroupingModes.Alphabetical;
    public IReadOnlyDictionary<string, bool> EnabledStrategies { get; init; }
        = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, AllocationConstraintPriority> ConstraintPriorities { get; init; }
        = new Dictionary<string, AllocationConstraintPriority>(StringComparer.OrdinalIgnoreCase);

    public bool IsStrategyEnabled(string code)
        => !EnabledStrategies.TryGetValue(code, out var enabled) || enabled;

    public static AllocationPipelineConfig Default { get; } = new()
    {
        GroupingMode = AllocationGroupingModes.Alphabetical,
        EnabledStrategies = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [AllocationStrategyCodes.Validation] = true,
            [AllocationStrategyCodes.Capacity] = true,
            [AllocationStrategyCodes.Policy] = true,
            [AllocationStrategyCodes.Gender] = true,
            [AllocationStrategyCodes.Language] = true,
            [AllocationStrategyCodes.Scholarship] = false,
            [AllocationStrategyCodes.Elective] = false,
            [AllocationStrategyCodes.Transport] = false,
            [AllocationStrategyCodes.Hostel] = false,
            [AllocationStrategyCodes.Merit] = false,
            [AllocationStrategyCodes.Scoring] = true,
        },
        ConstraintPriorities = new Dictionary<string, AllocationConstraintPriority>(StringComparer.OrdinalIgnoreCase)
        {
            ["Capacity"] = AllocationConstraintPriority.Mandatory,
            ["ReservedSeats"] = AllocationConstraintPriority.Mandatory,
            ["GenderBalance"] = AllocationConstraintPriority.Preferred,
            ["Language"] = AllocationConstraintPriority.Preferred,
            ["Merit"] = AllocationConstraintPriority.Preferred,
            ["Hostel"] = AllocationConstraintPriority.Informational,
            ["Transport"] = AllocationConstraintPriority.Informational,
            ["ElectiveCombination"] = AllocationConstraintPriority.Preferred,
            ["MinorSubject"] = AllocationConstraintPriority.Informational,
            ["Scholarship"] = AllocationConstraintPriority.Preferred,
        },
    };
}

public enum AllocationConstraintPriority
{
    Mandatory = 0,
    Preferred = 1,
    Informational = 2,
}

public static class AllocationStrategyCodes
{
    public const string Validation = "Validation";
    public const string Capacity = "Capacity";
    public const string Policy = "Policy";
    public const string Gender = "Gender";
    public const string Language = "Language";
    public const string Scholarship = "Scholarship";
    public const string Elective = "Elective";
    public const string Transport = "Transport";
    public const string Hostel = "Hostel";
    public const string Merit = "Merit";
    public const string Scoring = "Scoring";
}

public static class AllocationGroupingModes
{
    public const string StudentNumber = "StudentNumber";
    public const string StudentNumberRange = "StudentNumberRange";
    public const string Alphabetical = "Alphabetical";
    public const string Merit = "Merit";
    public const string Gender = "Gender";
    public const string Language = "Language";
    public const string Scholarship = "Scholarship";
    public const string MinorSubject = "MinorSubject";
    public const string Hostel = "Hostel";
    public const string Transport = "Transport";
    public const string ElectiveCombination = "ElectiveCombination";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentNumber, StudentNumberRange, Alphabetical, Merit, Gender, Language,
        Scholarship, MinorSubject, Hostel, Transport, ElectiveCombination,
    ];
}

public sealed class AllocationExecutionContext
{
    public Guid SessionId { get; init; }
    public SectionAllocationContext Context { get; init; } = new();
    public AllocationPipelineConfig Config { get; init; } = AllocationPipelineConfig.Default;
    public DateTime StartedAt { get; init; }
}

public sealed class AllocationExecutionResult
{
    public Guid SessionId { get; init; }
    public Guid ScenarioId { get; init; }
    public bool Succeeded { get; init; }
    public string Status { get; init; } = "Completed";
    public AllocationScenario Scenario { get; init; } = new();
    public AllocationTrace Trace { get; init; } = new();
    public AllocationScoreBreakdown Score { get; init; } = new();
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public double DurationMs { get; init; }
}

public sealed class AllocationSession
{
    public Guid SessionId { get; init; }
    public Guid ContextId { get; init; }
    public string ContextChecksum { get; init; } = "";
    public string Status { get; init; } = "Created";
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string GroupingMode { get; init; } = "";
    public Guid? ActiveScenarioId { get; init; }
}

/// <summary>Immutable allocation scenario — recommendations only; never live student writes.</summary>
public sealed class AllocationScenario
{
    public Guid ScenarioId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ContextId { get; init; }
    public string ContextChecksum { get; init; } = "";
    public DateTime GeneratedAt { get; init; }
    public string Status { get; init; } = "Draft";
    public IReadOnlyList<AllocationStudentRecommendation> Recommendations { get; init; } = [];
    public IReadOnlyList<AllocationSectionSummary> SectionSummaries { get; init; } = [];
    public AllocationScoreBreakdown Score { get; init; } = new();
    public IReadOnlyList<AllocationConstraintEvaluation> Constraints { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}

public sealed class AllocationStudentRecommendation
{
    public int StudentId { get; init; }
    public string? StudentNumber { get; init; }
    public string? StudentName { get; init; }
    public int? FromSectionId { get; init; }
    public string? FromSectionCode { get; init; }
    public int ToSectionId { get; init; }
    public string ToSectionCode { get; init; } = "";
    public IReadOnlyList<string> Explanations { get; init; } = [];
}

public sealed class AllocationSectionSummary
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public int MaximumCapacity { get; init; }
    public int AssignedCount { get; init; }
    public int ReservedSeats { get; init; }
    public double OccupancyPercent { get; init; }
}

public sealed class AllocationScoreBreakdown
{
    public double TotalScore { get; init; }
    public double CapacityUtilization { get; init; }
    public double PolicyCompliance { get; init; }
    public double GenderBalance { get; init; }
    public double MeritDistribution { get; init; }
    public double LanguageDistribution { get; init; }
    public double HostelDistribution { get; init; }
    public double ElectiveBalance { get; init; }
    public double TransportBalance { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class AllocationConstraintEvaluation
{
    public string ConstraintCode { get; init; } = "";
    public AllocationConstraintPriority Priority { get; init; }
    public bool Satisfied { get; init; }
    public string Summary { get; init; } = "";
    public double ScoreImpact { get; init; }
}

public sealed class AllocationTrace
{
    public Guid TraceId { get; init; }
    public Guid SessionId { get; init; }
    public IReadOnlyList<AllocationTraceStep> Steps { get; init; } = [];
}

public sealed class AllocationTraceStep
{
    public int Order { get; init; }
    public string StrategyCode { get; init; } = "";
    public bool Enabled { get; init; }
    public bool Executed { get; init; }
    public double DurationMs { get; init; }
    public double ScoreAfter { get; init; }
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> ConstraintNotes { get; init; } = [];
}

public sealed class AllocationProgress
{
    public Guid SessionId { get; init; }
    public string CurrentStrategy { get; init; } = "";
    public int ProgressPercent { get; init; }
    public int StudentsProcessed { get; init; }
    public int TotalStudents { get; init; }
    public double? CurrentScore { get; init; }
    public string? EstimatedCompletion { get; init; }
    public string Message { get; init; } = "";
}

public sealed class AllocationComparisonReport
{
    public Guid ScenarioId { get; init; }
    public double OriginalAverageOccupancy { get; init; }
    public double AllocatedAverageOccupancy { get; init; }
    public double CapacityImprovement { get; init; }
    public double GenderBalanceScore { get; init; }
    public double PolicyComplianceScore { get; init; }
    public IReadOnlyList<AllocationSectionSummary> OriginalSections { get; init; } = [];
    public IReadOnlyList<AllocationSectionSummary> AllocatedSections { get; init; } = [];
    public IReadOnlyList<AllocationConstraintEvaluation> ConstraintViolations { get; init; } = [];
    public string Summary { get; init; } = "";
}

public sealed class AllocationDraft
{
    public Guid DraftId { get; init; }
    public Guid ScenarioId { get; init; }
    public Guid SessionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int? ApprovedBy { get; init; }
    public string Status { get; init; } = "Draft";
    public IReadOnlyList<AllocationStudentRecommendation> Recommendations { get; init; } = [];
    public string Note { get; init; } = "Draft only — live student allocations were not modified.";
}

public sealed class AllocationSandboxItem
{
    public Guid SandboxId { get; init; }
    public string Name { get; init; } = "";
    public Guid ScenarioId { get; init; }
    public Guid SessionId { get; init; }
    public DateTime SavedAt { get; init; }
    public bool IsArchived { get; init; }
    public string? Tags { get; init; }
}

public sealed class AllocationDashboardDto
{
    public int TotalRuns { get; init; }
    public double BestScore { get; init; }
    public double AverageCapacityUtilization { get; init; }
    public double AverageConstraintCompliance { get; init; }
    public IReadOnlyList<AllocationHistoryItem> RecentRuns { get; init; } = [];
    public IReadOnlyList<AllocationSectionSummary> Distribution { get; init; } = [];
}

public sealed class AllocationHistoryItem
{
    public Guid SessionId { get; init; }
    public Guid? ScenarioId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Status { get; init; } = "";
    public double Score { get; init; }
    public string GroupingMode { get; init; } = "";
}

/// <summary>In-memory working state during deterministic pipeline execution.</summary>
public sealed class AllocationWorkingState
{
    public SectionAllocationContext Context { get; }
    public AllocationPipelineConfig Config { get; }
    public List<int> OrderedStudentIds { get; }
    public Dictionary<int, int> Assignments { get; } = new();
    public Dictionary<int, List<string>> Explanations { get; } = new();
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
    public List<AllocationTraceStep> TraceSteps { get; } = [];
    public AllocationScoreBreakdown CurrentScore { get; set; } = new();
    public List<AllocationConstraintEvaluation> ConstraintEvals { get; set; } = [];

    public AllocationWorkingState(
        SectionAllocationContext context,
        AllocationPipelineConfig config,
        IEnumerable<int> orderedStudentIds)
    {
        Context = context;
        Config = config;
        OrderedStudentIds = orderedStudentIds.ToList();
    }
}
