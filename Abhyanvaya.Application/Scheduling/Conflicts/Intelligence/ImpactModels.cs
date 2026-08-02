using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public sealed class ImpactNode
{
    public required string NodeId { get; init; }
    public required ImpactCategory Category { get; init; }
    public required string Label { get; init; }
    public int? EntityId { get; init; }
    public ConflictSeverity Severity { get; init; } = ConflictSeverity.Information;
    public string? Detail { get; init; }
}

public sealed class ImpactEdge
{
    public required string FromNodeId { get; init; }
    public required string ToNodeId { get; init; }
    public required string Relation { get; init; }
}

public sealed class ImpactSummary
{
    public int FacultyAffected { get; init; }
    public int StudentsAffected { get; init; }
    public int RoomsAffected { get; init; }
    public int DepartmentsAffected { get; init; }
    public int PublishedVersionsAffected { get; init; }
    public int WorkloadSignals { get; init; }
    public int AvailabilitySignals { get; init; }
    public int AttendanceSignals { get; init; }
    public ConflictSeverity MaxSeverity { get; init; } = ConflictSeverity.Information;
    public string RiskLevel { get; init; } = "Low";
}

public sealed class ImpactGraph
{
    public required ImpactSummary Summary { get; init; }
    public required IReadOnlyList<ImpactNode> Nodes { get; init; }
    public required IReadOnlyList<ImpactEdge> Edges { get; init; }
    public string? NavigationPath { get; init; }
    public bool IsAdvisoryOnly => true;
}

public interface IImpactAnalyzer
{
    Task<ImpactGraph> AnalyzeAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default);

    Task<ImpactGraph> AnalyzeProposedChangeAsync(
        ConflictResult conflict,
        ResolutionOption? proposedOption,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default);
}
