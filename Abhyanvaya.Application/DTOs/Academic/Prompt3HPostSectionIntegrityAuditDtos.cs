namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H — post-3G integrity &amp; schema readiness (read-only).</summary>
public enum Prompt3HLegacySemesterClassification
{
    RetainHistorical = 1,
    ManualMappingRequired = 2,
    DuplicateReview = 3,
    BlockedByTeachingGroupReference = 4,
    ReadyForRetirement = 5,
    ReadyForGroupAssignment = 6,
    /// <summary>Prompt 3H contract — operational downstream refs remain (Student/Att/Section/SA/TT/TG).</summary>
    BlockedByDownstreamReference = 7,
    /// <summary>Prompt 3H contract alias for ReadyForGroupAssignment.</summary>
    SafeForGroupMapping = 8,
    /// <summary>Prompt 3H contract — zero refs; Architect may archive/retire (not deleted in this prompt).</summary>
    ObsoleteCandidate = 9,
}

public enum Prompt3HHardeningDecision
{
    Ready = 1,
    NotReady = 2,
    Blocked = 3,
}

public enum Prompt3HTgResidualClassification
{
    Safe = 1,
    Blocked = 2,
    ManualReviewRequired = 3,
}

public enum Prompt3HWildcardDependencyClassification
{
    ActiveRuntimeDependency = 1,
    LegacyReadOnlyCompatibility = 2,
    SafeToRemove = 3,
    RequiresFollowup = 4,
}

public enum Prompt3HEntityRefStatus
{
    Healthy = 1,
    AlreadyCorrect = 2,
    RemediatedByPrompt3G = 3,
    LegacyNullGroupReference = 4,
    Incompatible = 5,
    Ambiguous = 6,
    BlockedByTeachingGroupBoundary = 7,
    Unresolved = 8,
}

public sealed class Prompt3HPostSectionIntegrityAuditDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public string PromptCode { get; init; } = "P1-4-3H";

    /// <summary>Aggregate health: no incompatible Student/Section/Attendance/SA/TT refs and Prompt 3G verified.</summary>
    public bool IsHealthy { get; init; }
    public int CriticalCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }

    public Prompt3GVerificationDto Prompt3GVerification { get; init; } = new();
    public Prompt3HSemesterInventoryDto SemesterInventory { get; init; } = new();
    public Prompt3HEntityIntegrityDto Students { get; init; } = new();
    public Prompt3HEntityIntegrityDto Attendance { get; init; } = new();
    public Prompt3HEntityIntegrityDto Subjects { get; init; } = new();
    public Prompt3HEntityIntegrityDto Sections { get; init; } = new();
    public Prompt3HEntityIntegrityDto SubjectAllocations { get; init; } = new();
    public Prompt3HEntityIntegrityDto TimetableEntries { get; init; } = new();
    public Prompt3HTeachingGroupIntegrityDto TeachingGroups { get; init; } = new();
    public Prompt3HTeachingGroupSectionIntegrityDto TeachingGroupSections { get; init; } = new();
    public Prompt3HTimetableSectionOwnershipDto TimetableSections { get; init; } = new();
    public Prompt3HProgramOptionalityDto ProgramOptionality { get; init; } = new();
    public Prompt3HDepartmentSsotDto DepartmentSsot { get; init; } = new();
    public Prompt3HTenantIsolationDto TenantIsolation { get; init; } = new();
    public IReadOnlyList<Prompt3HLegacyClassificationRowDto> LegacyClassifications { get; init; } = [];
    public IReadOnlyList<Prompt3HWildcardDependencyStatusDto> WildcardDependencyStatus { get; init; } = [];
    public IReadOnlyList<NullWildcardDependencyDto> WildcardDependencies { get; init; } = [];
    public Prompt3HSchemaHardeningReadinessDto SchemaHardening { get; init; } = new();
    public SemesterPostMigrationIntegrityAuditDto? EmbeddedIntegrityAudit { get; init; }
    public LegacySemesterFinalizationAuditDto? EmbeddedFinalizationAudit { get; init; }
    public IReadOnlyList<string> ExactBlockers { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string RecommendedNextStep { get; init; } = "";

    // Prompt contract aliases (populated from SchemaHardening).
    public bool CanMakeGroupIdNotNull { get; init; }
    public bool CanAddGroupSemesterUniqueConstraint { get; init; }
    public bool CanRemoveLegacyWildcardSemantics { get; init; }
    public bool DownstreamReady { get; init; }
    public bool TenantIsolationReady { get; init; }
    public bool StudentIntegrityReady { get; init; }
    public bool SectionIntegrityReady { get; init; }
    public bool TeachingGroupBoundaryReady { get; init; }
    public Prompt3HHardeningDecision SemesterHardeningReady { get; init; } = Prompt3HHardeningDecision.NotReady;
    public string SemesterHardeningReadyCode { get; init; } = "NOT_READY";
}

