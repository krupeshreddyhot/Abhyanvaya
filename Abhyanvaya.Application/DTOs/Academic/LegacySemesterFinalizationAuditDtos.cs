namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3D — legacy finalization disposition (read-only).</summary>
public enum LegacySemesterFinalizationDisposition
{
    AlreadyGroupSpecific = 1,
    SafeSingleGroupMapping = 2,
    SplitRequired = 3,
    ManualMappingRequired = 4,
    DuplicateReview = 5,
    HistoricalRetain = 6,
    BlockedByTeachingGroupReference = 7,
    UnknownRequiresArchitectDecision = 8,
}

public enum TeachingGroupResidualRecommendation
{
    SafeForSeparateTgRemediation = 1,
    RequiresManualReview = 2,
    Blocked = 3,
}

public enum NullWildcardDependencyAction
{
    Remove = 1,
    ReplaceWithGroupScope = 2,
    HistoricalReadOnly = 3,
    SafeToDeprecate = 4,
    RequiresReview = 5,
}

public sealed class LegacySemesterFinalizationAuditDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public LegacySemesterFinalizationSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<LegacySemesterInventoryRowDto> LegacySemesters { get; init; } = [];
    public IReadOnlyList<TeachingGroupResidualReferenceDto> TeachingGroupResiduals { get; init; } = [];
    public IReadOnlyList<DuplicateGroupSemesterNumberDto> DuplicateGroupSemesterNumbers { get; init; } = [];
    public IReadOnlyList<NullWildcardDependencyDto> NullWildcardDependencies { get; init; } = [];
    public StudentSemesterIntegritySummaryDto StudentIntegrity { get; init; } = new();
    public DownstreamLegacyReferenceSummaryDto DownstreamLegacyReferences { get; init; } = new();
    public DatabaseHardeningPreconditionDto HardeningPreconditions { get; init; } = new();
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class LegacySemesterFinalizationSummaryDto
{
    public int LegacyNullGroupCount { get; init; }
    public int TeachingGroupResidualCount { get; init; }
    public int DuplicateGroupNumberKeys { get; init; }
    public int StudentIntegrityViolations { get; init; }
    public int AttendanceLegacyRefs { get; init; }
    public int SubjectAllocationLegacyRefs { get; init; }
    public int TimetableEntryLegacyRefs { get; init; }
    public bool NotNullReady { get; init; }
    public bool UniqueConstraintReady { get; init; }
}

public sealed class LegacySemesterInventoryRowDto
{
    public int TenantId { get; init; }
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseCode { get; init; } = "";
    public string CourseName { get; init; } = "";
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public bool IsDeleted { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public int ActiveGroupCountOnCourse { get; init; }
    public IReadOnlyList<LegacyFinalizationGroupInfoDto> GroupsOnCourse { get; init; } = [];
    public int StudentReferenceCount { get; init; }
    public int AttendanceReferenceCount { get; init; }
    public int SubjectAllocationReferenceCount { get; init; }
    public int TimetableEntryReferenceCount { get; init; }
    public int TeachingGroupReferenceCount { get; init; }
    public int SubjectReferenceCount { get; init; }
    public int SectionReferenceCount { get; init; }
    public string? Prompt2BClassification { get; init; }
    public string? Prompt3ADecision { get; init; }
    public LegacySemesterFinalizationDisposition Disposition { get; init; }
    public string DispositionCode { get; init; } = "";
    public string DispositionEvidence { get; init; } = "";
}

public sealed class LegacyFinalizationGroupInfoDto
{
    public int GroupId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class TeachingGroupResidualReferenceDto
{
    public int TeachingGroupId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = "";
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int LegacySemesterId { get; init; }
    public int LegacySemesterNumber { get; init; }
    public int TeachingGroupSectionCount { get; init; }
    public int TimetableEntryCountUsingTg { get; init; }
    public int? CandidateTargetSemesterId { get; init; }
    public bool CandidateIsDeterministic { get; init; }
    public TeachingGroupResidualRecommendation Recommendation { get; init; }
    public string RecommendationCode { get; init; } = "";
    public string Evidence { get; init; } = "";
    public bool NoMutationPerformed { get; init; } = true;
}

public sealed class DuplicateGroupSemesterNumberDto
{
    public int TenantId { get; init; }
    public int GroupId { get; init; }
    public int Number { get; init; }
    public IReadOnlyList<int> SemesterIds { get; init; } = [];
    public string RemediationPlan { get; init; } = "";
}

public sealed class NullWildcardDependencyDto
{
    public string Path { get; init; } = "";
    public string Location { get; init; } = "";
    public NullWildcardDependencyAction Action { get; init; }
    public string ActionCode { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed class StudentSemesterIntegritySummaryDto
{
    public int StudentsChecked { get; init; }
    public int Violations { get; init; }
    public IReadOnlyList<string> SampleViolationMessages { get; init; } = [];
}

public sealed class DownstreamLegacyReferenceSummaryDto
{
    public int LegacySemesterIiiId { get; init; }
    public int Attendance { get; init; }
    public int SubjectAllocation { get; init; }
    public int TimetableEntry { get; init; }
    public int TeachingGroup { get; init; }
    public int Subject { get; init; }
    public int Section { get; init; }
}

public sealed class DatabaseHardeningPreconditionDto
{
    public bool ZeroNullGroupSemesters { get; init; }
    public bool AllLegacyHaveExplicitDisposition { get; init; }
    public bool ZeroTeachingGroupOnLegacy { get; init; }
    public bool ZeroAttendanceOnLegacyNull { get; init; }
    public bool ZeroSaOnLegacyNull { get; init; }
    public bool ZeroTtOnLegacyNull { get; init; }
    public bool ZeroStudentOnLegacyNull { get; init; }
    public bool ZeroDuplicateGroupNumber { get; init; }
    public bool CourseGroupOwnershipConsistent { get; init; }
    public bool StudentIntegrityClean { get; init; }
    public bool WritePathsRequireGroupId { get; init; }
    public bool WildcardDependenciesDeprecated { get; init; }
    public bool RollbackStrategyDocumented { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public bool NotNullMayProceed => BlockingReasons.Count == 0;
    public bool UniqueMayProceed => ZeroDuplicateGroupNumber && NotNullMayProceed;
}
