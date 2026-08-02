using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization;

/// <summary>
/// Strategy extension point for Phase 3. Phase 2B.6 ships contracts + NoOp only.
/// Strategies must never edit the live timetable.
/// </summary>
public interface IOptimizationStrategy
{
    string StrategyCode { get; }
    string StrategyName { get; }
    OptimizationStrategyKind Kind { get; }
    bool IsImplemented { get; }

    Task<OptimizationResult> ProposeAsync(
        OptimizationContext context,
        OptimizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class OptimizationContext
{
    public required int TenantId { get; init; }
    public required int AcademicYearId { get; init; }
    public int? TimetableId { get; init; }
    public int? DepartmentId { get; init; }
    public int EntryCount { get; init; }
    public int ConflictCount { get; init; }
    public IReadOnlyDictionary<string, decimal> BaselineMetrics { get; init; } = new Dictionary<string, decimal>();
    /// <summary>In-memory working copy for pipeline strategies. Never persisted to production.</summary>
    public IReadOnlyList<OptimizationEntrySnapshot> WorkingEntries { get; init; } = [];
    public IReadOnlyDictionary<int, OptimizationRoomSnapshot> Rooms { get; init; } = new Dictionary<int, OptimizationRoomSnapshot>();
    public IReadOnlyDictionary<int, OptimizationSlotSnapshot> TimeSlots { get; init; } = new Dictionary<int, OptimizationSlotSnapshot>();
    public IReadOnlyDictionary<int, int> FacultyPreferredRoomIds { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> SubjectExpectedCapacities { get; init; } = new Dictionary<int, int>();
    public bool AllowTimetableMutation => false;
}

/// <summary>Mutable in-memory entry used only inside optimization pipeline / sandbox proposals.</summary>
public sealed class OptimizationEntrySnapshot
{
    public int EntryId { get; set; }
    public int TimetableId { get; set; }
    public byte DayOfWeek { get; set; }
    public int TimeSlotId { get; set; }
    public int StaffId { get; set; }
    public int RoomId { get; set; }
    public int DepartmentId { get; set; }
    public int SubjectId { get; set; }
    public int GroupId { get; set; }
    public int SubjectAllocationId { get; set; }
}

public sealed class OptimizationRoomSnapshot
{
    public int RoomId { get; init; }
    public string Name { get; init; } = "";
    public int Capacity { get; init; }
    public int? BuildingId { get; init; }
}

public sealed class OptimizationSlotSnapshot
{
    public int TimeSlotId { get; init; }
    public string Name { get; init; } = "";
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsPeriod { get; init; } = true;
}

public sealed class OptimizationRequest
{
    public int? TimetableId { get; init; }
    public int? AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public OptimizationStrategyKind StrategyKind { get; init; } = OptimizationStrategyKind.None;
    public string? ScenarioName { get; init; }
    public bool PreviewOnly { get; init; } = true;
    public bool ApplyChanges => false;
}

public sealed class OptimizationCandidate
{
    public required string CandidateId { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> ProposedChangeSummaries { get; init; } = [];
    public OptimizationScore? Score { get; init; }
    public string ChangeType { get; init; } = "Advise";
    public int? EntryId { get; init; }
    public int? ProposedRoomId { get; init; }
    public int? ProposedStaffId { get; init; }
    public int? ProposedTimeSlotId { get; init; }
    public byte? ProposedDayOfWeek { get; init; }
    public string StrategyCode { get; init; } = "";
    public bool IsAdvisoryOnly => true;
    public bool ModifiesLiveTimetable => false;
}

public sealed class OptimizationScore
{
    public decimal TotalScore { get; init; }
    public decimal NormalizedScore { get; init; }
    public IReadOnlyList<OptimizationDimensionScore> Dimensions { get; init; } = [];
}

public sealed class OptimizationDimensionScore
{
    public OptimizationDimension Dimension { get; init; }
    public decimal RawValue { get; init; }
    public decimal Weight { get; init; }
    public decimal WeightedScore { get; init; }
}

public sealed class OptimizationSummary
{
    public int CandidateCount { get; init; }
    public decimal BaselineScore { get; init; }
    public decimal BestProjectedScore { get; init; }
    public decimal ImprovementDelta { get; init; }
    public int BaselineConflictCount { get; init; }
    public int ProjectedConflictCount { get; init; }
    public string StatusMessage { get; init; } = "Readiness only — no optimizer executed.";
}

public sealed class OptimizationExecution
{
    public Guid ExecutionId { get; init; }
    public OptimizationStrategyKind StrategyKind { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public long ExecutionTimeMs { get; init; }
    public long ScoringTimeMs { get; init; }
    public bool AppliedToTimetable => false;
    public string Outcome { get; init; } = "Preview";
}

public sealed class OptimizationResult
{
    public required OptimizationExecution Execution { get; init; }
    public required OptimizationSummary Summary { get; init; }
    public required OptimizationScore BaselineScore { get; init; }
    public OptimizationScore? ProjectedScore { get; init; }
    public IReadOnlyList<OptimizationCandidate> Candidates { get; init; } = [];
    public bool IsPreviewOnly => true;
    public bool ModifiesTimetable => false;
}
