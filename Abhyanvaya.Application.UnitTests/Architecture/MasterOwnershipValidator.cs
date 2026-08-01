using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Architecture;

/// <summary>
/// Architecture Guard: validates Master Data Ownership Matrix (AI30 AC1 / AC1.5).
/// Catalog owns masters; Scheduling must not expose duplicate CRUD surfaces.
/// </summary>
public sealed class MasterOwnershipValidator
{
    /// <summary>Source of truth: docs/AI30_MASTER_DATA_OWNERSHIP_MATRIX.md</summary>
    public static readonly IReadOnlyList<string> CatalogOwnedMasters =
    [
        "Department",
        "Course",
        "Group",
        "Semester",
        "Subject",
        "Staff",
        "Language",
        "Medium",
        "Gender",
        "Role",
    ];

    private static readonly (string Master, string[] ForbiddenPathFragments, string[] ForbiddenContentPatterns)[] ForbiddenSchedulingSurfaces =
    [
        ("Department",
            ["pages/setup/scheduling/DepartmentsPage.tsx", "DTOs/Scheduling/DepartmentDtos.cs", "DepartmentSchedulingService.cs", "IDepartmentSchedulingService.cs"],
            [@"Route\(""api/scheduling/departments""\)", @"class\s+DepartmentsController\b", @"DepartmentSchedulingDto", @"CreateDepartmentSchedulingRequest", @"listSchedulingDepartments"]),
        ("Course",
            ["pages/setup/scheduling/CoursesPage.tsx", "DTOs/Scheduling/CourseDtos.cs", "CourseSchedulingService.cs"],
            [@"Route\(""api/scheduling/courses""\)", @"class\s+CoursesController\b", @"CreateCourseSchedulingRequest"]),
        ("Group",
            ["pages/setup/scheduling/GroupsPage.tsx", "DTOs/Scheduling/GroupDtos.cs", "GroupSchedulingService.cs"],
            [@"Route\(""api/scheduling/groups""\)", @"class\s+GroupsController\b", @"CreateGroupSchedulingRequest"]),
        ("Semester",
            ["pages/setup/scheduling/SemestersPage.tsx", "DTOs/Scheduling/SemesterDtos.cs", "SemesterSchedulingService.cs"],
            [@"Route\(""api/scheduling/semesters""\)", @"class\s+SemestersController\b", @"CreateSemesterSchedulingRequest"]),
        ("Subject",
            ["pages/setup/scheduling/SubjectsPage.tsx", "DTOs/Scheduling/SubjectMasterDtos.cs", "SubjectSchedulingCrudService.cs"],
            [@"Route\(""api/scheduling/subjects""\)", @"class\s+SubjectsController\b", @"CreateSubjectSchedulingRequest"]),
        ("Staff",
            ["pages/setup/scheduling/StaffPage.tsx", "DTOs/Scheduling/StaffDtos.cs", "StaffSchedulingService.cs", "FacultySchedulingCrudService.cs"],
            [@"Route\(""api/scheduling/staff""\)", @"Route\(""api/scheduling/faculty""\)", @"class\s+StaffController\b", @"CreateStaffSchedulingRequest", @"CreateFacultySchedulingRequest"]),
        ("Language",
            ["pages/setup/scheduling/LanguagesPage.tsx", "LanguageSchedulingService.cs"],
            [@"Route\(""api/scheduling/languages""\)", @"CreateLanguageSchedulingRequest"]),
        ("Medium",
            ["pages/setup/scheduling/MediumsPage.tsx", "MediumSchedulingService.cs"],
            [@"Route\(""api/scheduling/mediums""\)", @"CreateMediumSchedulingRequest"]),
        ("Gender",
            ["pages/setup/scheduling/GendersPage.tsx", "GenderSchedulingService.cs"],
            [@"Route\(""api/scheduling/genders""\)", @"CreateGenderSchedulingRequest"]),
        ("Role",
            ["pages/setup/scheduling/RolesPage.tsx", "RoleSchedulingService.cs"],
            [@"Route\(""api/scheduling/roles""\)", @"CreateRoleSchedulingRequest"]),
    ];

