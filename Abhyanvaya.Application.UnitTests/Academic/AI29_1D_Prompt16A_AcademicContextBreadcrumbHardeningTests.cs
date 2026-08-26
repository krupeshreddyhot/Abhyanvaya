using System.Security.Claims;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Domain.Authorization;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 16A — Academic Context Breadcrumb authorization &amp; consistency hardening.
/// </summary>
public sealed class AI29_1D_Prompt16A_AcademicContextBreadcrumbHardeningTests
{
    [Fact]
    public void Case01_Valid_Program_Course_Group_Semester_Section_Subject()
    {
        var (tree, model) = BuildTree(enablePrograms: true, includeSectionB: true);
        var ctx = new AcademicOperationalContext
        {
            ProgramId = 1,
            CourseId = 2,
            GroupId = 3,
            SemesterId = 4,
            SectionId = 5,
            SubjectId = 6,
        };
        Assert.True(AcademicOperationalContextValidator.Validate(tree, model, ctx).IsValid);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(tree, model, ctx);
        Assert.Equal(
            "Commerce > B.Com > Computer Applications > Semester 3 > Section A > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Case02_Program_Disabled()
    {
        var (tree, model) = BuildTree(enablePrograms: false);
        var ctx = new AcademicOperationalContext
        {
            CourseId = 2,
            GroupId = 3,
            SemesterId = 4,
            SectionId = 5,
            SubjectId = 6,
        };
        Assert.True(AcademicOperationalContextValidator.Validate(tree, model, ctx).IsValid);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(tree, model, ctx);
        Assert.DoesNotContain(crumb.Items, i => i.EntityType == "Program");
        Assert.Equal(
            "B.Com > Computer Applications > Semester 3 > Section A > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Case03_Attendance_Permission_Without_Program_View_Is_Allowed()
    {
        Assert.True(AcademicOperationalContextAccess.IsAllowed([PermissionKeys.AttendanceView]));
        Assert.True(AcademicOperationalContextAccess.IsAllowed([PermissionKeys.AttendanceManage]));
        Assert.DoesNotContain(PermissionKeys.ProgramView, new[] { PermissionKeys.AttendanceView });
        Assert.DoesNotContain(PermissionKeys.ProgramCreate, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.DoesNotContain(PermissionKeys.ProgramEdit, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.DoesNotContain(PermissionKeys.ProgramDelete, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.DoesNotContain(PermissionKeys.ProgramManage, AcademicOperationalContextAccess.AllowedPermissionKeys);
    }

    [Fact]
    public void Case04_Unauthorized_Breadcrumb_Request_Is_Denied()
    {
        Assert.False(AcademicOperationalContextAccess.IsAllowed([]));
        Assert.False(AcademicOperationalContextAccess.IsAllowed([PermissionKeys.ProgramCreate]));
        Assert.False(AcademicOperationalContextAccess.IsAllowed([PermissionKeys.ReportsView]));

        var claims = new[] { new Claim("permission", PermissionKeys.ProgramCreate) };
        Assert.False(AcademicOperationalContextAccess.HasPermission(claims, role: "Faculty"));
        Assert.True(AcademicOperationalContextAccess.HasPermission(
            [new Claim("permission", PermissionKeys.AttendanceView)],
            role: "Faculty"));
        Assert.True(AcademicOperationalContextAccess.HasPermission([], role: "SuperAdmin"));
    }

    [Fact]
    public void Case05_Section_From_Wrong_Semester()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeSectionB: true, includeAltSemester: true);
        var result = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SectionId = 8, // belongs to Semester 40
            });
        Assert.False(result.IsValid);
        Assert.Contains("Semester", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case06_Section_From_Wrong_Course()
    {
        var (tree, model) = BuildTree(enablePrograms: true, includeAltCourseSection: true);
        // Omit Semester so the Course ancestry rule is the failing check (Section 9 is under Course 20).
        var result = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                ProgramId = 1,
                CourseId = 2,
                SectionId = 9,
            });
        Assert.False(result.IsValid);
        Assert.Contains("Course", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case07_Subject_From_Wrong_Semester()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeAltSemester: true);
        var result = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SubjectId = 60, // under Semester 40
            });
        Assert.False(result.IsValid);
        Assert.Contains("Semester", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case08_Subject_From_Wrong_Course_Or_Group()
    {
        var (tree, model) = BuildTree(enablePrograms: true, includeAltCourseSection: true);
        var wrongCourse = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SubjectId = 61, // under Course 20
            });
        Assert.False(wrongCourse.IsValid);
        Assert.Contains("Course", wrongCourse.Error, StringComparison.OrdinalIgnoreCase);

        var wrongGroup = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SubjectId = 62, // under Group 30 of Course 2
            });
        Assert.False(wrongGroup.IsValid);
        Assert.Contains("Group", wrongGroup.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case09_Combined_Sections_From_Same_Scope()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeSectionB: true);
        var ctx = new AcademicOperationalContext
        {
            SemesterId = 4,
            SectionIds = [5, 7],
            SubjectId = 6,
        };
        Assert.True(AcademicOperationalContextValidator.Validate(tree, model, ctx).IsValid);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(tree, model, ctx);
        Assert.Contains(crumb.Items, i => i.NodeId == "Section:combined");
        Assert.Equal(
            "B.Com > Computer Applications > Semester 3 > A + B > Business Statistics",
            crumb.DisplayPath);
    }

    [Fact]
    public void Case10_Combined_Sections_From_Different_Scopes()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeSectionB: true, includeAltSemester: true);
        var result = AcademicOperationalContextValidator.Validate(
            tree,
            model,
            new AcademicOperationalContext
            {
                SectionIds = [5, 8], // Semester 4 vs Semester 40
            });
        Assert.False(result.IsValid);
        Assert.Contains("same academic scope", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consumer_Permission_Catalog_Covers_Attendance_Sections_Timetable_Allocation()
    {
        Assert.Contains(PermissionKeys.AttendanceView, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.Contains(PermissionKeys.SectionView, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.Contains(PermissionKeys.SchedulingTimetableView, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.Contains(PermissionKeys.AllocationRun, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.Contains(PermissionKeys.AllocationOperationsView, AcademicOperationalContextAccess.AllowedPermissionKeys);
        Assert.Contains(PermissionKeys.ProgramView, AcademicOperationalContextAccess.AllowedPermissionKeys);
    }

    [Fact]
    public void Invalid_Context_Does_Not_Compose_Misleading_Trail()
    {
        var (tree, model) = BuildTree(enablePrograms: false, includeAltSemester: true);
        var ctx = new AcademicOperationalContext { SemesterId = 4, SectionId = 8 };
        var validation = AcademicOperationalContextValidator.Validate(tree, model, ctx);
        Assert.False(validation.IsValid);

        // Composer is not called by the service when invalid; ensure we do not treat invalid as a normal trail.
        var outcome = AcademicOperationalBreadcrumbOutcome.Invalid(validation.Error!);
        Assert.False(outcome.IsValid);
        Assert.Empty(outcome.Breadcrumb.Items);
    }

    #region Tree fixture

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) BuildTree(
        bool enablePrograms,
        bool includeSectionB = false,
        bool includeAltSemester = false,
        bool includeAltCourseSection = false)
    {
        var subject = Node("Subject", 6, "Semester:4", "Business Statistics", "BS", 4);
        var sectionA = Node("Section", 5, "Semester:4", "Section A", "A", 4);
        var leaves = new List<AcademicHierarchyNode> { subject, sectionA };
        if (includeSectionB)
            leaves.Add(Node("Section", 7, "Semester:4", "Section B", "B", 4));

        var semester = Node("Semester", 4, "Group:3", "Semester 3", "3", 3, leaves);
        var groupChildren = new List<AcademicHierarchyNode> { semester };

        if (includeAltSemester)
        {
            var altSubject = Node("Subject", 60, "Semester:40", "Alt Subject", "AS", 4);
            var altSection = Node("Section", 8, "Semester:40", "Section Z", "Z", 4);
            groupChildren.Add(Node("Semester", 40, "Group:3", "Semester Alt", "40", 3, [altSubject, altSection]));
        }

        var group = Node("Group", 3, "Course:2", "Computer Applications", "CA", 2, groupChildren);

        if (includeAltCourseSection)
        {
            // Wrong group under same course
            var wrongGroupSubject = Node("Subject", 62, "Semester:42", "Wrong Group Subject", "WGS", 5);
            var wrongGroupSemester = Node("Semester", 42, "Group:30", "Sem WG", "42", 4, [wrongGroupSubject]);
            var wrongGroup = Node("Group", 30, "Course:2", "Wrong Group", "WG", 3, [wrongGroupSemester]);

            // Wrong course under same program
            var wrongCourseSubject = Node("Subject", 61, "Semester:41", "Wrong Course Subject", "WCS", 5);
            var wrongCourseSection = Node("Section", 9, "Semester:41", "Section X", "X", 5);
            var wrongCourseSemester = Node("Semester", 41, "Group:31", "Sem WC", "41", 4, [wrongCourseSubject, wrongCourseSection]);
            var wrongCourseGroup = Node("Group", 31, "Course:20", "Alt Group", "AG", 3, [wrongCourseSemester]);
            var wrongCourse = Node("Course", 20, enablePrograms ? "Program:1" : null, "B.A", "BA", enablePrograms ? 1 : 0, [wrongCourseGroup]);

            var course = Node(
                "Course",
                2,
                enablePrograms ? "Program:1" : null,
                "B.Com",
                "BCOM",
                enablePrograms ? 1 : 0,
                [group, wrongGroup]);

            IReadOnlyList<AcademicHierarchyNode> roots = enablePrograms
                ? [Node("Program", 1, null, "Commerce", "COM", 0, [course, wrongCourse])]
                : [course, wrongCourse];

            return MockTree(enablePrograms, roots);
        }

        {
            var course = Node(
                "Course",
                2,
                enablePrograms ? "Program:1" : null,
                "B.Com",
                "BCOM",
                enablePrograms ? 1 : 0,
                [group]);

            IReadOnlyList<AcademicHierarchyNode> roots = enablePrograms
                ? [Node("Program", 1, null, "Commerce", "COM", 0, [course])]
                : [course];

            return MockTree(enablePrograms, roots);
        }
    }

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) MockTree(
        bool enablePrograms,
        IReadOnlyList<AcademicHierarchyNode> roots)
    {
        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = enablePrograms,
            GeneratedUtc = DateTime.UtcNow,
            Roots = roots,
            TotalNodes = 20,
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

    #endregion
}
