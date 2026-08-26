namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3A — explicit migration decisions (read-only plan).</summary>
public enum LegacySemesterMigrationDecision
{
    Split = 1,
    MapToSingleGroup = 2,
    RetainLegacyPendingDecision = 3,
    DuplicateReview = 4,
    AlreadyGroupSpecific = 5,
    InvalidData = 6,
}

public enum DownstreamReferenceDeterminism
{
    DeterministicByEntityGroupId = 1,
    DeterministicByStudentGroupId = 2,
    ManualReviewRequired = 3,
    IdentifyOnlyDoNotMutate = 4,
    NoReferences = 5,
}

public sealed class DownstreamReferenceClassificationDto
{
    public string EntityType { get; init; } = null!;
    public int ReferenceCount { get; init; }
    public IReadOnlyDictionary<int, int> CountsByGroupId { get; init; } = new Dictionary<int, int>();
    public DownstreamReferenceDeterminism Determinism { get; init; }
    public string DeterminismCode { get; init; } = null!;
    public string Notes { get; init; } = null!;
}

public sealed class LegacySemesterMigrationDecisionRowDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = null!;
    public int Number { get; init; }
    public string Name { get; init; } = null!;
    public int? CurrentGroupId { get; init; }
    public string? CurrentGroupName { get; init; }
    public IReadOnlyList<int> TargetGroupIds { get; init; } = [];
    public IReadOnlyList<string> TargetGroupNames { get; init; } = [];
    public LegacySemesterMigrationDecision Decision { get; init; }
    public string DecisionCode { get; init; } = null!;
    public string DecisionReason { get; init; } = null!;
    public IReadOnlyDictionary<int, int> StudentCountsByTargetGroup { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<DownstreamReferenceClassificationDto> DownstreamClassifications { get; init; } = [];
    public bool RequiresManualApproval { get; init; }
    public bool MustNotModify { get; init; }
}

public sealed class LegacySemesterMigrationDecisionPlanDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool MatchesPrompt2BBaseline { get; init; }
    public IReadOnlyList<string> RevalidationNotes { get; init; } = [];
    public IReadOnlyList<LegacySemesterMigrationDecisionRowDto> Decisions { get; init; } = [];
    public IReadOnlyList<LegacySemesterMigrationDecisionRowDto> DuplicateReviewRows { get; init; } = [];
    public IReadOnlyList<int> RecordsMustNotModify { get; init; } = [];
    public IReadOnlyList<string> ProposedCreates { get; init; } = [];
    public IReadOnlyList<string> ProposedUpdates { get; init; } = [];
    public IReadOnlyList<string> ManualApprovalsRequired { get; init; } = [];
    public IReadOnlyList<string> MigrationBlockers { get; init; } = [];
}
