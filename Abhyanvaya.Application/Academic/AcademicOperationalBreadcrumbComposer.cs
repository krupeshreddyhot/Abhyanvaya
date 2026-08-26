using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D Prompt 16 — composes operational breadcrumbs from the canonical academic tree.
/// Pages must not rebuild Program→Course→Group→Semester paths; they call the breadcrumb API instead.
/// </summary>
public static class AcademicOperationalBreadcrumbComposer
{
    private static readonly HashSet<string> SpineTypes = new(StringComparer.Ordinal)
    {
        "Program", "Course", "Group", "Semester",
    };

    public static AcademicBreadcrumb Compose(IAcademicTreeService tree, AcademicHierarchyReadModel model, AcademicOperationalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ctx);

        var sectionIds = NormalizeIds(ctx.SectionId, ctx.SectionIds);
        var subject = ctx.SubjectId is > 0 ? tree.FindByNodeId(model, $"Subject:{ctx.SubjectId.Value}") : null;
        var sectionNodes = sectionIds
            .Select(id => tree.FindByNodeId(model, $"Section:{id}"))
            .Where(n => n is not null)
            .Cast<AcademicHierarchyNode>()
            .ToList();

        // Subject-only or Section-only: reuse the tree path for that leaf (Programs disabled ⇒ no Program segment).
        if (subject is not null && sectionNodes.Count == 0)
            return FromPath(FilterDisplay(tree.GetPath(model, subject.NodeId)));

        if (subject is null && sectionNodes.Count == 1)
            return FromPath(FilterDisplay(tree.GetPath(model, sectionNodes[0].NodeId)));

        // Combined Section (+ optional Subject): spine through Semester, then Section label(s), then Subject.
        var semesterNode = ResolveSemesterNode(tree, model, ctx, subject, sectionNodes);
        if (semesterNode is not null)
        {
            var items = FilterDisplay(tree.GetPath(model, semesterNode.NodeId))
                .Where(n => SpineTypes.Contains(n.EntityType))
                .Select(ToItem)
                .ToList();

            if (sectionNodes.Count == 1)
            {
                items.Add(ToItem(sectionNodes[0]));
            }
            else if (sectionNodes.Count > 1)
            {
                var label = string.Join(" + ", sectionNodes.Select(SectionLabel));
                var code = string.Join("+", sectionNodes.Select(n => string.IsNullOrWhiteSpace(n.Code) ? n.DisplayName : n.Code));
                items.Add(new AcademicBreadcrumbItem("Section:combined", "Section", 0, label, code));
            }

            if (subject is not null)
                items.Add(ToItem(subject));

            return new AcademicBreadcrumb(items);
        }

        // Partial selection — deepest available single node path.
        var fallbackId =
            ctx.GroupId is > 0 ? $"Group:{ctx.GroupId.Value}"
            : ctx.CourseId is > 0 ? $"Course:{ctx.CourseId.Value}"
            : ctx.ProgramId is > 0 ? $"Program:{ctx.ProgramId.Value}"
            : null;

        if (fallbackId is null)
            return new AcademicBreadcrumb([]);

        return FromPath(FilterDisplay(tree.GetPath(model, fallbackId)));
    }

    private static AcademicHierarchyNode? ResolveSemesterNode(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        AcademicOperationalContext ctx,
        AcademicHierarchyNode? subject,
        IReadOnlyList<AcademicHierarchyNode> sectionNodes)
    {
        if (ctx.SemesterId is > 0)
            return AcademicHierarchyNodeResolver.ResolveSemester(tree, model, ctx.SemesterId.Value, ctx.GroupId);

        if (subject is not null)
            return FindAncestor(tree, model, subject.NodeId, "Semester");

        if (sectionNodes.Count > 0)
            return FindAncestor(tree, model, sectionNodes[0].NodeId, "Semester");

        return null;
    }

    private static AcademicHierarchyNode? FindAncestor(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        string nodeId,
        string entityType)
        => tree.GetPath(model, nodeId).LastOrDefault(n =>
            string.Equals(n.EntityType, entityType, StringComparison.Ordinal));

    private static IReadOnlyList<AcademicHierarchyNode> FilterDisplay(IReadOnlyList<AcademicHierarchyNode> path)
        => path.Where(n => !string.Equals(n.EntityType, "Unassigned", StringComparison.Ordinal)).ToList();

    private static AcademicBreadcrumb FromPath(IReadOnlyList<AcademicHierarchyNode> path)
        => new(path.Select(ToItem).ToList());

    private static AcademicBreadcrumbItem ToItem(AcademicHierarchyNode n)
        => new(n.NodeId, n.EntityType, n.EntityId, n.DisplayName, n.Code);

    private static string SectionLabel(AcademicHierarchyNode n)
        => !string.IsNullOrWhiteSpace(n.Code) ? n.Code : n.DisplayName;

    private static IReadOnlyList<int> NormalizeIds(int? sectionId, IEnumerable<int>? sectionIds)
    {
        var ids = (sectionIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
        if (sectionId is > 0 && !ids.Contains(sectionId.Value))
            ids.Add(sectionId.Value);
        return ids;
    }
}
