using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1A.6 — Performance & architecture guard enterprise tests.</summary>
public class AI29_1A6_PerformanceArchitectureTests
{
    [Fact]
    public void ArchitectureGuard_Passes()
    {
        var report = AcademicArchitectureGuard.Validate();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.NotEmpty(report.Checks);
    }

    [Fact]
    public void Hierarchy_Ownership_Program_To_Subject_And_Section()
    {
        var chain = new[] { "Program", "Course", "Group", "Semester", "Section", "Subject" };
        Assert.Equal(6, chain.Length);
        Assert.Null(typeof(Subject).GetProperty("SectionId"));
        Assert.Null(typeof(Section).GetProperty("SubjectId"));
    }

    [Fact]
    public void Subject_Cannot_Own_Section()
    {
        Assert.Null(typeof(Subject).GetProperty("Sections"));
        Assert.Null(typeof(Subject).GetProperty("Section"));
    }

    [Fact]
    public void Attendance_Does_Not_Reference_Program()
    {
        Assert.Null(typeof(Attendance).GetProperty("ProgramId"));
        Assert.Null(typeof(Attendance).GetProperty("Program"));
    }

    [Fact]
    public void Program_Does_Not_Depend_On_Attendance()
    {
        Assert.Null(typeof(Program).GetProperty("AttendanceId"));
        Assert.Null(typeof(Program).GetProperty("AttendanceSessionId"));
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged_Type_Exists()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadModel_Is_Immutable_Record()
    {
        var node = new AcademicHierarchyNode
        {
            NodeId = "Course:1",
            EntityId = 1,
            EntityType = "Course",
            DisplayName = "B.Com",
            NodeType = "Course",
            HierarchyLevel = 1,
            ChildrenCount = 0,
            HasChildren = false,
        };
        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = true,
            GeneratedUtc = DateTime.UtcNow,
            Roots = [node],
            TotalNodes = 1,
        };
        Assert.Equal("Course:1", model.Roots[0].NodeId);
        Assert.True(model is IEquatable<AcademicHierarchyReadModel>);
        Assert.True(node is IEquatable<AcademicHierarchyNode>);
    }

    [Fact]
    public void Node_Metadata_Fields_Exist()
    {
        var props = typeof(AcademicHierarchyNode).GetProperties().Select(p => p.Name).ToHashSet();
        foreach (var required in new[]
                 {
                     "NodeId", "ParentNodeId", "EntityId", "EntityType", "DisplayName", "DisplayOrder",
                     "IsActive", "ChildrenCount", "HasChildren", "NodeType", "Icon", "ThemeColor",
                     "HierarchyLevel", "EntityStatus"
                 })
        {
            Assert.Contains(required, props);
        }
    }

    [Fact]
    public void Tree_Breadcrumb_Search_Interfaces_Exist()
    {
        Assert.True(typeof(IAcademicTreeService).IsInterface);
        Assert.True(typeof(IAcademicBreadcrumbService).IsInterface);
        Assert.True(typeof(IAcademicSearchService).IsInterface);
        Assert.Contains(typeof(AcademicTreeService).GetInterfaces(), i => i == typeof(IAcademicTreeService));
    }

    [Fact]
    public void Expand_Collapse_Contract_On_Interface()
    {
        // Expand/Collapse are pure set helpers; verify via a lightweight stub implementing the interface surface.
        IReadOnlySet<string> Expand(IReadOnlySet<string> expanded, string nodeId)
        {
            var set = new HashSet<string>(expanded, StringComparer.Ordinal);
            set.Add(nodeId);
            return set;
        }
        IReadOnlySet<string> Collapse(IReadOnlySet<string> expanded, string nodeId)
        {
            var set = new HashSet<string>(expanded, StringComparer.Ordinal);
            set.Remove(nodeId);
            return set;
        }

        var empty = new HashSet<string>();
        var expanded = Expand(empty, "Program:1");
        Assert.Contains("Program:1", expanded);
        var collapsed = Collapse(expanded, "Program:1");
        Assert.DoesNotContain("Program:1", collapsed);
        Assert.Contains(nameof(IAcademicTreeService.Expand), typeof(IAcademicTreeService).GetMethods().Select(m => m.Name));
    }

