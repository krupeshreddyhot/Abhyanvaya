namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>AI30 Phase 3.5 — configuration readiness section status.</summary>
public enum SchedulingConfigSectionStatus
{
    Complete = 0,
    Partial = 1,
    Missing = 2,
    Blocked = 3,
    Optional = 4,
    Required = 5
}

public sealed class SchedulingReadinessSectionDto
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "Missing";
    public double PercentComplete { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = [];
    public IReadOnlyList<string> MissingItems { get; init; } = [];
    public IReadOnlyList<string> BlockedBy { get; init; } = [];
}

public sealed class SchedulingModuleStatusDto
{
    public string ModuleKey { get; init; } = "";
    public string Path { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "Optional";
    public string Tooltip { get; init; } = "";
    public IReadOnlyList<string> Requires { get; init; } = [];
    public IReadOnlyList<string> UsedBy { get; init; } = [];
    public IReadOnlyList<string> RelatedModules { get; init; } = [];
    public string HelpDocPath { get; init; } = "";
}

public sealed class SchedulingNextStepDto
{
    public string ModuleKey { get; init; } = "";
    public string Title { get; init; } = "";
    public string Path { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class SchedulingReadinessSummaryDto
{
    public double OverallPercent { get; init; }
    public IReadOnlyList<SchedulingReadinessSectionDto> Sections { get; init; } = [];
    public IReadOnlyList<SchedulingModuleStatusDto> Modules { get; init; } = [];
    public SchedulingNextStepDto? NextRecommendedStep { get; init; }
    public int CompletedModules { get; init; }
    public int PendingModules { get; init; }
    public int BlockedModules { get; init; }
    public IReadOnlyList<SchedulingChartPointDto> ProgressChart { get; init; } = [];
    public IReadOnlyList<SchedulingDependencyEdgeDto> DependencyTree { get; init; } = [];
    public bool DoesNotModifyTimetableGeneration => true;
    public bool DoesNotModifyAttendanceApis => true;
}

public sealed class SchedulingChartPointDto
{
    public string Label { get; init; } = "";
    public decimal Value { get; init; }
}

public sealed class SchedulingDependencyEdgeDto
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
}

public sealed class SchedulingSetupIssueDto
{
    public string Code { get; init; } = "";
    public string Severity { get; init; } = "Warning";
    public string Message { get; init; } = "";
    public string? Suggestion { get; init; }
    public string? Path { get; init; }
}

public sealed class SchedulingSetupValidationDto
{
    public IReadOnlyList<SchedulingSetupIssueDto> Errors { get; init; } = [];
    public IReadOnlyList<SchedulingSetupIssueDto> Warnings { get; init; } = [];
    public IReadOnlyList<SchedulingSetupIssueDto> Suggestions { get; init; } = [];
    public bool NeverBlocks => true;
    public bool SkipsConflictDetection => true;
}
