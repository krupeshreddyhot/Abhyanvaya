namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-A (package 3KA) / PromptCode P1-4-3KA —
/// Historical Semester disposition &amp; archive architecture discovery (read-only).
/// Reuses existing <c>IsHistoricalArchive</c> + disposition journals; does not invent a second lifecycle.
/// </summary>
public static class HistoricalSemesterDispositionClassifications
{
    public const string ActiveOperational = "ACTIVE_OPERATIONAL";
    public const string HistoricalRetain = "HISTORICAL_RETAIN";
    public const string ManualMappingRequired = "MANUAL_MAPPING_REQUIRED";
    public const string DuplicateReview = "DUPLICATE_REVIEW";
    public const string BlockedByReference = "BLOCKED_BY_REFERENCE";
    public const string ArchiveEligible = "ARCHIVE_ELIGIBLE";
    public const string Archived = "ARCHIVED";
}

public static class HistoricalSemesterDispositionAuditCodes
{
    public const string PromptCode = "P1-4-3KA";
}

public sealed class HistoricalSemesterDispositionAuditDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public string PromptCode { get; init; } = HistoricalSemesterDispositionAuditCodes.PromptCode;

    public int ActiveOperationalCount { get; init; }
    public int HistoricalRetainCount { get; init; }
    public int ManualMappingRequiredCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public int BlockedByReferenceCount { get; init; }
    public int ArchiveEligibleCount { get; init; }
    public int ArchivedCount { get; init; }
    public int LegacyNullGroupCount { get; init; }

    public bool ExistingArchivePatternFound { get; init; } = true;
    public string ExistingArchivePatternName { get; init; } = "Semester.IsHistoricalArchive + LegacySemesterDispositionJournals (P1-4-3JA)";
    public bool CompetingLifecycleAvoided { get; init; } = true;
    public bool SchemaHardeningDeferred { get; init; } = true;
    public bool TenantIsolationPassed { get; init; }

    public IReadOnlyList<HistoricalSemesterDispositionDto> Items { get; init; } = [];
    public IReadOnlyList<HistoricalSemesterDependencyMatrixRowDto> DownstreamDependencyMatrix { get; init; } = [];
    public IReadOnlyList<string> ArchiveEligibilityRules { get; init; } = [];
    public IReadOnlyList<string> RetainVsArchiveNotes { get; init; } = [];
    public IReadOnlyList<string> FutureExecutionContract { get; init; } = [];
    public IReadOnlyList<string> UiRecommendations { get; init; } = [];
    public IReadOnlyList<string> Blockers { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string RecommendedNextPrompt { get; init; } = "";
}

/// <summary>Read-only disposition row for Prompt 3K-A discovery contract.</summary>
public sealed class HistoricalSemesterDispositionDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = "";
    public int? GroupId { get; init; }
    public int SemesterNumber { get; init; }
    public string Name { get; init; } = "";
    public string Classification { get; init; } = "";
    public bool IsOperational { get; init; }
    public bool IsHistorical { get; init; }
    public bool IsHistoricalArchive { get; init; }
    public bool IsArchiveEligible { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public HistoricalSemesterDownstreamReferenceSummaryDto DownstreamReferenceSummary { get; init; } = new();
    public string RecommendedAction { get; init; } = "";
}

public sealed class HistoricalSemesterDownstreamReferenceSummaryDto
{
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public int OperationalRefTotal { get; init; }
    public int HistoricalDependencyHintCount { get; init; }
}

public sealed class HistoricalSemesterDependencyMatrixRowDto
{
    public string Entity { get; init; } = "";
    public string ReferenceKind { get; init; } = "";
    public string ClassificationGuidance { get; init; } = "";
    public bool BlocksArchiveEligibility { get; init; }
    public bool TeachingGroupIdentifyOnly { get; init; }
    public string Notes { get; init; } = "";
}
