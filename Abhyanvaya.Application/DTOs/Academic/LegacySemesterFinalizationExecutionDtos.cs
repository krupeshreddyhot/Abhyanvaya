namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E — execution disposition codes.</summary>
public static class LegacySemesterExecutionDispositionCodes
{
    public const string RetainHistorical = "RETAIN_HISTORICAL";
    public const string ManualMappingRequired = "MANUAL_MAPPING_REQUIRED";
    public const string DuplicateReview = "DUPLICATE_REVIEW";
    public const string BlockedByTeachingGroupReference = "BLOCKED_BY_TEACHING_GROUP_REFERENCE";
    public const string AlreadyGroupSpecific = "ALREADY_GROUP_SPECIFIC";
    public const string FinalizedLegacy = "FINALIZED_LEGACY";
}

public sealed class LegacySemesterFinalizationExecutionItemDto
{
    public int SemesterId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int CourseId { get; init; }
    public string ClassifierDispositionCode { get; init; } = "";
    public string DispositionCode { get; init; } = "";
    public string Action { get; init; } = ""; // Retain | Block | DeferTg | AlreadyComplete | Skip
    public string BlockingReason { get; init; } = "";
    public bool MutationAllowed { get; init; }
    public bool SemesterRowMutated { get; init; }
    public bool JournalWritten { get; init; }
    public int? CandidateTargetSemesterIdForTg { get; init; }
    public IReadOnlyList<int> TeachingGroupIds { get; init; } = [];
}

public sealed class LegacySemesterFinalizationExecutionSummaryDto
{
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int RetainedCount { get; init; }
    public int DeferredTeachingGroupCount { get; init; }
}

public sealed class LegacySemesterFinalizationExecutionResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public string ExecutionStatus { get; init; } = "NotExecuted";
    public bool RolledBack { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int RetainedCount { get; init; }
    public int DeferredTeachingGroupCount { get; init; }
    public DateTime? FinalizationTimestamp { get; init; }
    public IReadOnlyList<int> AffectedSemesterIds { get; init; } = [];
    public IReadOnlyList<LegacySemesterFinalizationExecutionItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public LegacySemesterFinalizationAuditDto? PostFinalizationAudit { get; init; }
    public bool SchemaHardeningReady { get; init; }
}
