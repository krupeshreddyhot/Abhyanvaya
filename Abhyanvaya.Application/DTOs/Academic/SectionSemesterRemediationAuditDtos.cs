namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G.1 —
/// Read-only Section Semester remediation post-execution audit &amp; readiness contract.
/// </summary>
public enum SectionSemesterAuditClassification
{
    SafeForFinance = 1,
    SafeForCa = 2,
    AlreadyCorrect = 3,
    ManualMappingRequired = 4,
    Blocked = 5,
    InvalidReference = 6,
}

public enum SectionSemesterAuditReadiness
{
    Ready = 1,
    NotReady = 2,
}

public enum TeachingGroupSectionCompatibilityStatus
{
    Compatible = 1,
    InterimLegacyTgAllowed = 2,
    Incompatible = 3,
    MissingTeachingGroup = 4,
    CrossTenant = 5,
}

public sealed class SectionSemesterRemediationAuditResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public string PromptCode { get; init; } = "P1-4-3G.1";
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }

    public int LegacySemesterId { get; init; } = 3;
    public int? FinanceTargetSemesterId { get; init; }
    public int? CaTargetSemesterId { get; init; }
    public bool FinanceTargetValid { get; init; }
    public bool CaTargetValid { get; init; }
    public string FinanceTargetValidationNotes { get; init; } = "";
    public string CaTargetValidationNotes { get; init; } = "";

    public int TotalLegacySections { get; init; }
    public int SafeFinanceCount { get; init; }
    public int SafeCaCount { get; init; }
    public int AlreadyCorrectCount { get; init; }
    public int ManualMappingCount { get; init; }
    public int BlockedCount { get; init; }
    public int InvalidCount { get; init; }
    public int TeachingGroupSectionDependencyCount { get; init; }

    public SectionSemesterAuditReadiness Readiness { get; init; } = SectionSemesterAuditReadiness.NotReady;
    public string ReadinessCode { get; init; } = "NOT_READY";
    public bool IsReady { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];

    public IReadOnlyList<SectionSemesterAuditSectionRowDto> Sections { get; init; } = [];
    public IReadOnlyList<SectionSemesterAuditTgsRowDto> TeachingGroupSections { get; init; } = [];
    public string ResolutionPrecedence { get; init; } = "";
    public string FutureExecutionContract { get; init; } = "";
    public string RecommendedNextPrompt { get; init; } = "";
}

public sealed class SectionSemesterAuditSectionRowDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int CurrentSemesterId { get; init; }
    public int? CurrentSemesterNumber { get; init; }
    public int CurrentGroupId { get; init; }
    public int? ResolvedGroupId { get; init; }
    public int? TargetSemesterId { get; init; }
    public SectionSemesterAuditClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = "";
    public string ResolutionReason { get; init; } = "";
    public bool IsDeterministic { get; init; }
    public string Confidence { get; init; } = "";
    public int TeachingGroupSectionCount { get; init; }
    public int StudentSectionCount { get; init; }
    public int SubjectAllocationCount { get; init; }
    public int TimetableEntryCount { get; init; }
    public int TimetableSectionCount { get; init; }
    public int AttendanceSessionSectionCount { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
}

public sealed class SectionSemesterAuditTgsRowDto
{
    public int TeachingGroupSectionId { get; init; }
    public int TeachingGroupId { get; init; }
    public int SectionId { get; init; }
    public int? TeachingGroupSemesterId { get; init; }
    public int SectionSemesterId { get; init; }
    public int? TeachingGroupGroupId { get; init; }
    public int? ResolvedTargetSemesterId { get; init; }
    public TeachingGroupSectionCompatibilityStatus Compatibility { get; init; }
    public string CompatibilityCode { get; init; } = "";
    public string Notes { get; init; } = "";
}
