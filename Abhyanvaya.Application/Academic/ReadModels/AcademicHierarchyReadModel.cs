namespace Abhyanvaya.Application.Academic.ReadModels;

/// <summary>
/// AI29.1A.6 — Immutable academic hierarchy read model.
/// Projection optimized for queries; never mutate or persist through this type.
/// </summary>
public sealed record AcademicHierarchyReadModel
{
    public bool EnablePrograms { get; init; }
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<AcademicHierarchyNode> Roots { get; init; } = [];
    public int TotalNodes { get; init; }
}