    public ArchitectureOwnershipReport Validate(string? solutionRoot = null)
    {
        var root = solutionRoot ?? FindSolutionRoot();
        var findings = new List<OwnershipFinding>();
        var passed = new List<string>();

        foreach (var master in CatalogOwnedMasters)
            ValidateSingleCatalogOwner(root, master, findings, passed);

        foreach (var surface in ForbiddenSchedulingSurfaces)
            ValidateNoSchedulingCrud(root, surface.Master, surface.ForbiddenPathFragments, surface.ForbiddenContentPatterns, findings, passed);

        ValidateDepartmentSsotDetails(root, findings, passed);
        ValidateRetiredSchedulingDepartmentPermissions(root, findings, passed);

        var errors = findings.Where(f => f.Severity == OwnershipSeverity.Error).ToList();
        return new ArchitectureOwnershipReport
        {
            SolutionRoot = root,
            IsCompliant = errors.Count == 0,
            CatalogOwnedMasters = CatalogOwnedMasters,
            Findings = findings,
            PassedChecks = passed
        };
    }

    private static void ValidateSingleCatalogOwner(
        string root,
        string master,
        List<OwnershipFinding> findings,
        List<string> passed)
    {
        var apiControllers = Directory.Exists(Path.Combine(root, "Abhyanvaya.API", "Controllers"))
            ? Directory.GetFiles(Path.Combine(root, "Abhyanvaya.API", "Controllers"), "*.cs", SearchOption.AllDirectories)
            : [];

        var controllerName = master == "Staff" ? "StaffController.cs" : $"{master}Controller.cs";
        // Role is managed via TenantRbac / lookups — accept TenantRbacController or *Role*Lookup controllers in Catalog
        var catalogMatches = apiControllers
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Scheduling{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(p =>
            {
                var file = Path.GetFileName(p);
                if (master == "Role")
                    return file.Contains("Role", StringComparison.OrdinalIgnoreCase)
                           || file.Equals("TenantRbacController.cs", StringComparison.OrdinalIgnoreCase)
                           || file.Equals("StaffHubLookupsController.cs", StringComparison.OrdinalIgnoreCase);
                if (master == "Subject")
                    return file.Equals("SubjectController.cs", StringComparison.OrdinalIgnoreCase);
                return file.Equals(controllerName, StringComparison.OrdinalIgnoreCase)
                       || (master == "Staff" && file.Equals("FacultyController.cs", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        var schedulingMatches = apiControllers
            .Where(p => p.Contains($"{Path.DirectorySeparatorChar}Scheduling{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(p =>
            {
                var text = File.ReadAllText(p);
                return Regex.IsMatch(text, $@"class\s+{master}s?Controller\b")
                       || Regex.IsMatch(text, $@"Route\(""api/scheduling/{master.ToLowerInvariant()}s?""\)");
            })
            .ToList();

        if (schedulingMatches.Count > 0)
        {
            foreach (var path in schedulingMatches)
            {
                findings.Add(new OwnershipFinding
                {
                    MasterEntity = master,
                    Severity = OwnershipSeverity.Error,
                    Message = "Scheduling bounded context exposes a Catalog-owned master controller/route.",
                    Path = Rel(root, path)
                });
            }
        }
        else
        {
            passed.Add($"{master}: no Scheduling controller/route ownership");
        }

        if (master is "Department" or "Course" or "Group" or "Semester" or "Subject" or "Staff" or "Language" or "Medium" or "Gender")
        {
            var exact = apiControllers.Count(p =>
                Path.GetFileName(p).Equals(controllerName, StringComparison.OrdinalIgnoreCase)
                && !p.Contains($"{Path.DirectorySeparatorChar}Scheduling{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            if (exact != 1 && master != "Staff")
            {
                // Staff may also have FacultyController — allow 1 StaffController
                findings.Add(new OwnershipFinding
                {
                    MasterEntity = master,
                    Severity = OwnershipSeverity.Error,
                    Message = $"Expected exactly one Catalog {controllerName}; found {exact}.",
                    Path = "Abhyanvaya.API/Controllers"
                });
            }
            else if (master == "Staff")
            {
                var staffCount = apiControllers.Count(p => Path.GetFileName(p).Equals("StaffController.cs", StringComparison.OrdinalIgnoreCase));
                if (staffCount != 1)
                {
                    findings.Add(new OwnershipFinding
                    {
                        MasterEntity = master,
                        Severity = OwnershipSeverity.Error,
                        Message = $"Expected exactly one StaffController; found {staffCount}.",
                        Path = "Abhyanvaya.API/Controllers"
                    });
                }
                else passed.Add("Staff: single StaffController in Catalog/API");
            }
            else
            {
                passed.Add($"{master}: single Catalog controller ({controllerName})");
            }
        }
        else if (catalogMatches.Count > 0)
        {
            passed.Add($"{master}: Catalog ownership surface present");
        }
    }

    private static void ValidateNoSchedulingCrud(
        string root,
        string master,
        string[] forbiddenPathFragments,
        string[] forbiddenContentPatterns,
        List<OwnershipFinding> findings,
        List<string> passed)
    {
        var localErrors = 0;

        foreach (var fragment in forbiddenPathFragments)
        {
            var matches = EnumerateSourceFiles(root)
                .Where(p => p.Replace('\\', '/').Contains(fragment.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var match in matches)
            {
                localErrors++;
                findings.Add(new OwnershipFinding
                {
                    MasterEntity = master,
                    Severity = OwnershipSeverity.Error,
                    Message = $"Forbidden Scheduling/Catalog-duplicate artifact exists ({fragment}).",
                    Path = Rel(root, match)
                });
            }
        }

        var scanRoots = new[]
        {
            Path.Combine(root, "Abhyanvaya.API", "Controllers", "Scheduling"),
            Path.Combine(root, "Abhyanvaya.Application", "Scheduling"),
            Path.Combine(root, "Abhyanvaya.Application", "DTOs", "Scheduling"),
            Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "scheduling"),
            Path.Combine(root, "abhyanvaya-ui", "src", "services"),
            Path.Combine(root, "abhyanvaya-ui", "src", "routes"),
        };

        foreach (var scanRoot in scanRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.GetFiles(scanRoot, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = File.ReadAllText(file);
                foreach (var pattern in forbiddenContentPatterns)
                {
                    if (!Regex.IsMatch(text, pattern))
                        continue;

                    // AC1 backward-compat redirect to Catalog is allowed
                    if (master == "Department"
                        && (file.EndsWith("AppRoutes.tsx", StringComparison.OrdinalIgnoreCase)
                            || file.Contains($"{Path.DirectorySeparatorChar}routes{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                        && text.Contains("/setup/departments", StringComparison.Ordinal))
                        continue;

                    localErrors++;
                    findings.Add(new OwnershipFinding
                    {
                        MasterEntity = master,
                        Severity = OwnershipSeverity.Error,
                        Message = $"Forbidden Scheduling CRUD/lookup pattern matched: {pattern}",
                        Path = Rel(root, file)
                    });
                }
            }
        }

        if (localErrors == 0)
            passed.Add($"{master}: no forbidden Scheduling CRUD surfaces detected in scan");
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var skip = new[] { "bin", "obj", "node_modules", ".git", "dist", "TestResults" };
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(dir); }
            catch { continue; }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (skip.Any(s => name.Equals(s, StringComparison.OrdinalIgnoreCase)))
                    continue;
                stack.Push(child);
            }

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); }
            catch { continue; }

            foreach (var file in files)
                yield return file;
        }
    }

    private static void ValidateDepartmentSsotDetails(
        string root,
        List<OwnershipFinding> findings,
        List<string> passed)
    {
        var deptControllers = Directory.GetFiles(Path.Combine(root, "Abhyanvaya.API"), "*Department*Controller*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        if (deptControllers.Count != 1)
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Error,
                Message = $"Expected exactly one Department controller file; found {deptControllers.Count}.",
                Path = string.Join("; ", deptControllers.Select(p => Rel(root, p)))
            });
        }
        else
        {
            passed.Add("Department: exactly one DepartmentController file");
        }

        var schedulingDeptDto = Path.Combine(root, "Abhyanvaya.Application", "DTOs", "Scheduling", "DepartmentDtos.cs");
        if (File.Exists(schedulingDeptDto))
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Error,
                Message = "Duplicate Scheduling Department DTO hierarchy still present.",
                Path = Rel(root, schedulingDeptDto)
            });
        }
        else
        {
            passed.Add("Department: no Scheduling Department DTO hierarchy");
        }

        var catalogDeptDto = Path.Combine(root, "Abhyanvaya.Application", "DTOs", "Department", "DepartmentDtos.cs");
        if (!File.Exists(catalogDeptDto))
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Error,
                Message = "Catalog Department DTO hierarchy missing.",
                Path = "Abhyanvaya.Application/DTOs/Department/DepartmentDtos.cs"
            });
        }
        else
        {
            passed.Add("Department: Catalog Department DTO hierarchy present");
        }

        // Read-only Scheduling Department repository helper is allowed (consumes Catalog entity)
        var deptRepo = Path.Combine(root, "Abhyanvaya.Infrastructure", "Persistence", "Repositories", "Scheduling", "DepartmentRepository.cs");
        if (File.Exists(deptRepo))
        {
            var text = File.ReadAllText(deptRepo);
            if (Regex.IsMatch(text, @"Task\s+AddAsync\s*\(") || Regex.IsMatch(text, @"CodeExistsAsync"))
            {
                findings.Add(new OwnershipFinding
                {
                    MasterEntity = "Department",
                    Severity = OwnershipSeverity.Error,
                    Message = "Scheduling DepartmentRepository must remain read-only (no Add/CodeExists CRUD helpers).",
                    Path = Rel(root, deptRepo)
                });
            }
            else
            {
                passed.Add("Department: Scheduling DepartmentRepository is read-only Catalog consumer");
            }
        }
    }

    private static void ValidateRetiredSchedulingDepartmentPermissions(
        string root,
        List<OwnershipFinding> findings,
        List<string> passed)
    {
        var keysPath = Path.Combine(root, "Abhyanvaya.Domain", "Authorization", "PermissionKeys.cs");
        if (!File.Exists(keysPath))
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Error,
                Message = "PermissionKeys.cs not found.",
                Path = keysPath
            });
            return;
        }

        var text = File.ReadAllText(keysPath);
        // Active All array must not include Scheduling.Department.*
        var allMatch = Regex.Match(text, @"public static IReadOnlyList<string> All[\s\S]*?\]\s*;", RegexOptions.Multiline);
        if (!allMatch.Success)
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Warning,
                Message = "Could not locate PermissionKeys.All for retirement check.",
                Path = Rel(root, keysPath)
            });
            return;
        }

        if (allMatch.Value.Contains("SchedulingDepartmentView", StringComparison.Ordinal)
            || allMatch.Value.Contains("SchedulingDepartmentManage", StringComparison.Ordinal))
        {
            findings.Add(new OwnershipFinding
            {
                MasterEntity = "Department",
                Severity = OwnershipSeverity.Error,
                Message = "Retired Scheduling.Department permissions still listed in PermissionKeys.All.",
                Path = Rel(root, keysPath)
            });
        }
        else
        {
            passed.Add("Department: Scheduling.Department permissions excluded from PermissionKeys.All");
        }
    }

    public static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Abhyanvaya.sln from test base directory.");
    }

    private static string Rel(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
