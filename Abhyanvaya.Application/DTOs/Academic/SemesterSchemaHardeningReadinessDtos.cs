namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J (Architect package 3J3) / PromptCode P1-4-3J3 —
/// Final Semester schema-hardening readiness GO/NO-GO contract (read-only).
/// PromptCode avoids colliding with Subject Catalog remediation (P1-4-3J) and prior 3J1 (P1-4-3M).
/// </summary>
public enum SemesterSchemaHardeningDecision
{
    Go = 1,
    NoGo = 2,
}

public enum SemesterSchemaHardeningFindingSeverity
{
    Critical = 1,
    Error = 2,
    Warning = 3,
    Info = 4,
}

public enum NullGroupSemesterDisposition
{
    Remediated = 1,
    RetainHistorical = 2,
    ManualMappingRequired = 3,
    Blocked = 4,
    OtherExplicitApprovedState = 5,
    Unexplained = 6,
}

public enum TgSectionBoundaryClassification
{
    SafeForHardening = 1,
    BlockedByTg = 2,
    BlockedBySection = 3,
    ManualReview = 4,
}

public enum WildcardDependencyKind
{
    ActiveProduction = 1,
    LegacyReadCompatibility = 2,
    HistoricalDisplayOnly = 3,
    DeadUnreachable = 4,
}

public enum DownstreamConsumerStatus
{
    Valid = 1,
    Legacy = 2,
    Mismatch = 3,
    Orphaned = 4,
    CrossTenant = 5,
    ManualReview = 6,
}

/// <summary>Deterministic readiness / blocking codes for Prompt 3J.</summary>
public static class SemesterSchemaHardeningReadinessCodes
{
    public const string ReadyForSchemaHardening = "READY_FOR_SCHEMA_HARDENING";
    public const string NotReadyNullSemesters = "NOT_READY_NULL_SEMESTERS";
    public const string NotReadyDownstreamReferences = "NOT_READY_DOWNSTREAM_REFERENCES";
    public const string NotReadyWildcardConsumers = "NOT_READY_WILDCARD_CONSUMERS";
    public const string NotReadyTgReferences = "NOT_READY_TG_REFERENCES";
    public const string NotReadyDuplicates = "NOT_READY_DUPLICATES";
    public const string NotReadyWritePath = "NOT_READY_WRITE_PATH";
    public const string NotReadyTenantIsolation = "NOT_READY_TENANT_ISOLATION";
    public const string NotReadyManualReview = "NOT_READY_MANUAL_REVIEW";
    public const string NotReadySemesterIntegrity = "NOT_READY_SEMESTER_INTEGRITY";
    public const string NotReadyStudentIntegrity = "NOT_READY_STUDENT_INTEGRITY";
    public const string NotReadySchedulingIntegrity = "NOT_READY_SCHEDULING_INTEGRITY";
}

public sealed class SemesterSchemaHardeningReadinessResult
{
    public DateTime GeneratedAt { get; init; }
    public string PromptCode { get; init; } = "P1-4-3J3";
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public bool IsReady { get; init; }
    public SemesterSchemaHardeningDecision Decision { get; init; } = SemesterSchemaHardeningDecision.NoGo;
    /// <summary>Primary decision code (READY_FOR_SCHEMA_HARDENING or first NOT_READY_*).</summary>
    public string DecisionCode { get; init; } = SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters;
    /// <summary>All applicable readiness / blocking codes (deterministic order).</summary>
    public IReadOnlyList<string> ReadinessCodes { get; init; } = [];

    public int TenantCount { get; init; }
    public int SemesterCount { get; init; }
    public int NullGroupSemesterCount { get; init; }
    public int DuplicateGroupSemesterCount { get; init; }
    public int InvalidOwnershipCount { get; init; }
    public int DuplicateKeyCount { get; init; }
    public int SemesterIntegrityErrorCount { get; init; }
    public int StudentIntegrityErrorCount { get; init; }
    public int AttendanceIntegrityErrorCount { get; init; }
    public int SectionIntegrityErrorCount { get; init; }
    public int SubjectAllocationIntegrityErrorCount { get; init; }
    public int TimetableIntegrityErrorCount { get; init; }
    public int TeachingGroupIntegrityErrorCount { get; init; }
    public int DownstreamLegacyReferenceCount { get; init; }
    public int TeachingGroupBlockingCount { get; init; }
    public int SectionBlockingCount { get; init; }
    public int StudentIntegrityViolationCount { get; init; }
    public int SchedulingIntegrityViolationCount { get; init; }
    public int WildcardConsumerCount { get; init; }
    public int WildcardProductionDependencyCount { get; init; }
    public int ActiveWritePathViolationCount { get; init; }
    public int CrossTenantViolationCount { get; init; }
    public int ManualReviewCount { get; init; }

    public bool NotNullReady { get; init; }
    public bool UniqueReady { get; init; }
    public bool WritePathsGroupOwned { get; init; }
    public bool NoActiveNullGroupWritePath { get; init; }
    public bool ArchitectureGuardsIntact { get; init; }
    public string WildcardConsumerClosureStatus { get; init; } = "OPEN";

    public string LifecycleScopeNote { get; init; } = "";
    public string ConstraintSimulationSummary { get; init; } = "";
    public string EvidenceSummary { get; init; } = "";

