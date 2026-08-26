using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.ReadModels;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D Prompt 16 — academic context breadcrumb composition from canonical tree.</summary>
public sealed class AI29_1D_Prompt16_AcademicContextBreadcrumbTests
{
    [Fact]
    public void Programs_Enabled_Section_And_Subject_Trail()
    {
        var (tree, model) = BuildTree(enablePrograms: true);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext
            {
                ProgramId = 1,
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SectionId = 5,
                SubjectId = 6,
            });

        Assert.Equal(
            "Commerce > B.Com > Computer Applications > Semester 3 > Section A > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Programs_Disabled_Omits_Program()
    {
        var (tree, model) = BuildTree(enablePrograms: false);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SectionId = 5,
                SubjectId = 6,
            });

        Assert.Equal(
            "B.Com > Computer Applications > Semester 3 > Section A > Business Statistics",
            crumb.DisplayPath);
        Assert.DoesNotContain(crumb.Items, i => i.EntityType == "Program");
    }

    [Fact]
    public void Subject_Only_Uses_Tree_Path()
    {
        var (tree, model) = BuildTree(enablePrograms: true);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext { SubjectId = 6 });

        Assert.Equal(
            "Commerce > B.Com > Computer Applications > Semester 3 > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Combined_Sections_Join_Codes()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeSectionB: true);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext
            {
                SemesterId = 4,
                SectionIds = [5, 7],
                SubjectId = 6,
            });

        Assert.Equal(
            "B.Com > Computer Applications > Semester 3 > A + B > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Empty_Context_Yields_Empty_Trail()
    {
        var (tree, model) = BuildTree(enablePrograms: true);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext());
        Assert.Empty(crumb.Items);
    }

    [Fact]
    public void Breadcrumb_Service_Exposes_Operational_Context_Contract()
    {
        Assert.Contains(
            typeof(IAcademicBreadcrumbService).GetMethods().Select(m => m.Name),
            n => n == nameof(IAcademicBreadcrumbService.BuildOperationalContextBreadcrumbAsync));
    }

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) BuildTree(
        bool enablePrograms,
        bool includeSectionB = false)
    {
        var subject = Node("Subject", 6, "Semester:4", "Business Statistics", "BS", 4);
        var sectionA = Node("Section", 5, "Semester:4", "Section A", "A", 4);
        var sectionB = Node("Section", 7, "Semester:4", "Section B", "B", 4);
        var leaves = new List<AcademicHierarchyNode> { subject, sectionA };
        if (includeSectionB) leaves.Add(sectionB);

        var semester = Node("Semester", 4, "Group:3", "Semester 3", "3", 3, leaves);
        var group = Node("Group", 3, "Course:2", "Computer Applications", "CA", 2, [semester]);
        var course = Node("Course", 2, enablePrograms ? "Program:1" : null, "B.Com", "BCOM", enablePrograms ? 1 : 0, [group]);

        IReadOnlyList<AcademicHierarchyNode> roots = enablePrograms
            ? [Node("Program", 1, null, "Commerce", "COM", 0, [course])]
            : [course];

        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = enablePrograms,
            GeneratedUtc = DateTime.UtcNow,
            Roots = roots,
            TotalNodes = 10,
        };

        var index = Flatten(roots).ToDictionary(n => n.NodeId, StringComparer.Ordinal);
        var mock = new Mock<IAcademicTreeService>(MockBehavior.Strict);
        mock.Setup(t => t.FindByNodeId(model, It.IsAny<string>()))
            .Returns((AcademicHierarchyReadModel _, string nodeId) =>
                index.TryGetValue(nodeId, out var n) ? n : null);
        mock.Setup(t => t.GetPath(model, It.IsAny<string>()))
            .Returns((AcademicHierarchyReadModel _, string nodeId) =>
            {
                var path = new List<AcademicHierarchyNode>();
                var current = index.TryGetValue(nodeId, out var n) ? n : null;
                while (current is not null)
                {
                    path.Insert(0, current with { Children = [] });
                    current = current.ParentNodeId is not null && index.TryGetValue(current.ParentNodeId, out var p)
                        ? p
                        : null;
                }
                return path;
            });

        return (mock.Object, model);
    }

    private static AcademicHierarchyNode Node(
        string type,
        int id,
        string? parent,
        string name,
        string code,
        int level,
        IReadOnlyList<AcademicHierarchyNode>? children = null)
        => new()
        {
            NodeId = $"{type}:{id}",
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

    private static IEnumerable<AcademicHierarchyNode> Flatten(IEnumerable<AcademicHierarchyNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n with { Children = [] };
            foreach (var c in Flatten(n.Children))
                yield return c;
        }
    }
}
