namespace Abhyanvaya.Application.Academic.ReadModels;

/// <summary>AI29.1A.6 — Search hit with hierarchy path (immutable).</summary>
public sealed record AcademicSearchResult(
    AcademicHierarchyNode Node,
    AcademicBreadcrumb Path);
