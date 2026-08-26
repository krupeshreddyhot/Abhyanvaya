namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A — explicit per-Semester disposition request.</summary>
public sealed class LegacySemesterHistoricalDispositionExecuteRequest
{
    /// <summary>Required. Each Semester disposition must be explicit (no archive-all).</summary>
    public List<LegacySemesterHistoricalDispositionItemRequest> Items { get; set; } = [];

    /// <summary>Optional human reason recorded in journal evidence.</summary>
    public string? Reason { get; set; }
}

public sealed class LegacySemesterHistoricalDispositionItemRequest
{
    public int SemesterId { get; set; }

    /// <summary>
    /// HISTORICAL_ARCHIVE | RETAIN_HISTORICAL_PENDING_REVIEW | DUPLICATE_REVIEW | MANUAL_MAPPING_REQUIRED
    /// </summary>
    public string Disposition { get; set; } = "";
}

public static class LegacySemesterHistoricalDispositionCodes
{
    public const string PromptCode = "P1-4-3JA";

    public const string HistoricalArchive = "HISTORICAL_ARCHIVE";
    public const string RetainHistoricalPendingReview = "RETAIN_HISTORICAL_PENDING_REVIEW";
    public const string DuplicateReview = "DUPLICATE_REVIEW";
    public const string ManualMappingRequired = "MANUAL_MAPPING_REQUIRED";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        HistoricalArchive,
        RetainHistoricalPendingReview,
        DuplicateReview,
        ManualMappingRequired,
    };

    /// <summary>Only HISTORICAL_ARCHIVE mutates Semester.IsHistoricalArchive.</summary>
    public static bool MutatesSemesterRow(string disposition)
        => string.Equals(disposition, HistoricalArchive, StringComparison.OrdinalIgnoreCase);
}

public sealed class LegacySemesterHistoricalDispositionPreviewDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public string PromptCode { get; init; } = LegacySemesterHistoricalDispositionCodes.PromptCode;
    public int LegacyNullGroupCount { get; init; }
    public int HistoricalArchiveCount { get; init; }
    public int PendingReviewCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public int ManualMappingRequiredCount { get; init; }
    public int EligibleForHistoricalArchiveCount { get; init; }
    public IReadOnlyList<LegacySemesterHistoricalDispositionCandidateDto> Candidates { get; init; } = [];
    public IReadOnlyList<LegacySemesterDependencyMatrixRowDto> DependencyMatrix { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool SchemaHardeningReady { get; init; }
    public bool Prompt3JAuthorized { get; init; }
}

public sealed class LegacySemesterHistoricalDispositionCandidateDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = "";
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int? GroupId { get; init; }
    public bool IsHistoricalArchive { get; init; }
    public string RecommendedDisposition { get; init; } = "";
    public string CurrentJournalDisposition { get; init; } = "";
    public bool EligibleForHistoricalArchive { get; init; }
    public bool Blocked { get; init; }
    public string Reason { get; init; } = "";
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public int OperationalRefTotal { get; init; }
    public IReadOnlyList<string> AllowedDispositions { get; init; } = [];
}

public sealed class LegacySemesterDependencyMatrixRowDto
{
    public string Entity { get; init; } = "";
    public string SemesterFk { get; init; } = "";
    public string OperationalOrHistoricalMeaning { get; init; } = "";
    public bool CanReferenceArchivedSemester { get; init; }
    public bool MustRemapBeforeArchival { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class LegacySemesterHistoricalDispositionResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsSuccessful { get; init; }
    public string ExecutionStatus { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public bool RolledBack { get; init; }
    public bool TransactionCommitted { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public int BlockedCount { get; init; }
    public int JournalOnlyCount { get; init; }
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }
    public IReadOnlyList<LegacySemesterHistoricalDispositionFindingDto> Findings { get; init; } = [];
    public LegacySemesterHistoricalPostDispositionIntegrityDto? PostDispositionIntegrity { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool SchemaHardeningReady { get; init; }
    public bool Prompt3JAuthorized { get; init; }
}

public sealed class LegacySemesterHistoricalDispositionFindingDto
{
    public int SemesterId { get; init; }
    public string RequestedDisposition { get; init; } = "";
    public string PreviousState { get; init; } = "";
    public string NewState { get; init; } = "";
    public string Result { get; init; } = "";
    public bool SemesterRowMutated { get; init; }
    public bool GroupIdMutated { get; init; }
    public int AffectedDownstreamReferenceCount { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class LegacySemesterHistoricalPostDispositionIntegrityDto
{
    public bool Passed { get; init; }
    public int HistoricalArchiveCount { get; init; }
    public int NullGroupNonArchivedCount { get; init; }
    public int OperationalWithHistoricalFlagCount { get; init; }
    public int CrossTenantJournalViolationCount { get; init; }
    public IReadOnlyList<string> Findings { get; init; } = [];
}
