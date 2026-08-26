namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2B — legacy Semester classification.</summary>
public enum LegacySemesterClassification
{
    DeterministicSingleGroup = 1,
    ExplicitExistingGroupReference = 2,
    AmbiguousMultiGroup = 3,
    OrphanNoGroup = 4,
    InvalidData = 5,
    AlreadyGroupSpecific = 6,
}

/// <summary>Controlled migration action recommendation (read-only; no execution).</summary>
public enum LegacySemesterMigrationAction
{
    MapSingleGroup = 1,
    SplitRequired = 2,
    ManualMappingRequired = 3,
    OrphanReviewRequired = 4,
    InvalidDataReview = 5,
    AlreadyGroupSpecific = 6,
    NoAction = 7,
}

public sealed class LegacySemesterCandidateGroupDto
{
    public int GroupId { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int StudentReferenceCount { get; init; }
}

public sealed class LegacySemesterMigrationRowDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = null!;
    public int Number { get; init; }
    public string Name { get; init; } = null!;
    public int? CurrentGroupId { get; init; }
    public string? CurrentGroupName { get; init; }
    public IReadOnlyList<LegacySemesterCandidateGroupDto> CandidateGroups { get; init; } = [];
    public LegacySemesterClassification Classification { get; init; }
    public string ClassificationCode { get; init; } = null!;
    public int StudentReferenceCount { get; init; }
    public int AttendanceReferenceCount { get; init; }
    public int SubjectAllocationReferenceCount { get; init; }
    public int TimetableEntryReferenceCount { get; init; }
    public int SubjectReferenceCount { get; init; }
    public int SectionReferenceCount { get; init; }
    public int TeachingGroupReferenceCount { get; init; }
    public bool HasDuplicateLegacyNumberOnCourse { get; init; }
    public LegacySemesterMigrationAction MigrationAction { get; init; }
    public string MigrationActionCode { get; init; } = null!;
    public string Reason { get; init; } = null!;
}

public sealed class LegacySemesterMigrationAuditSummaryDto
{
    public int TotalSemesters { get; init; }
    public int LegacyNullGroupCount { get; init; }
    public int GroupSpecificCount { get; init; }
    public int MapSingleGroupCount { get; init; }
    public int SplitRequiredCount { get; init; }
    public int ManualMappingRequiredCount { get; init; }
    public int OrphanReviewRequiredCount { get; init; }
    public int InvalidDataReviewCount { get; init; }
    public int AlreadyGroupSpecificCount { get; init; }
    public int DuplicateLegacyNumberCourseKeys { get; init; }
    public bool HasMigrationBlockers { get; init; }
}

public sealed class LegacySemesterMigrationAuditReportDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public LegacySemesterMigrationAuditSummaryDto Summary { get; init; } = null!;
    public IReadOnlyList<LegacySemesterMigrationRowDto> Rows { get; init; } = [];
}
