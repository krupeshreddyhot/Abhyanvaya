namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1B.7 — Immutable allocation context. Sole input model for AI29.1C.
/// No setters; construct via init / builder only.
/// </summary>
public sealed class SectionAllocationContext
{
    public const string CurrentSchemaVersion = "1.0.0";

    public Guid ContextId { get; init; }
    public string ContextVersion { get; init; } = "1";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTime GeneratedAt { get; init; }
    public string Checksum { get; init; } = "";

    public AllocationHierarchyProjection Hierarchy { get; init; } = new();
    public IReadOnlyList<AllocationSectionProjection> Sections { get; init; } = [];
    public IReadOnlyList<AllocationCapacityProjection> Capacities { get; init; } = [];
    public IReadOnlyList<AllocationStudentProjection> Students { get; init; } = [];
    public IReadOnlyList<AllocationFacultyProjection> FacultyAssignments { get; init; } = [];
    public IReadOnlyList<AllocationSubjectProjection> SubjectAssignments { get; init; } = [];
    public IReadOnlyList<AllocationRoomProjection> RoomAvailability { get; init; } = [];
    public IReadOnlyList<string> Policies { get; init; } = [];
    public IReadOnlyList<string> Recommendations { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();

    public string OverallHealth { get; init; } = "Healthy";
    public string OverallReadiness { get; init; } = "Ready";
    public string TimetableStatus { get; init; } = "Unknown";
}

/// <summary>Analysis context — never used by allocation execution.</summary>
public sealed class SectionAllocationAnalysisContext
{
    public SectionAllocationContext Context { get; init; } = new();
    public IReadOnlyList<AllocationHistoryPoint> CapacityHistory { get; init; } = [];
    public IReadOnlyList<AllocationHistoryPoint> LifecycleHistory { get; init; } = [];
    public IReadOnlyList<AllocationHistoryPoint> MergeHistory { get; init; } = [];
    public IReadOnlyList<AllocationHistoryPoint> SplitHistory { get; init; } = [];
    public IReadOnlyList<AllocationHistoryPoint> VersionHistory { get; init; } = [];
    public IReadOnlyList<AllocationOccupancyTrendPoint> OccupancyTrend { get; init; } = [];
    public IReadOnlyList<string> Forecast { get; init; } = [];
    public IReadOnlyList<string> Recommendations { get; init; } = [];
    public IReadOnlyDictionary<string, string> Analytics { get; init; }
        = new Dictionary<string, string>();
}

public sealed class AllocationHierarchyProjection
{
    public int AcademicYearId { get; init; }
    public string? AcademicYearName { get; init; }
    public int? ProgramId { get; init; }
    public string? ProgramName { get; init; }
    public int CourseId { get; init; }
    public string? CourseName { get; init; }
    public int GroupId { get; init; }
    public string? GroupName { get; init; }
    public int SemesterId { get; init; }
    public string? SemesterName { get; init; }
}

/// <summary>Immutable read model — no business logic.</summary>
public sealed class AllocationSectionProjection
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string SectionType { get; init; } = "";
    public string Lifecycle { get; init; } = "";
    public string Health { get; init; } = "";
    public string Readiness { get; init; } = "";
    /// <summary>AI29.1D.24B.4A — Authoritative academic sort key from Section.DisplayOrder.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>Immutable read model — no business logic.</summary>
public sealed class AllocationCapacityProjection
{
    public int SectionId { get; init; }
    public int MaximumCapacity { get; init; }
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int CurrentStrength { get; init; }
    public int AvailableCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingList { get; init; }
    public double OccupancyPercent { get; init; }
    public string CapacityStatus { get; init; } = "";
}

/// <summary>Immutable read model — no business logic.</summary>
public sealed class AllocationStudentProjection
{
    public int StudentId { get; init; }
    public string? StudentNumber { get; init; }
    public string? StudentName { get; init; }
    public int? CurrentSectionId { get; init; }
    public string? CurrentSectionCode { get; init; }

    /// <summary>AI29.1D population filter facets (read-only; may be null when not in domain).</summary>
    public int? GenderId { get; init; }
    public string? Gender { get; init; }
    public int? LanguageId { get; init; }
    public string? Language { get; init; }
    public string? ScholarshipCategory { get; init; }
    public string? MinorSubject { get; init; }
    public string? TransportRoute { get; init; }
    public string? Hostel { get; init; }
    public string? ElectiveCombination { get; init; }
    public string? Merit { get; init; }
}

/// <summary>Immutable read model — no business logic.</summary>
public sealed class AllocationFacultyProjection
{
    public int FacultyId { get; init; }
    public string? FacultyName { get; init; }
    public int SectionId { get; init; }
    public string Role { get; init; } = "";
}

public sealed class AllocationSubjectProjection
{
    public int SubjectId { get; init; }
    public string? SubjectCode { get; init; }
    public string? SubjectName { get; init; }
    public int CourseId { get; init; }
    public int SemesterId { get; init; }
}

public sealed class AllocationRoomProjection
{
    public int? RoomId { get; init; }
    public string? RoomCode { get; init; }
    public int TimetableMappingCount { get; init; }
    public string Status { get; init; } = "Unknown";
}

public sealed class AllocationHistoryPoint
{
    public DateTime At { get; init; }
    public string Kind { get; init; } = "";
    public string Summary { get; init; } = "";
}

public sealed class AllocationOccupancyTrendPoint
{
    public DateOnly Date { get; init; }
    public double AverageOccupancyPercent { get; init; }
    public int TotalCurrentStrength { get; init; }
}

public sealed class AllocationValidationReport
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Checks { get; init; } = [];
}

public sealed class AllocationReadinessReport
{
    public string OverallStatus { get; init; } = "Ready";
    public IReadOnlyList<AllocationReadinessCheck> Checks { get; init; } = [];
}

public sealed class AllocationReadinessCheck
{
    public string Area { get; init; } = "";
    public string Status { get; init; } = "Ready";
    public string Message { get; init; } = "";
}

public sealed class AllocationHealthReport
{
    public string OverallStatus { get; init; } = "Healthy";
    public IReadOnlyList<AllocationHealthDimension> Dimensions { get; init; } = [];
}

public sealed class AllocationHealthDimension
{
    public string Area { get; init; } = "";
    public string Status { get; init; } = "Healthy";
    public string Message { get; init; } = "";
}

public sealed class AllocationContextCompositionReport
{
    public Guid ContextId { get; init; }
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<AllocationCompositionStep> Steps { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public double TotalDurationMs { get; init; }
}

public sealed class AllocationCompositionStep
{
    public string Service { get; init; } = "";
    public double DurationMs { get; init; }
    public string Outcome { get; init; } = "Ok";
    public string? Detail { get; init; }
}

public sealed class AllocationSnapshotDto
{
    public Guid SnapshotId { get; init; }
    public string ContextVersion { get; init; } = "";
    public string SchemaVersion { get; init; } = "";
    public string Checksum { get; init; } = "";
    public DateTime GeneratedDate { get; init; }
    public int? GeneratedBy { get; init; }
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
}

public sealed class AllocationArchitectureReport
{
    public DateTime GeneratedUtc { get; init; }
    public bool Passed { get; init; }
    public IReadOnlyList<string> Checks { get; init; } = [];
    public IReadOnlyList<string> Violations { get; init; } = [];
}

public sealed class AllocationScopeRequest
{
    public int AcademicYearId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
}
