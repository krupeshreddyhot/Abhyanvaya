using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public sealed class DependencyNode
{
    public required string NodeId { get; init; }
    public required string RuleCode { get; init; }
    public required string Label { get; init; }
    public ConflictSeverity Severity { get; init; }
    public int? TimetableEntryId { get; init; }
    public int? RelatedEntryId { get; init; }
    public string? NavigationPath { get; init; }
    public string? ClusterKey { get; init; }
}

public sealed class DependencyEdge
{
    public required string FromNodeId { get; init; }
    public required string ToNodeId { get; init; }
    public required string Relation { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class DependencySummary
{
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public int ClusterCount { get; init; }
    public int RootConflictCount { get; init; }
}

public sealed class DependencyGraph
{
    public required DependencySummary Summary { get; init; }
    public required IReadOnlyList<DependencyNode> Nodes { get; init; }
    public required IReadOnlyList<DependencyEdge> Edges { get; init; }
    public required string Mermaid { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Clusters { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}

public interface IConflictDependencyAnalyzer
{
    DependencyGraph Analyze(IReadOnlyList<ConflictResult> conflicts);
}
