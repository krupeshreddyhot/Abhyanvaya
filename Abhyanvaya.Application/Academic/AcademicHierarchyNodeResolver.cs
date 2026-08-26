using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// Resolves hierarchy nodes when the same entity can appear under multiple parents.
/// P1-4 Prompt 3L / 3I1 — operational AcademicTree no longer places NULL-group Semesters under Groups.
/// <see cref="CourseWideSemesterNodeId"/> remains for resolving historically stored disambiguated node ids.
/// </summary>
public static class AcademicHierarchyNodeResolver
{
    /// <summary>
    /// Legacy disambiguated node id used when course-wide Semesters were placed under every Group.
    /// Operational trees no longer emit these nodes; resolver still accepts them for path compatibility.
    /// </summary>
    public static string CourseWideSemesterNodeId(int semesterId, int groupId)
        => $"Semester:{semesterId}@Group:{groupId}";

    public static AcademicHierarchyNode? ResolveSemester(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        int semesterId,
        int? groupId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(model);
        if (semesterId <= 0) return null;

        if (groupId is > 0)
        {
            var disambiguated = tree.FindByNodeId(model, CourseWideSemesterNodeId(semesterId, groupId.Value));
            if (disambiguated is not null) return disambiguated;

            var group = tree.FindByNodeId(model, $"Group:{groupId.Value}");
            var underGroup = group?.Children.FirstOrDefault(c =>
                string.Equals(c.EntityType, "Semester", StringComparison.Ordinal)
                && c.EntityId == semesterId);
            if (underGroup is not null) return underGroup;
        }

        var direct = tree.FindByNodeId(model, $"Semester:{semesterId}");
        if (direct is not null) return direct;

        // Last resort: first Semester entity match (any placement).
        return FindFirstByEntity(model.Roots, "Semester", semesterId);
    }

    private static AcademicHierarchyNode? FindFirstByEntity(
        IEnumerable<AcademicHierarchyNode> nodes,
        string entityType,
        int entityId)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.EntityType, entityType, StringComparison.Ordinal) && n.EntityId == entityId)
                return n;
            var child = FindFirstByEntity(n.Children, entityType, entityId);
            if (child is not null) return child;
        }

        return null;
    }
}
