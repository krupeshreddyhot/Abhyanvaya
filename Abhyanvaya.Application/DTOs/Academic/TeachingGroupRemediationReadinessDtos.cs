namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (TG readiness) / PromptCode P1-4-3H2 —
/// Post-Section-remediation Teaching Group remediation readiness (read-only).
/// Distinct from Prompt3H post-section integrity / schema readiness audit.
/// </summary>
public enum TgRemediationReadinessStatus
{
    ReadyFor3FReexecution = 1,
    Blocked = 2,
    AlreadyComplete = 3,
    ManualReviewRequired = 4,
}

public enum TgRemediationFindingSeverity
{
    Critical = 1,
    Error = 2,
    Warning = 3,
    Info = 4,
}

public sealed class TeachingGroupRemediationReadinessResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public string PromptCode { get; init; } = "P1-4-3H2";
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public bool Prompt3FExecuteInvoked { get; init; }

    public bool IsHealthy { get; init; }
    public bool CanReExecuteTeachingGroupRemediation { get; init; }

    public int CriticalCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }

    public IReadOnlyList<int> ApprovedTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> ReadyTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> BlockedTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> AlreadyCompleteTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> ManualReviewTeachingGroupIds { get; init; } = [];

    public int SectionLegacyReferenceCount { get; init; }
    public int TeachingGroupLegacyReferenceCount { get; init; }

    public TgRemediationTargetSemesterValidationDto TargetSemesterValidation { get; init; } = new();
    public string TenantIsolationStatus { get; init; } = "UNKNOWN";
    public bool TenantIsolationOk { get; init; }

    public TgRemediationDownstreamRegressionDto DownstreamRegression { get; init; } = new();
    public IReadOnlyList<TgRemediationFindingDto> Findings { get; init; } = [];
    public IReadOnlyList<TgRemediationTeachingGroupRowDto> TeachingGroups { get; init; } = [];
    public IReadOnlyList<TgRemediationSectionLegacyRowDto> LegacySections { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string RecommendedNextPrompt { get; init; } = "";
}

public sealed class TgRemediationTargetSemesterValidationDto
{
    public int LegacySemesterId { get; init; } = 3;
    public int TargetSemesterId { get; init; } = 11;
    public bool LegacyValid { get; init; }
    public bool TargetValid { get; init; }
    public int? TargetGroupId { get; init; }
    public int? TargetCourseId { get; init; }
    public bool CourseGroupAligned { get; init; }
    public bool NoDuplicateGroupNumber { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class TgRemediationDownstreamRegressionDto
{
    public int StudentLegacySem3Count { get; init; }
    public int AttendanceLegacySem3Count { get; init; }
    public int SubjectAllocationLegacySem3Count { get; init; }
    public int TimetableEntryLegacySem3Count { get; init; }
    public int SubjectLegacySem3Count { get; init; }
    public bool AttendanceSaTtRegressionDetected { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class TgRemediationFindingDto
{
    public string Code { get; init; } = "";
    public TgRemediationFindingSeverity Severity { get; init; }
    public string SeverityCode { get; init; } = "";
    public string EntityType { get; init; } = "";
    public int? EntityId { get; init; }
    public int? CurrentSemesterId { get; init; }
    public int? TargetSemesterId { get; init; }
    public int? CurrentGroupId { get; init; }
    public int? TargetGroupId { get; init; }
    public string Reason { get; init; } = "";
    public string RemediationStatus { get; init; } = "";
}

public sealed class TgRemediationTeachingGroupRowDto
{
    public int TeachingGroupId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = "";
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int CurrentSemesterId { get; init; }
    public int? TargetSemesterId { get; init; }
    public TgRemediationReadinessStatus Readiness { get; init; }
    public string ReadinessCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public int TeachingGroupSectionCount { get; init; }
    public int CompatibleSectionCount { get; init; }
    public int IncompatibleSectionCount { get; init; }
    public IReadOnlyList<int> LinkedSectionIds { get; init; } = [];
}

public sealed class TgRemediationSectionLegacyRowDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public bool CompatibleWithCaTarget { get; init; }
    public string Notes { get; init; } = "";
}