    public IReadOnlyList<SemesterSchemaHardeningFindingDto> BlockingFindings { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<NullGroupSemesterAuditRowDto> NullGroupSemesters { get; init; } = [];
    public IReadOnlyList<DownstreamLegacyReferenceRowDto> DownstreamLegacyReferences { get; init; } = [];
    public IReadOnlyList<DownstreamConsumerFindingDto> DownstreamConsumerFindings { get; init; } = [];
    public IReadOnlyList<DuplicateSemesterKeyRowDto> DuplicateKeys { get; init; } = [];
    public IReadOnlyList<WildcardDependencyAuditRowDto> WildcardDependencies { get; init; } = [];
    public StudentIntegrityAuditSummaryDto StudentIntegrity { get; init; } = new();
    public SchedulingIntegrityAuditSummaryDto SchedulingIntegrity { get; init; } = new();
    public AttendanceIntegrityAuditSummaryDto AttendanceIntegrity { get; init; } = new();
    public TgSectionBoundarySummaryDto TeachingGroupSectionBoundary { get; init; } = new();
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string RecommendedNextPrompt { get; init; } = "";
}

public sealed class SemesterSchemaHardeningFindingDto
{
    public string Code { get; init; } = "";
    public SemesterSchemaHardeningFindingSeverity Severity { get; init; }
    public string SeverityCode { get; init; } = "";
    public string Entity { get; init; } = "";
    public int? EntityId { get; init; }
    public int? TenantId { get; init; }
    public int? SemesterId { get; init; }
    public string CurrentState { get; init; } = "";
    public string ExpectedState { get; init; } = "";
    public string Reason { get; init; } = "";
    public string RequiredRemediation { get; init; } = "";
    public string OwningModule { get; init; } = "";
    public bool RequiresSeparateApprovedPrompt { get; init; } = true;
    public bool IsBlocking { get; init; } = true;
}

public sealed class DownstreamConsumerFindingDto
{
    public string Entity { get; init; } = "";
    public string RecordId { get; init; } = "";
    public int TenantId { get; init; }
    public int SemesterId { get; init; }
    public int? SemesterGroupId { get; init; }
    public int? EntityGroupId { get; init; }
    public int? ExpectedGroupId { get; init; }
    public DownstreamConsumerStatus Status { get; init; }
    public string StatusCode { get; init; } = "";
    public string Evidence { get; init; } = "";
}

public sealed class NullGroupSemesterAuditRowDto
{
    public int TenantId { get; init; }
    public int SemesterId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int CourseId { get; init; }
    public int? GroupId { get; init; }
    public NullGroupSemesterDisposition Disposition { get; init; }
    public string DispositionCode { get; init; } = "";
    public string Evidence { get; init; } = "";
    public int DownstreamReferenceCount { get; set; }
    public bool BlocksNotNull { get; init; } = true;
    public bool IsHistoricalArchive { get; init; }
}

public sealed class DownstreamLegacyReferenceRowDto
{
    public int TenantId { get; init; }
    public int SemesterId { get; init; }
    public int SemesterNumber { get; init; }
    public int CourseId { get; init; }
    public int? GroupId { get; init; }
    public string ReferenceEntity { get; init; } = "";
    public int ReferenceCount { get; init; }
    /// <summary>Entity primary keys as strings (supports int and Guid identities).</summary>
    public IReadOnlyList<string> ReferenceIds { get; init; } = [];
    public string Disposition { get; init; } = "";
    public string BlockingReason { get; init; } = "";
}

public sealed class DuplicateSemesterKeyRowDto
{
    public int TenantId { get; init; }
    public int GroupId { get; init; }
    public int Number { get; init; }
    public IReadOnlyList<int> SemesterIds { get; init; } = [];
}

public sealed class WildcardDependencyAuditRowDto
{
    public string Path { get; init; } = "";
    public string Location { get; init; } = "";
    public WildcardDependencyKind Kind { get; init; }
    public string KindCode { get; init; } = "";
    public string Notes { get; init; } = "";
    public bool BlocksHardening { get; init; }
    public string ClosureStatus { get; init; } = "CLOSED";
}

public sealed class StudentIntegrityAuditSummaryDto
{
    public int TotalAudited { get; init; }
    public int Valid { get; init; }
    public int Invalid { get; init; }
    public int Legacy { get; init; }
    public int OrphanedSemester { get; init; }
}

public sealed class SchedulingIntegrityAuditSummaryDto
{
    public int SubjectAllocationChecked { get; init; }
    public int SubjectAllocationInvalid { get; init; }
    public int TimetableEntryChecked { get; init; }
    public int TimetableEntryInvalid { get; init; }
    public int TimetableSectionChecked { get; init; }
    public int TimetableSectionInvalid { get; init; }
}

public sealed class AttendanceIntegrityAuditSummaryDto
{
    public int SessionsChecked { get; init; }
    public int SessionsInvalid { get; init; }
}

public sealed class TgSectionBoundarySummaryDto
{
    public TgSectionBoundaryClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = "SAFE_FOR_HARDENING";
    public int TeachingGroupLegacyRefs { get; init; }
    public int TeachingGroupSectionMismatches { get; init; }
    public int SectionLegacyRefs { get; init; }
    public int TimetableSectionLegacyRefs { get; init; }
    public string Notes { get; init; } = "Classify-only; no TG/Section mutation.";
}