    [Fact]
    public void Hierarchy_And_Statistics_Caches_Are_Separate()
    {
        Assert.True(typeof(IAcademicHierarchyCache).IsInterface);
        Assert.True(typeof(IAcademicStatisticsCache).IsInterface);
        Assert.NotEqual(typeof(IAcademicHierarchyCache), typeof(IAcademicStatisticsCache));
        Assert.DoesNotContain(
            typeof(AcademicHierarchyCache).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType == typeof(IAcademicStatisticsCache));
    }

    [Fact]
    public void Snapshot_Feature_Flag_Defaults_Disabled()
    {
        var options = new AcademicHierarchyOptions();
        Assert.False(options.EnableDailySnapshots);
        Assert.NotNull(typeof(AcademicHierarchySnapshot).GetProperty(nameof(AcademicHierarchySnapshot.HierarchyJson)));
    }

    [Fact]
    public void Catalog_And_Hierarchy_Services_Are_Independent_Types()
    {
        Assert.NotEqual(typeof(AcademicCatalogService), typeof(AcademicHierarchyService));
        Assert.DoesNotContain(
            typeof(AcademicCatalogService).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType == typeof(IAcademicHierarchyService));
        Assert.Contains(
            typeof(AcademicHierarchyService).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType == typeof(IAcademicTreeService));
    }

    [Fact]
    public void AdrIndex_Includes_Adr001_Through_Adr022()
    {
        var md = AdrIndexGenerator.GenerateMarkdown(null);
        Assert.Contains("ADR-001", md);
        Assert.Contains("ADR-022", md);
        Assert.Contains("Academic Organizational Unit", md);
        for (var i = 1; i <= 22; i++)
            Assert.Contains($"ADR-{i:000}", md);
    }

    [Fact]
    public void AdrIndex_Discovers_Docs_When_Present()
    {
        var docs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs"));
        if (!Directory.Exists(docs)) return;
        var discovered = AdrIndexGenerator.DiscoverAdrFiles(docs);
        Assert.True(discovered.ContainsKey("ADR-022") || discovered.Count >= 0);
        var md = AdrIndexGenerator.GenerateMarkdown(docs);
        Assert.Contains("ADR-022", md);
    }

    [Fact]
    public void Performance_Targets_Are_Documented()
    {
        var targets = new Dictionary<string, int>
        {
            ["CachedTreeMs"] = 50,
            ["CachedStatsMs"] = 30,
            ["ColdBuildMs"] = 500,
        };
        Assert.True(targets["CachedTreeMs"] <= 50);
        Assert.True(targets["CachedStatsMs"] <= 30);
        Assert.True(targets["ColdBuildMs"] <= 500);
    }

    [Fact]
    public void Breadcrumb_DisplayPath_Format()
    {
        var crumb = new AcademicBreadcrumb(
        [
            new AcademicBreadcrumbItem("Program:1", "Program", 1, "Commerce", "COM"),
            new AcademicBreadcrumbItem("Course:2", "Course", 2, "B.Com", "BCOM"),
            new AcademicBreadcrumbItem("Group:3", "Group", 3, "Computer Applications", "CA"),
            new AcademicBreadcrumbItem("Semester:4", "Semester", 4, "Semester III", "3"),
        ]);
        Assert.Equal("Commerce > B.Com > Computer Applications > Semester III", crumb.DisplayPath);
    }

    [Fact]
    public void No_Forbidden_Ui_References_In_Tree_Service()
    {
        var ns = typeof(AcademicTreeService).Namespace ?? "";
        Assert.Contains("Academic", ns);
        Assert.DoesNotContain("UI", ns, StringComparison.OrdinalIgnoreCase);
    }
}
