using System.Text;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

/// <summary>Builds conflict dependency / causal clusters for visualization. No optimization.</summary>
public sealed class ConflictDependencyAnalyzer : IConflictDependencyAnalyzer
{
    public DependencyGraph Analyze(IReadOnlyList<ConflictResult> conflicts)
    {
        var nodes = new List<DependencyNode>();
        var edges = new List<DependencyEdge>();
        var byEntry = conflicts
            .Where(c => c.TimetableEntryId.HasValue)
            .GroupBy(c => c.TimetableEntryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var i = 0; i < conflicts.Count; i++)
        {
            var c = conflicts[i];
            var nodeId = $"C{i}_{c.RuleCode}_{c.TimetableEntryId}";
            var cluster = c.StaffId.HasValue ? $"faculty-{c.StaffId}"
                : c.RoomId.HasValue ? $"room-{c.RoomId}"
                : c.GroupId.HasValue ? $"group-{c.GroupId}"
                : "general";

            nodes.Add(new DependencyNode
            {
                NodeId = nodeId,
                RuleCode = c.RuleCode,
                Label = $"{c.RuleName} (#{c.TimetableEntryId})",
                Severity = c.Severity,
                TimetableEntryId = c.TimetableEntryId,
                RelatedEntryId = c.RelatedEntryId,
                NavigationPath = c.Recommendation.NavigationPath,
                ClusterKey = cluster
            });
        }

        for (var i = 0; i < conflicts.Count; i++)
        {
            var a = conflicts[i];
            var aId = nodes[i].NodeId;

            if (a.RelatedEntryId.HasValue && byEntry.TryGetValue(a.RelatedEntryId.Value, out var relatedConflicts))
            {
                foreach (var other in relatedConflicts)
                {
                    var j = conflicts.ToList().FindIndex(x =>
                        ReferenceEquals(x, other) ||
                        (x.RuleCode == other.RuleCode && x.TimetableEntryId == other.TimetableEntryId && x.RelatedEntryId == other.RelatedEntryId));
                    if (j < 0 || j == i) continue;
                    edges.Add(new DependencyEdge
                    {
                        FromNodeId = aId,
                        ToNodeId = nodes[j].NodeId,
                        Relation = "related-entry",
                        Reason = $"Shares related entry {a.RelatedEntryId}"
                    });
                }
            }

            for (var j = i + 1; j < conflicts.Count; j++)
            {
                var b = conflicts[j];
                if (SameResource(a, b))
                {
                    edges.Add(new DependencyEdge
                    {
                        FromNodeId = aId,
                        ToNodeId = nodes[j].NodeId,
                        Relation = "causes",
                        Reason = DescribeLink(a, b)
                    });
                }
            }
        }

        var distinctEdges = edges
            .GroupBy(e => $"{e.FromNodeId}->{e.ToNodeId}:{e.Relation}")
            .Select(g => g.First())
            .ToList();

        var clusters = nodes
            .GroupBy(n => n.ClusterKey ?? "general")
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(n => n.NodeId).ToList());

        var mermaid = BuildMermaid(nodes, distinctEdges);
        var roots = nodes.Count(n => distinctEdges.All(e => e.ToNodeId != n.NodeId));

        return new DependencyGraph
        {
            Summary = new DependencySummary
            {
                NodeCount = nodes.Count,
                EdgeCount = distinctEdges.Count,
                ClusterCount = clusters.Count,
                RootConflictCount = roots
            },
            Nodes = nodes,
            Edges = distinctEdges,
            Mermaid = mermaid,
            Clusters = clusters
        };
    }

    private static bool SameResource(ConflictResult a, ConflictResult b)
    {
        if (a.TimetableEntryId.HasValue && a.TimetableEntryId == b.TimetableEntryId) return true;
        if (a.RelatedEntryId.HasValue && a.RelatedEntryId == b.TimetableEntryId) return true;
        if (b.RelatedEntryId.HasValue && b.RelatedEntryId == a.TimetableEntryId) return true;
        if (a.StaffId.HasValue && a.StaffId == b.StaffId && a.DayOfWeek == b.DayOfWeek) return true;
        if (a.RoomId.HasValue && a.RoomId == b.RoomId && a.DayOfWeek == b.DayOfWeek && a.TimeSlotId == b.TimeSlotId) return true;
        if (a.GroupId.HasValue && a.GroupId == b.GroupId && a.DayOfWeek == b.DayOfWeek && a.TimeSlotId == b.TimeSlotId) return true;
        return false;
    }

    private static string DescribeLink(ConflictResult a, ConflictResult b)
    {
        if (a.TimetableEntryId == b.TimetableEntryId) return "Same timetable cell";
        if (a.StaffId.HasValue && a.StaffId == b.StaffId) return "Same faculty day cluster";
        if (a.RoomId.HasValue && a.RoomId == b.RoomId) return "Same room slot cluster";
        if (a.GroupId.HasValue && a.GroupId == b.GroupId) return "Same student group slot";
        return "Shared scheduling resource";
    }

    private static string BuildMermaid(IReadOnlyList<DependencyNode> nodes, IReadOnlyList<DependencyEdge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        foreach (var n in nodes)
        {
            var label = n.Label.Replace("\"", "'");
            var style = n.Severity >= ConflictSeverity.Error ? ":::critical" : n.Severity == ConflictSeverity.Warning ? ":::warn" : ":::info";
            sb.AppendLine($"  {Sanitize(n.NodeId)}[\"{label}\"]{style}");
        }
        foreach (var e in edges)
            sb.AppendLine($"  {Sanitize(e.FromNodeId)} -->|{e.Relation}| {Sanitize(e.ToNodeId)}");
        sb.AppendLine("  classDef critical fill:#ef5350,color:#fff");
        sb.AppendLine("  classDef warn fill:#ff9800,color:#fff");
        sb.AppendLine("  classDef info fill:#42a5f5,color:#fff");
        return sb.ToString();
    }

    private static string Sanitize(string id) =>
        new string(id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
}
