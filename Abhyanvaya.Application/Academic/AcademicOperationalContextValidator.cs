using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D Prompt 16A — validates operational IDs against <see cref="IAcademicTreeService"/> only.
/// Does not query a second hierarchy source.
/// </summary>
public static class AcademicOperationalContextValidator
{
    public const string InvalidContextMessagePrefix = "Invalid academic operational context";

    public sealed record Result(bool IsValid, string? Error)
    {
        public static Result Ok() => new(true, null);
        public static Result Fail(string error) => new(false, error);
    }

    public static Result Validate(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        AcademicOperationalContext ctx)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ctx);

        var sectionIds = NormalizeIds(ctx.SectionId, ctx.SectionIds);
        var hasAny =
            ctx.ProgramId is > 0
            || ctx.CourseId is > 0
            || ctx.GroupId is > 0
            || ctx.SemesterId is > 0
            || sectionIds.Count > 0
            || ctx.SubjectId is > 0;
        if (!hasAny)
            return Result.Ok();

        AcademicHierarchyNode? program = null;
        if (ctx.ProgramId is > 0)
        {
            program = RequireNode(tree, model, $"Program:{ctx.ProgramId.Value}", "Program", out var programError);
            if (programError is not null) return Result.Fail(programError);
        }

        AcademicHierarchyNode? course = null;
        if (ctx.CourseId is > 0)
        {
            course = RequireNode(tree, model, $"Course:{ctx.CourseId.Value}", "Course", out var courseError);
            if (courseError is not null) return Result.Fail(courseError);
        }

        AcademicHierarchyNode? group = null;
        if (ctx.GroupId is > 0)
        {
            group = RequireNode(tree, model, $"Group:{ctx.GroupId.Value}", "Group", out var groupError);
            if (groupError is not null) return Result.Fail(groupError);
        }

        AcademicHierarchyNode? semester = null;
        if (ctx.SemesterId is > 0)
        {
            semester = AcademicHierarchyNodeResolver.ResolveSemester(tree, model, ctx.SemesterId.Value, ctx.GroupId);
            if (semester is null)
                return Result.Fail($"{InvalidContextMessagePrefix}: Semester was not found in the academic hierarchy.");
        }

        AcademicHierarchyNode? subject = null;
        if (ctx.SubjectId is > 0)
        {
            subject = RequireNode(tree, model, $"Subject:{ctx.SubjectId.Value}", "Subject", out var subjectError);
            if (subjectError is not null) return Result.Fail(subjectError);
        }

        var sections = new List<AcademicHierarchyNode>();
        foreach (var id in sectionIds)
        {
            var node = RequireNode(tree, model, $"Section:{id}", "Section", out var sectionError);
            if (sectionError is not null) return Result.Fail(sectionError);
            sections.Add(node!);
        }

        // Program → Course
        if (program is not null && course is not null)
        {
            if (!PathContains(tree, model, course.NodeId, program.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Course does not belong to the selected Program.");
        }

        // Course → Group
        if (course is not null && group is not null)
        {
            if (!PathContains(tree, model, group.NodeId, course.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Group does not belong to the selected Course.");
        }

        // Course + Group → Semester
        if (semester is not null)
        {
            if (course is not null && !PathContains(tree, model, semester.NodeId, course.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Semester does not belong to the selected Course.");
            if (group is not null && !PathContains(tree, model, semester.NodeId, group.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Semester does not belong to the selected Group.");
        }

        // Semester → Section (+ Course/Group when supplied)
        foreach (var section in sections)
        {
            if (semester is not null && !PathContains(tree, model, section.NodeId, semester.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Section does not belong to the selected Semester.");
            if (course is not null && !PathContains(tree, model, section.NodeId, course.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Section does not belong to the selected Course.");
            if (group is not null && !PathContains(tree, model, section.NodeId, group.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Section does not belong to the selected Group.");
        }

        // Combined sections must share the same academic scope (same Semester / Course / Group ancestors).
        if (sections.Count > 1)
        {
            var scopes = sections.Select(s => ScopeKey(tree, model, s.NodeId)).Distinct(StringComparer.Ordinal).ToList();
            if (scopes.Count > 1)
                return Result.Fail($"{InvalidContextMessagePrefix}: Combined sections must belong to the same academic scope.");
        }

        // Course + Group + Semester → Subject
        if (subject is not null)
        {
            if (course is not null && !PathContains(tree, model, subject.NodeId, course.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Subject does not belong to the selected Course.");
            if (group is not null && !PathContains(tree, model, subject.NodeId, group.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Subject does not belong to the selected Group.");
            if (semester is not null && !PathContains(tree, model, subject.NodeId, semester.NodeId))
                return Result.Fail($"{InvalidContextMessagePrefix}: Subject does not belong to the selected Semester.");
        }

        return Result.Ok();
    }

    private static AcademicHierarchyNode? RequireNode(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        string nodeId,
        string label,
        out string? error)
    {
        var node = tree.FindByNodeId(model, nodeId);
        if (node is null)
        {
            error = $"{InvalidContextMessagePrefix}: {label} was not found in the academic hierarchy.";
            return null;
        }

        error = null;
        return node;
    }

    private static bool PathContains(
        IAcademicTreeService tree,
        AcademicHierarchyReadModel model,
        string nodeId,
        string ancestorNodeId)
        => tree.GetPath(model, nodeId)
            .Any(n => string.Equals(n.NodeId, ancestorNodeId, StringComparison.Ordinal));

    private static string ScopeKey(IAcademicTreeService tree, AcademicHierarchyReadModel model, string nodeId)
    {
        var path = tree.GetPath(model, nodeId);
        var semester = path.LastOrDefault(n => n.EntityType == "Semester")?.NodeId ?? "";
        var group = path.LastOrDefault(n => n.EntityType == "Group")?.NodeId ?? "";
        var course = path.LastOrDefault(n => n.EntityType == "Course")?.NodeId ?? "";
        return $"{course}|{group}|{semester}";
    }

    private static IReadOnlyList<int> NormalizeIds(int? sectionId, IEnumerable<int>? sectionIds)
    {
        var ids = (sectionIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
        if (sectionId is > 0 && !ids.Contains(sectionId.Value))
            ids.Add(sectionId.Value);
        return ids;
    }
}
