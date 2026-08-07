using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6/7 — Canonical tree builder over catalog masters (no duplicated hierarchy logic).</summary>
public sealed class AcademicTreeService : IAcademicTreeService
{
    private readonly IAcademicCatalogService _catalog;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly AcademicMetricsStore _store;
    private readonly ILogger<AcademicTreeService> _logger;

    public AcademicTreeService(
        IAcademicCatalogService catalog,
        IAcademicTelemetryService telemetry,
        AcademicMetricsStore store,
        ILogger<AcademicTreeService> logger)
    {
        _catalog = catalog;
        _telemetry = telemetry;
        _store = store;
        _logger = logger;
    }

    public Task<AcademicHierarchyReadModel> BuildTreeAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.TreeBuild,
            "AcademicTree.Build",
            ct => BuildTreeCoreAsync(includeInactive, includeSections, includeSubjects, ct),
            cancellationToken);

    private async Task<AcademicHierarchyReadModel> BuildTreeCoreAsync(
        bool includeInactive,
        bool includeSections,
        bool includeSubjects,
        CancellationToken cancellationToken)
    {
        var cfg = await _catalog.GetConfigurationAsync(cancellationToken);
        var courses = await _catalog.GetCoursesAsync(cancellationToken);
        var groups = await _catalog.GetGroupsAsync(cancellationToken);
        var semesters = await _catalog.GetSemestersAsync(cancellationToken);
        var sections = includeSections ? await _catalog.GetSectionsAsync(cancellationToken) : Array.Empty<SectionDto>();
        var subjects = includeSubjects ? await _catalog.GetSubjectsAsync(cancellationToken) : Array.Empty<SubjectCatalogItemDto>();
        var programs = cfg.EnablePrograms
            ? await _catalog.GetProgramsAsync(includeInactive, cancellationToken)
            : Array.Empty<ProgramDto>();

        AcademicHierarchyNode BuildCourseNode(Course course, string? parentNodeId, int level)
        {
            var courseNodeId = NodeId("Course", course.Id);
            var courseGroups = groups.Where(g => g.CourseId == course.Id)
                .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
                .ToList();

            var groupChildren = courseGroups.Select(g =>
            {
                var groupNodeId = NodeId("Group", g.Id);
                var semNodes = semesters
                    .Where(s => s.CourseId == course.Id && (s.GroupId == null || s.GroupId == g.Id))
                    .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
                    .Select(sem =>
                    {
                        var semNodeId = NodeId("Semester", sem.Id);
                        var leaf = new List<AcademicHierarchyNode>();

                        if (includeSubjects)
                        {
                            leaf.AddRange(subjects
                                .Where(sub => sub.CourseId == course.Id && sub.GroupId == g.Id && sub.SemesterId == sem.Id)
                                .OrderBy(sub => sub.DisplayOrder).ThenBy(sub => sub.Name)
                                .Select(sub => Leaf(
                                    "Subject", sub.Id, semNodeId, sub.Name, sub.Code, sub.DisplayOrder, level + 3, true, "Active")));
                        }

                        if (includeSections)
                        {
                            leaf.AddRange(sections
                                .Where(sec => sec.CourseId == course.Id && sec.GroupId == g.Id && sec.SemesterId == sem.Id)
                                .OrderBy(sec => sec.DisplayOrder).ThenBy(sec => sec.SectionName)
                                .Select(sec => Leaf(
                                    "Section",
                                    sec.Id,
                                    semNodeId,
                                    sec.SectionName,
                                    sec.SectionCode,
                                    sec.DisplayOrder,
                                    level + 3,
                                    string.Equals(sec.Status, "Active", StringComparison.OrdinalIgnoreCase),
                                    sec.Status)));
                        }

                        return WithChildren(
                            "Semester",
                            sem.Id,
                            groupNodeId,
                            sem.Name,
                            sem.Number.ToString(),
                            sem.DisplayOrder,
                            level + 2,
                            true,
                            "Active",
                            leaf);
                    }).ToList();

                return WithChildren(
                    "Group",
                    g.Id,
                    courseNodeId,
                    g.Name,
                    g.Code,
                    g.DisplayOrder,
                    level + 1,
                    true,
                    "Active",
                    semNodes);
            }).ToList();

            return WithChildren(
                "Course",
                course.Id,
                parentNodeId,
                course.Name,
                course.Code,
                course.DisplayOrder,
                level,
                true,
                "Active",
                groupChildren);
        }

        List<AcademicHierarchyNode> roots;
        if (!cfg.EnablePrograms)
        {
            roots = courses.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => BuildCourseNode(c, null, 0))
                .ToList();
        }
        else
        {
            roots = [];
            foreach (var program in programs.OrderBy(p => p.DisplayOrder).ThenBy(p => p.ProgramName))
            {
                var programNodeId = NodeId("Program", program.Id);
                var programCourses = courses
                    .Where(c => c.ProgramId == program.Id)
                    .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                    .Select(c => BuildCourseNode(c, programNodeId, 1))
                    .ToList();

                roots.Add(WithChildren(
                    "Program",
                    program.Id,
                    null,
                    program.ProgramName,
                    program.ProgramCode,
                    program.DisplayOrder,
                    0,
                    program.IsActive && !string.Equals(program.Status, "Archived", StringComparison.OrdinalIgnoreCase),
                    program.Status,
                    programCourses,
                    program.Icon,
                    program.ThemeColor));
            }

            var unassigned = courses
                .Where(c => c.ProgramId == null)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => BuildCourseNode(c, NodeId("Unassigned", 0), 1))
                .ToList();
            if (unassigned.Count > 0)
            {
                roots.Add(WithChildren(
                    "Unassigned",
                    0,
                    null,
                    "Courses without Program",
                    "UNASSIGNED",
                    int.MaxValue,
                    0,
                    true,
                    "Active",
                    unassigned));
            }
        }

        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = cfg.EnablePrograms,
            GeneratedUtc = DateTime.UtcNow,
            Roots = roots,
            TotalNodes = CountNodes(roots),
        };
        _store.SetHierarchySize(model.TotalNodes);
        _logger.LogInformation(
            "Academic tree built EnablePrograms={EnablePrograms} TotalNodes={TotalNodes} IncludeSections={IncludeSections} IncludeSubjects={IncludeSubjects}",
            model.EnablePrograms, model.TotalNodes, includeSections, includeSubjects);
        return model;
    }

    public IReadOnlyList<AcademicHierarchyNode> FlattenTree(AcademicHierarchyReadModel model)
    {
        var list = new List<AcademicHierarchyNode>();
        void Walk(IEnumerable<AcademicHierarchyNode> nodes)
        {
            foreach (var n in nodes)
            {
                list.Add(n with { Children = [] });
                if (n.Children.Count > 0) Walk(n.Children);
            }
        }
        Walk(model.Roots);
        return list;
    }

    public IReadOnlyList<AcademicHierarchyNode> GetChildren(AcademicHierarchyReadModel model, string nodeId)
        => FindByNodeId(model, nodeId)?.Children ?? [];

    public AcademicHierarchyNode? GetParent(AcademicHierarchyReadModel model, string nodeId)
    {
        var node = FindByNodeId(model, nodeId);
        if (node?.ParentNodeId is null) return null;
        return FindByNodeId(model, node.ParentNodeId);
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

    public IReadOnlySet<string> Expand(IReadOnlySet<string> expandedNodeIds, string nodeId)
    {
        var set = new HashSet<string>(expandedNodeIds, StringComparer.Ordinal);
        set.Add(nodeId);
        return set;
    }

    public IReadOnlySet<string> Collapse(IReadOnlySet<string> expandedNodeIds, string nodeId)
    {
        var set = new HashSet<string>(expandedNodeIds, StringComparer.Ordinal);
        set.Remove(nodeId);
        return set;
    }

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

    private static string NodeId(string type, int id) => $"{type}:{id}";

    private static AcademicHierarchyNode Leaf(
        string type, int id, string? parent, string name, string code, int order, int level, bool active, string status)
        => new()
        {
            NodeId = NodeId(type, id),
            ParentNodeId = parent,
            EntityId = id,
            EntityType = type,
            NodeType = type,
            DisplayName = name,
            Code = code,
            DisplayOrder = order,
            IsActive = active,
            ChildrenCount = 0,
            HasChildren = false,
            HierarchyLevel = level,
            EntityStatus = status,
            Children = [],
        };

    private static AcademicHierarchyNode WithChildren(
        string type,
        int id,
        string? parent,
        string name,
        string code,
        int order,
        int level,
        bool active,
        string status,
        IReadOnlyList<AcademicHierarchyNode> children,
        string? icon = null,
        string? themeColor = null)
        => new()
        {
            NodeId = NodeId(type, id),
            ParentNodeId = parent,
            EntityId = id,
            EntityType = type,
            NodeType = type,
            DisplayName = name,
            Code = code,
            DisplayOrder = order,
            IsActive = active,
            ChildrenCount = children.Count,
            HasChildren = children.Count > 0,
            HierarchyLevel = level,
            EntityStatus = status,
            Icon = icon,
            ThemeColor = themeColor,
            Children = children,
        };

    private static int CountNodes(IEnumerable<AcademicHierarchyNode> nodes)
        => nodes.Sum(n => 1 + CountNodes(n.Children));
}
