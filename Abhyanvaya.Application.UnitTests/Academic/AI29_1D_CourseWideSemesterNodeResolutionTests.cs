using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// Course-wide Semester (GroupId null) is placed under every Group with a disambiguated NodeId.
/// Regression: FindByNodeId(Semester:id) previously returned the first placement and rejected
/// valid Group + Semester pairs with "Semester does not belong to the selected Group."
/// </summary>
public sealed class AI29_1D_CourseWideSemesterNodeResolutionTests
{
    [Fact]
    public void CourseWide_Semester_Validates_Under_Second_Group()
    {
        var (tree, model) = BuildCourseWideTree();
        var ctx = new AcademicOperationalContext
        {
            CourseId = 2,
            GroupId = 30, // second group — not the first tree walk hit
            SemesterId = 111,
        };

        var result = AcademicOperationalContextValidator.Validate(tree, model, ctx);
        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void CourseWide_Semester_Breadcrumb_Uses_Selected_Group()
    {
        var (tree, model) = BuildCourseWideTree();
        var ctx = new AcademicOperationalContext
        {
            CourseId = 2,
            GroupId = 30,
            SemesterId = 111,
        };

        Assert.True(AcademicOperationalContextValidator.Validate(tree, model, ctx).IsValid);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(tree, model, ctx);
        Assert.Equal("B.Com > Computer Applications > Semester III", crumb.DisplayPath);
    }

    [Fact]
    public void GroupSpecific_Semester_Still_Rejects_Wrong_Group()
    {
        var (tree, model) = BuildGroupSpecificTree();
        var result = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 30,
                SemesterId = 111, // only under Group 3
            });
        Assert.False(result.IsValid);
        Assert.Contains("Group", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) BuildCourseWideTree()
    {
        // Same semester entity under two groups (course-wide), distinct NodeIds.
        var semUnderFirst = Node(
            "Semester",
            111,
            "Group:3",
            "Semester III",
            "3",
            3,
            nodeId: AcademicHierarchyNodeResolver.CourseWideSemesterNodeId(111, 3));
        var semUnderSecond = Node(
            "Semester",
            111,
            "Group:30",
            "Semester III",
            "3",
            3,
            nodeId: AcademicHierarchyNodeResolver.CourseWideSemesterNodeId(111, 30));

        var groupFirst = Node("Group", 3, "Course:2", "General", "GEN", 2, [semUnderFirst]);
        var groupSecond = Node("Group", 30, "Course:2", "Computer Applications", "CA", 2, [semUnderSecond]);
        var course = Node("Course", 2, null, "B.Com", "BCOM", 0, [groupFirst, groupSecond]);

        return MockTree([course]);
    }

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) BuildGroupSpecificTree()
    {
        var sem = Node("Semester", 111, "Group:3", "Semester III", "3", 3);
        var groupFirst = Node("Group", 3, "Course:2", "General", "GEN", 2, [sem]);
        var groupSecond = Node("Group", 30, "Course:2", "Computer Applications", "CA", 2, []);
        var course = Node("Course", 2, null, "B.Com", "BCOM", 0, [groupFirst, groupSecond]);
        return MockTree([course]);
    }

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) MockTree(
        IReadOnlyList<AcademicHierarchyNode> roots)
    {
        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = false,
            GeneratedUtc = DateTime.UtcNow,
            Roots = roots,
            TotalNodes = 10,
        };

        // Real walk (not dictionary) so duplicate EntityIds with distinct NodeIds are exercised.
        var tree = new WalkTreeService();
        return (tree, model);
    }

    private static AcademicHierarchyNode Node(
        string type,
        int id,
        string? parent,
        string name,
        string code,
        int level,
        IReadOnlyList<AcademicHierarchyNode>? children = null,
        string? nodeId = null)
        => new()
        {
            NodeId = nodeId ?? $"{type}:{id}",
            ParentNodeId = parent,
            EntityId = id,
            EntityType = type,
            NodeType = type,
            DisplayName = name,
            Code = code,
            DisplayOrder = 0,
            IsActive = true,
            ChildrenCount = children?.Count ?? 0,
            HasChildren = children is { Count: > 0 },
            HierarchyLevel = level,
            EntityStatus = "Active",
            Children = children ?? [],
        };

    /// <summary>Minimal tree service matching AcademicTreeService FindByNodeId/GetPath walk semantics.</summary>
    private sealed class WalkTreeService : IAcademicTreeService
    {
        public Task<AcademicHierarchyReadModel> BuildTreeAsync(
            bool includeInactive = false,
            bool includeSections = true,
            bool includeSubjects = true,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IReadOnlyList<AcademicHierarchyNode> FlattenTree(AcademicHierarchyReadModel model) => [];
        public IReadOnlyList<AcademicHierarchyNode> GetChildren(AcademicHierarchyReadModel model, string nodeId)
            => FindByNodeId(model, nodeId)?.Children ?? [];
        public AcademicHierarchyNode? GetParent(AcademicHierarchyReadModel model, string nodeId)
        {
            var node = FindByNodeId(model, nodeId);
            return node?.ParentNodeId is null ? null : FindByNodeId(model, node.ParentNodeId);
        }

        public IReadOnlyList<AcademicHierarchyNode> GetPath(AcademicHierarchyReadModel model, string nodeId)
        {
            var path = new List<AcademicHierarchyNode>();
            var current = FindByNodeId(model, nodeId);
            while (current is not null)
            {
                path.Insert(0, current with { Children = [] });
                current = current.ParentNodeId is null ? null : FindByNodeId(model, current.ParentNodeId);
            }
            return path;
        }

        public IReadOnlySet<string> Expand(IReadOnlySet<string> expandedNodeIds, string nodeId) => expandedNodeIds;
        public IReadOnlySet<string> Collapse(IReadOnlySet<string> expandedNodeIds, string nodeId) => expandedNodeIds;

        public AcademicHierarchyNode? FindByNodeId(AcademicHierarchyReadModel model, string nodeId)
        {
            AcademicHierarchyNode? Walk(IEnumerable<AcademicHierarchyNode> nodes)
            {
                foreach (var n in nodes)
                {
                    if (string.Equals(n.NodeId, nodeId, StringComparison.Ordinal)) return n;
                    var child = Walk(n.Children);
                    if (child is not null) return child;
                }
                return null;
            }
            return Walk(model.Roots);
        }
    }
}
