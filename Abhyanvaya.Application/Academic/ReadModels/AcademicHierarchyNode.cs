namespace Abhyanvaya.Application.Academic.ReadModels;

/// <summary>
/// AI29.1A.6 — Immutable hierarchy node (read projection only).
/// Never used for writes or persistence.
/// </summary>
public sealed record AcademicHierarchyNode
{
    public required string NodeId { get; init; }
    public string? ParentNodeId { get; init; }
    public int EntityId { get; init; }
    public string EntityType { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public int ChildrenCount { get; init; }
    public bool HasChildren { get; init; }

    // Metadata (no UI logic)
    public string NodeType { get; init; } = "";
    public string? Icon { get; init; }
    public string? ThemeColor { get; init; }
    public int HierarchyLevel { get; init; }
    public string EntityStatus { get; init; } = "Active";
    public string Code { get; init; } = "";

    public IReadOnlyList<AcademicHierarchyNode> Children { get; init; } = [];
}
