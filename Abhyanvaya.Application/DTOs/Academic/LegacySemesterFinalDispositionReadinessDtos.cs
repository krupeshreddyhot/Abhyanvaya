namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (Architect package 3I2) / PromptCode P1-4-3N —
/// Legacy Semester final disposition + schema hardening readiness gate (read-only).
/// PromptCode avoids colliding with Finance Section remediation (P1-4-3I).
/// </summary>
public enum FinalLegacySemesterDisposition
{
    FinalizedGroupSpecific = 1,
    RetainHistorical = 2,
    ManualMappingRequired = 3,
    DuplicateReview = 4,
    BlockedByReference = 5,
    BlockedByArchitecturalBoundary = 6,
}

public sealed class LegacySemesterFinalDispositionReadinessResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public string PromptCode { get; init; } = "P1-4-3N";
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public bool SchemaHardeningReady { get; init; }
    public bool IsReady { get; init; }

    public bool NullGroupReady { get; init; }
    public bool UniqueKeyReady { get; init; }
    public bool StudentIntegrityReady { get; init; }
    public bool DownstreamReferenceReady { get; init; }
    public bool TeachingGroupBoundaryReady { get; init; }
    public bool TenantIsolationReady { get; init; }
    public bool WildcardDependencyReady { get; init; }
    public bool WritePathReady { get; init; }
    public bool MigrationSafetyReady { get; init; }

    public FinalDispositionEvidenceCountsDto EvidenceCounts { get; init; } = new();
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<FinalLegacySemesterDispositionRowDto> LegacySemesters { get; init; } = [];
    public IReadOnlyList<DuplicateSemesterKeyRowDto> DuplicateKeys { get; init; } = [];
    public IReadOnlyList<FinalOutstandingReferenceDto> OutstandingReferences { get; init; } = [];
    public IReadOnlyList<WildcardDependencyAuditRowDto> WildcardDependencies { get; init; } = [];
    public SchemaHardeningMigrationContractDto? NextMigrationContract { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string RecommendedNextPrompt { get; init; } = "";
}

public sealed class FinalDispositionEvidenceCountsDto
{
    public int TotalSemesters { get; init; }
    public int NullGroupSemesters { get; init; }
    public int GroupSpecificSemesters { get; init; }
    public int DuplicateKeyGroups { get; init; }
    public int OrphanedSemesterReferenceSamples { get; init; }
    public int CrossCourseSemesterRefs { get; init; }
    public int CrossGroupSemesterRefs { get; init; }
    public int CrossTenantViolations { get; init; }
    public int StudentLegacyRefs { get; init; }
    public int AttendanceLegacyRefs { get; init; }
    public int SubjectLegacyRefs { get; init; }
    public int SectionLegacyRefs { get; init; }
    public int SubjectAllocationLegacyRefs { get; init; }
    public int TimetableEntryLegacyRefs { get; init; }
    public int TeachingGroupLegacyRefs { get; init; }
    public int TeachingGroupSectionLegacyRefs { get; init; }
    public int TimetableSectionLegacyRefs { get; init; }
    public int StudentIntegrityViolations { get; init; }
    public int ActiveWildcardDependencies { get; init; }
}

public sealed class FinalLegacySemesterDispositionRowDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int? CurrentGroupId { get; init; }
    public int TenantId { get; init; }
    public FinalLegacySemesterDisposition Disposition { get; init; }
    public string DispositionCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public string? BlockingDependency { get; init; }
    public int? ProposedTargetGroupId { get; init; }
    public bool MutationPermitted { get; init; }
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public int TeachingGroupSectionRefs { get; init; }
    public int TimetableSectionRefs { get; init; }
    public IReadOnlyList<string> DependentEntities { get; init; } = [];
}

public sealed class FinalOutstandingReferenceDto
{
    public string EntityType { get; init; } = "";
    public int EntityId { get; init; }
    public int SemesterId { get; init; }
    public string Classification { get; init; } = ""; // valid_group_specific | historical | blocked | unresolved
    public string Notes { get; init; } = "";
}

public sealed class SchemaHardeningMigrationContractDto
{
    public bool AuthorizedForExecution { get; init; }
    public string Title { get; init; } = "P1-4 Prompt 3J — Schema Hardening Execution";
    public IReadOnlyList<string> Steps { get; init; } = [];
    public string RollbackStrategy { get; init; } = "";
    public string FailureBehavior { get; init; } = "";
    public string Notes { get; init; } = "";
}
