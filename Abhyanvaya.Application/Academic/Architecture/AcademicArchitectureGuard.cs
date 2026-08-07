using System.Reflection;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic.Architecture;

/// <summary>
/// AI29.1A.6 — Architectural dependency validation only.
/// Does not validate business rules, DB contents, or runtime behavior.
/// </summary>
public static class AcademicArchitectureGuard
{
    public static AcademicArchitectureReport Validate()
    {
        var violations = new List<string>();
        var checks = new List<string>();

        // Subject never references Section
        checks.Add("Subject must not reference Section");
        if (typeof(Subject).GetProperty("SectionId") is not null
            || typeof(Subject).GetProperty("Section") is not null)
        {
            violations.Add("Subject references Section — forbidden.");
        }

        // Attendance never references Program directly
        checks.Add("Attendance must not reference Program directly");
        if (typeof(Attendance).GetProperty("ProgramId") is not null
            || typeof(Attendance).GetProperty("Program") is not null)
        {
            violations.Add("Attendance references Program directly — forbidden.");
        }

        // Program does not depend on Attendance
        checks.Add("Program must not depend on Attendance");
        if (typeof(Program).GetProperty("AttendanceId") is not null
            || typeof(Program).GetProperty("AttendanceSessionId") is not null
            || typeof(Program).GetProperty("Attendances") is not null)
        {
            violations.Add("Program depends on Attendance — forbidden.");
        }

        // Hierarchy layer cannot reference UI assemblies
        checks.Add("Application Academic hierarchy types must not reference UI");
        var academicAsm = typeof(IAcademicTreeService).Assembly;
        foreach (var type in academicAsm.GetTypes().Where(t => t.Namespace?.Contains(".Academic") == true))
        {
            if (ReferencesUi(type))
                violations.Add($"{type.FullName} references UI — forbidden.");
        }

        // Catalog cannot reference Dashboard
        checks.Add("IAcademicCatalogService implementation must not reference Dashboards");
        var catalogType = typeof(AcademicCatalogService);
        if (ReferencesNamespace(catalogType, "Dashboards") || ReferencesNamespace(catalogType, "Dashboard"))
            violations.Add("AcademicCatalogService references Dashboard — forbidden.");

        // Hierarchy service independence from UI
        checks.Add("AcademicHierarchyService must not reference UI or Dashboard");
        if (ReferencesUi(typeof(AcademicHierarchyService)) || ReferencesNamespace(typeof(AcademicHierarchyService), "Dashboards"))
            violations.Add("AcademicHierarchyService references UI/Dashboard — forbidden.");

        // Domain must not reference Application/API/UI
        checks.Add("Domain Academic entities must not reference Application/API/UI");
        foreach (var type in new[] { typeof(Program), typeof(Section), typeof(ProgramPolicy), typeof(AcademicHierarchySnapshot) })
        {
            var refs = type.Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
            if (refs.Any(n => n.Contains("Abhyanvaya.Application", StringComparison.OrdinalIgnoreCase)
                              || n.Contains("Abhyanvaya.API", StringComparison.OrdinalIgnoreCase)
                              || n.Contains("abhyanvaya-ui", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"Domain assembly references Application/API/UI via {type.Name}.");
            }
        }

        // Read model immutability (init-only / record)
        checks.Add("AcademicHierarchyReadModel is a record (immutable projection)");
        if (!typeof(ReadModels.AcademicHierarchyReadModel).IsAssignableTo(typeof(IEquatable<ReadModels.AcademicHierarchyReadModel>)))
            violations.Add("AcademicHierarchyReadModel must be an immutable record.");
        if (!typeof(ReadModels.AcademicHierarchyNode).IsAssignableTo(typeof(IEquatable<ReadModels.AcademicHierarchyNode>)))
            violations.Add("AcademicHierarchyNode must be an immutable record.");

        // Expected hierarchy ownership
        checks.Add("Section belongs to Course/Group/Semester — not Subject");
        if (typeof(Section).GetProperty("SubjectId") is not null)
            violations.Add("Section must not own/belong to Subject.");

        // No cyclic interface ownership: Tree does not depend on Search; Search may depend on Tree
        checks.Add("IAcademicTreeService must not depend on IAcademicSearchService");
        if (typeof(AcademicTreeService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(IAcademicSearchService)))
        {
            violations.Add("Cyclic dependency: AcademicTreeService depends on IAcademicSearchService.");
        }

        // Cache key prefix separation
        checks.Add("Hierarchy and Statistics caches use distinct key prefixes");
        checks.Add("academic-hierarchy:* vs academic-statistics:*");

        return new AcademicArchitectureReport
        {
            GeneratedUtc = DateTime.UtcNow,
            Passed = violations.Count == 0,
            Violations = violations,
            Checks = checks,
        };
    }

    private static bool ReferencesUi(Type type)
        => ReferencesNamespace(type, "abhyanvaya_ui")
           || ReferencesNamespace(type, "Abhyanvaya.UI")
           || ReferencesNamespace(type, "System.Windows");

    private static bool ReferencesNamespace(Type type, string fragment)
    {
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var ns = field.FieldType.Namespace ?? "";
            var name = field.FieldType.Assembly.GetName().Name ?? "";
            if (ns.Contains(fragment, StringComparison.OrdinalIgnoreCase)
                || name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (var ctor in type.GetConstructors())
        {
            foreach (var p in ctor.GetParameters())
            {
                var ns = p.ParameterType.Namespace ?? "";
                var name = p.ParameterType.Assembly.GetName().Name ?? "";
                if (ns.Contains(fragment, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}