public sealed class Prompt3GVerificationDto
{
    public bool JournalEvidenceFound { get; init; }
    public IReadOnlyList<int> JournaledSectionIds { get; init; } = [];
    public IReadOnlyList<int> RemediatedOnTargetSemester { get; init; } = [];
    public IReadOnlyList<int> StillOnLegacySemester { get; init; } = [];
    public IReadOnlyList<int> AlreadyCorrectOnTarget { get; init; } = [];
    public IReadOnlyList<int> FinanceResidualOnLegacy { get; init; } = [];
    public int ExpectedLegacySemesterId { get; init; } = 3;
    public int ExpectedTargetSemesterId { get; init; } = 11;
    public bool Prompt3GContractSatisfied { get; init; }
    public string Evidence { get; init; } = "";
}

public sealed class Prompt3HSemesterInventoryDto
{
    public int TotalSemesters { get; init; }
    public int NullGroupIdCount { get; init; }
    public int GroupSpecificCount { get; init; }
    public int CourseGroupMismatchCount { get; init; }
    public int DuplicateGroupNumberCandidateCount { get; init; }
    public int HistoricalRetainedCount { get; init; }
    public int AmbiguousLegacyCount { get; init; }
    public IReadOnlyList<int> NullGroupSemesterIds { get; init; } = [];
    public IReadOnlyList<int> CourseGroupMismatchSemesterIds { get; init; } = [];
    public IReadOnlyList<DuplicateGroupSemesterNumberDto> DuplicateKeys { get; init; } = [];
}

public sealed class Prompt3HEntityIntegrityDto
{
    public string EntityType { get; init; } = "";
    public int TotalChecked { get; init; }
    public int HealthyCount { get; init; }
    public int LegacyNullGroupRefs { get; init; }
    public int IncompatibleRefs { get; init; }
    public int UnresolvedRefs { get; init; }
    public int RemediationCandidates { get; init; }
    public IReadOnlyList<Prompt3HEntityRefSampleDto> Samples { get; init; } = [];
}

