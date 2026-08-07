namespace Abhyanvaya.Application.Academic.ReadModels;

/// <summary>AI29.1A.6 — Single breadcrumb segment (immutable).</summary>
public sealed record AcademicBreadcrumbItem(
    string NodeId,
    string EntityType,
    int EntityId,
    string DisplayName,
    string Code);

/// <summary>AI29.1A.6 — Breadcrumb path (immutable).</summary>
public sealed record AcademicBreadcrumb(
    IReadOnlyList<AcademicBreadcrumbItem> Items)
{
    public string DisplayPath => string.Join(" > ", Items.Select(i => i.DisplayName));
}
