namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionVersionDto
{
    public int Id { get; init; }
    public int SectionId { get; init; }
    public int VersionNumber { get; init; }
    public DateTime VersionDate { get; init; }
    public int? ChangedBy { get; init; }
    public string? Reason { get; init; }
    public string Operation { get; init; } = "";
    public int? PreviousVersionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string Status { get; init; } = "";
    public string SectionTypeCode { get; init; } = "";
    public int MaximumCapacity { get; init; }
    public int MinimumCapacity { get; init; }
    public int RecommendedCapacity { get; init; }
    public int ReservedSeats { get; init; }
    public int WaitingListCount { get; init; }
    public int CurrentStrength { get; init; }
    public double OccupancyPercent { get; init; }
}

public sealed class SectionCapacityHistoryDto
{
    public int Id { get; init; }
    public int SectionId { get; init; }
    public int MaximumCapacity { get; init; }
    public int MinimumCapacity { get; init; }
    public int CurrentStrength { get; init; }
    public int ReservedSeats { get; init; }
    public double OccupancyPercent { get; init; }
    public DateTime RecordedDate { get; init; }
    public string? Reason { get; init; }
}

public sealed class SectionTimelineEventDto
{
    public DateTime Timestamp { get; init; }
    public int? UserId { get; init; }
    public string Operation { get; init; } = "";
    public string EventKind { get; init; } = "";
    public string? Notes { get; init; }
    public string? FromStatus { get; init; }
    public string? ToStatus { get; init; }
    public int? VersionNumber { get; init; }
}

public sealed class MergePreviewEngineDto
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public int TargetSectionId { get; init; }
    public int CurrentCapacityTotal { get; init; }
    public int MergedCapacity { get; init; }
    public int CombinedStudentCount { get; init; }
    public int CombinedFacultyCount { get; init; }
    public int TimetableMappingCount { get; init; }
    public int AttendanceSessionLinkCount { get; init; }
    public string? TargetReadiness { get; init; }
    public IReadOnlyList<string> ReadinessNotes { get; init; } = [];
}

public sealed class SplitPreviewEngineDto
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int SourceSectionId { get; init; }
    public string SourceSectionCode { get; init; } = "";
    public int SourceStudentCount { get; init; }
    public int SourceCapacity { get; init; }
    public int SourceFacultyCount { get; init; }
    public int TimetableMappingCount { get; init; }
    public IReadOnlyList<SplitPreviewChildDto> ProposedChildren { get; init; } = [];
}

public sealed class SplitPreviewChildDto
{
    public string ProposedCode { get; init; } = "";
    public string ProposedName { get; init; } = "";
    public int ProposedCapacity { get; init; }
    public int ExpectedStudentCount { get; init; }
    public string FacultyImpact { get; init; } = "Unassigned — allocation deferred";
    public string RoomImpact { get; init; } = "No room auto-assignment";
}

public sealed class SectionPolicyDto
{
    public int Id { get; init; }
    public string ScopeLevel { get; init; } = "Tenant";
    public int? ProgramId { get; init; }
    public int? CourseId { get; init; }
    public string? SectionTypeCode { get; init; }
    public int? MaximumCapacity { get; init; }
    public int? MinimumCapacity { get; init; }
    public int? RecommendedCapacity { get; init; }
    public int? MaximumCombinedSections { get; init; }
    public int? MaximumFaculty { get; init; }
    public int? MaximumRoomOccupancy { get; init; }
    public bool? AllowMerge { get; init; }
    public bool? AllowSplit { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpsertSectionPolicyRequest
{
    public string ScopeLevel { get; init; } = "Tenant";
    public int? ProgramId { get; init; }
    public int? CourseId { get; init; }
    public string? SectionTypeCode { get; init; }
    public int? MaximumCapacity { get; init; }
    public int? MinimumCapacity { get; init; }
    public int? RecommendedCapacity { get; init; }
    public int? MaximumCombinedSections { get; init; }
    public int? MaximumFaculty { get; init; }
    public int? MaximumRoomOccupancy { get; init; }
    public bool? AllowMerge { get; init; }
    public bool? AllowSplit { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed class ResolvedSectionPolicyDto
{
    public int SectionId { get; init; }
    public int? MaximumCapacity { get; init; }
    public int? MinimumCapacity { get; init; }
    public int? RecommendedCapacity { get; init; }
    public int? MaximumCombinedSections { get; init; }
    public int? MaximumFaculty { get; init; }
    public int? MaximumRoomOccupancy { get; init; }
    public bool AllowMerge { get; init; } = true;
    public bool AllowSplit { get; init; } = true;
    public IReadOnlyList<string> AppliedScopeChain { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class SectionCapacityRecommendationDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    /// <summary>IncreaseCapacity | DecreaseCapacity | MergeCandidate | SplitCandidate | Healthy</summary>
    public string Recommendation { get; init; } = "Healthy";
    public string Rationale { get; init; } = "";
    public double OccupancyPercent { get; init; }
}

public sealed class SectionHealthReportDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    /// <summary>Healthy | Warning | Critical</summary>
    public string OverallStatus { get; init; } = "Healthy";
    public IReadOnlyList<SectionHealthDimensionDto> Dimensions { get; init; } = [];
}

public sealed class SectionHealthDimensionDto
{
    public string Area { get; init; } = "";
    public string Status { get; init; } = "Healthy";
    public string Message { get; init; } = "";
}

public sealed class SectionArchitectureReportDto
{
    public DateTime GeneratedUtc { get; init; }
    public bool Passed { get; init; }
    public IReadOnlyList<string> Checks { get; init; } = [];
    public IReadOnlyList<string> Violations { get; init; } = [];
}
