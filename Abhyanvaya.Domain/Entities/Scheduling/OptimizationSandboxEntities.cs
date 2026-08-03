using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// Optimization sandbox scenario. Snapshots are immutable after Saved.
/// Never modifies production timetables.
/// </summary>
public class OptimizationScenario : BaseEntity
{
    public Guid ScenarioId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ScenarioStatus Status { get; set; } = ScenarioStatus.Draft;
    public int OwnerUserId { get; set; }
    public int AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SemesterId { get; set; }
    public int? TimetableId { get; set; }
    public Guid? SourceSimulationId { get; set; }
    public Guid? ParentScenarioId { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public bool IsTemplate { get; set; }
    public string TagsCsv { get; set; } = "";
    public string Category { get; set; } = "General";
    public decimal CurrentScore { get; set; }
    public decimal ProjectedScore { get; set; }
    public int ConflictCount { get; set; }
    public int ReplayCount { get; set; }
    public int ComparisonCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastReplayedUtc { get; set; }
    public DateTime? LastComparedUtc { get; set; }
    public bool IsImmutable { get; set; }
    public bool ModifiesProductionTimetable => false;

    public ICollection<OptimizationSnapshot> Snapshots { get; set; } = [];
}

/// <summary>Immutable payload attached to a scenario (read-only after save).</summary>
public class OptimizationSnapshot : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public int OptimizationScenarioId { get; set; }
    public OptimizationScenario? Scenario { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = "Baseline";
    public Guid? SimulationId { get; set; }
    public string TimetableSummaryJson { get; set; } = "{}";
    public string SimulationJson { get; set; } = "{}";
    public string ScoresJson { get; set; } = "{}";
    public string ConflictSummaryJson { get; set; } = "{}";
    public string MetricsJson { get; set; } = "{}";
    public string RecommendationsJson { get; set; } = "[]";
    public DateTime CapturedUtc { get; set; }
    public bool IsImmutable { get; set; } = true;
}

public class OptimizationScenarioFavorite : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int UserId { get; set; }
}

public class OptimizationScenarioNote : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int UserId { get; set; }
    public string NoteText { get; set; } = null!;
}

public class OptimizationScenarioComment : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int UserId { get; set; }
    public string CommentText { get; set; } = null!;
}

public class OptimizationScenarioBookmark : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
}

public class OptimizationScenarioApprovalRequest : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int RequestedByUserId { get; set; }
    public int? ReviewerUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Message { get; set; }
    public DateTime RequestedUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}

public class OptimizationScenarioShare : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public int SharedByUserId { get; set; }
    public int SharedWithUserId { get; set; }
    public bool ReadOnly { get; set; } = true;
    public DateTime SharedUtc { get; set; }
}

public class OptimizationScenarioHistory : BaseEntity
{
    public int OptimizationScenarioId { get; set; }
    public ScenarioHistoryAction Action { get; set; }
    public int? ActorUserId { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredUtc { get; set; }
}