public sealed class Prompt3HEntityRefSampleDto
{
    public string EntityKey { get; init; } = "";
    public int? SemesterId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public Prompt3HEntityRefStatus Status { get; init; }
    public string StatusCode { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed class Prompt3HTeachingGroupIntegrityDto
{
    public int TotalChecked { get; init; }
    public int OnGroupSpecificSemester { get; init; }
    public int LegacyNullGroupRefs { get; init; }
    public int IncompatibleRefs { get; init; }
    public IReadOnlyList<Prompt3HEntityRefSampleDto> Samples { get; init; } = [];
    public IReadOnlyList<Prompt3HTgResidualRowDto> Residuals { get; init; } = [];
    public string ClassificationOnlyNote { get; init; } =
        "Teaching Groups are classify-only in Prompt 3H; no TG mutation.";
}

public sealed class Prompt3HTgResidualRowDto
{
    public int TeachingGroupId { get; init; }
    public int SemesterId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public Prompt3HTgResidualClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = "";
    public string Evidence { get; init; } = "";
}

public sealed class Prompt3HTeachingGroupSectionIntegrityDto
{
    public int TotalLinksChecked { get; init; }
    public int CompatibleCount { get; init; }
    public int IncompatibleCount { get; init; }
    public IReadOnlyList<Prompt3HTgsCompatibilitySampleDto> Samples { get; init; } = [];
    public string ClassificationOnlyNote { get; init; } =
        "TeachingGroupSection classify-only; no mutation.";
}

public sealed class Prompt3HTgsCompatibilitySampleDto
{
    public int TeachingGroupId { get; init; }
    public int SectionId { get; init; }
    public int TeachingGroupSemesterId { get; init; }
    public int SectionSemesterId { get; init; }
    public bool IsCompatible { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class Prompt3HTimetableSectionOwnershipDto
{
    public int RowCount { get; init; }
    public bool ProjectorOwnedConfirmed { get; init; } = true;
    public bool DirectWriterAbsentInThisPrompt { get; init; } = true;
    public string Notes { get; init; } =
        "TimetableSection audited only; Prompt 3H does not write TimetableSection.";
}

public sealed class Prompt3HLegacyClassificationRowDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int? GroupId { get; init; }
    public Prompt3HLegacySemesterClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = "";
    public string Evidence { get; init; } = "";
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public bool BlocksSchemaHardening { get; init; }
    public string? Prompt3DDispositionCode { get; init; }
}

public sealed class Prompt3HWildcardDependencyStatusDto
{
    public string Path { get; init; } = "";
    public string Location { get; init; } = "";
    public Prompt3HWildcardDependencyClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed class Prompt3HSchemaHardeningReadinessDto
{
    public bool NotNullReady { get; init; }
    public string NotNullVerdict { get; init; } = "NOT READY";
    public IReadOnlyList<string> NotNullBlockers { get; init; } = [];
    public bool UniqueReady { get; init; }
    public string UniqueVerdict { get; init; } = "NOT READY";
    public IReadOnlyList<string> UniqueBlockers { get; init; } = [];
    public bool DownstreamReady { get; init; }
    public bool TenantIsolationReady { get; init; }
    public bool StudentIntegrityReady { get; init; }
    public bool SectionIntegrityReady { get; init; }
    public bool TeachingGroupBoundaryReady { get; init; }
    public Prompt3HHardeningDecision SemesterHardeningReady { get; init; } = Prompt3HHardeningDecision.NotReady;
    public string SemesterHardeningReadyCode { get; init; } = "NOT_READY";
    public string HistoricalNullPreservationNote { get; init; } = "";
    public bool SchemaHardeningPromptSafeToBegin { get; init; }

    /// <summary>Contract alias for NotNullReady.</summary>
    public bool CanMakeGroupIdNotNull { get; init; }
    /// <summary>Contract alias for UniqueReady under approved NULL strategy.</summary>
    public bool CanAddGroupSemesterUniqueConstraint { get; init; }
    /// <summary>True only when zero catalogued NULL-group wildcard dependency sites remain.</summary>
    public bool CanRemoveLegacyWildcardSemantics { get; init; }
}

public sealed class Prompt3HProgramOptionalityDto
{
    public bool EnablePrograms { get; init; }
    public bool ProgramRemainsOptional { get; init; } = true;
    public bool CourseDepartmentIdMandatory { get; init; } = true;
    public int CoursesMissingDepartmentId { get; init; }
    public string Notes { get; init; } =
        "Prompt 3H does not modify Program behavior; EnablePrograms continues to gate Program requirement.";
}

public sealed class Prompt3HDepartmentSsotDto
{
    public int SubjectAllocationsChecked { get; init; }
    public int SubjectAllocationDepartmentMismatches { get; init; }
    public int TimetableEntriesChecked { get; init; }
    public int TimetableEntryDepartmentMismatches { get; init; }
    public bool CourseDepartmentSsotIntact { get; init; } = true;
    public string Notes { get; init; } =
        "P1-3 Course.DepartmentId remains catalog SSOT; SA/TT DepartmentId are denormalized.";
}

public sealed class Prompt3HTenantIsolationDto
{
    public bool Passed { get; init; }
    public int CrossTenantSemesterRefs { get; init; }
    public int CrossTenantSectionRefs { get; init; }
    public int CrossTenantStudentRefs { get; init; }
    public IReadOnlyList<string> Findings { get; init; } = [];
}
