using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1A.6 — Sole academic tree builder. Dashboard/Catalog/Wizard/Student reuse this.
/// </summary>
public interface IAcademicTreeService
{
    Task<AcademicHierarchyReadModel> BuildTreeAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default);

    IReadOnlyList<AcademicHierarchyNode> FlattenTree(AcademicHierarchyReadModel model);

    IReadOnlyList<AcademicHierarchyNode> GetChildren(AcademicHierarchyReadModel model, string nodeId);

    AcademicHierarchyNode? GetParent(AcademicHierarchyReadModel model, string nodeId);

    IReadOnlyList<AcademicHierarchyNode> GetPath(AcademicHierarchyReadModel model, string nodeId);

    /// <summary>Returns a new expanded-node set (read model stays immutable).</summary>
    IReadOnlySet<string> Expand(IReadOnlySet<string> expandedNodeIds, string nodeId);

    /// <summary>Returns a new expanded-node set with the node removed.</summary>
    IReadOnlySet<string> Collapse(IReadOnlySet<string> expandedNodeIds, string nodeId);

    AcademicHierarchyNode? FindByNodeId(AcademicHierarchyReadModel model, string nodeId);
}
