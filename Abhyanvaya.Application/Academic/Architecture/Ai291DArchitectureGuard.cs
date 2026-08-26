using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic.Architecture;

/// <summary>
/// AI29.1D Prompt 21 / 21A — Architecture compliance gate.
/// Enforces UI → API/Application contracts → Domain Services.
/// Existing backend services remain authoritative; HTTP/API calls from UI are allowed.
/// </summary>
public static class Ai291DArchitectureGuard
{
    private static readonly string[] UiSourceExtensions = [".ts", ".tsx", ".js", ".jsx"];

    private static readonly (string Name, Regex Pattern)[] ForbiddenDataAccessPatterns =
    [
        ("EF Core / EntityFramework", new Regex(@"EntityFrameworkCore|\bUseSqlServer\b|\bUseNpgsql\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("DbContext", new Regex(@"\bDbContext\b|\bIApplicationDbContext\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Direct DB drivers", new Regex(@"\bfrom\s+['""](pg|mssql|mysql2|better-sqlite3|sqlite3|tedious|oracledb)['""]|require\(\s*['""](pg|mssql|mysql2|knex|typeorm)['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("ORM clients", new Regex(@"@prisma/client|\btypeorm\b|\bsequelize\b|\bknex\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        // SQL-shaped statements only — not English "Select … from …" / MUI <Select label="From".
        ("Raw SQL table access", new Regex(@"\bSELECT\s+(\*|TOP\b|DISTINCT\b)|\bINSERT\s+INTO\s+[A-Za-z_]|\bUPDATE\s+[A-Za-z_][\w\.]*\s+SET\s+[A-Za-z_]|\bDELETE\s+FROM\s+[A-Za-z_]|\bCREATE\s+TABLE\s+[A-Za-z_]|\bFROM\s+dbo\.", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Allocation DB entities", new Regex(@"\bAllocationEngineScenario\b|\bAllocationScenarioVersion\b|\bAllocationEngineSession\b|\bSectionAllocationSnapshot\b", RegexOptions.Compiled)),
        ("Scheduling DB entities", new Regex(@"\bAbhyanvaya\.Domain\.Entities\.(Scheduling|Academic)\b|\bDbSet\s*<\s*(TimetableEntry|TimetableSection|SubjectAllocation)\b", RegexOptions.Compiled)),
        ("Attendance DB entities", new Regex(@"\bAbhyanvaya\.Domain\.Entities\.Attendance\b|\bDbSet\s*<\s*Attendance\b|\bnew\s+AttendanceSessionSection\b", RegexOptions.Compiled)),
    ];

    private static readonly (string Name, Regex Pattern)[] ForbiddenAuthorityPatterns =
    [
        ("Authoritative capacity calculation", new Regex(@"\b(calculate|compute|derive)AuthoritativeCapacity\b|\bauthoritativeCapacity\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Authoritative allocation scoring", new Regex(@"\b(calculate|compute|derive)AllocationScore\b|\bnew\s+AllocationScoreCalculator\b|\bclass\s+AllocationScoreCalculator\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Timetable session resolution", new Regex(@"\bnew\s+AttendanceSessionResolver\b|\bclass\s+AttendanceSessionResolver\b|\bimplements\s+IAttendanceSessionResolver\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Attendance eligibility engine", new Regex(@"\b(implement|evaluate|resolve)AttendanceEligibility\b|\bnew\s+AttendanceEligibility|\bclass\s+AttendanceEligibility", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("SectionGroup resolution engine", new Regex(@"\b(resolve|implement)SectionGroup\b|\bnew\s+SectionGroupResolver\b|\bclass\s+SectionGroupResolver\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Lifecycle transition engine", new Regex(@"\bnew\s+AllocationScenarioLifecycleService\b|\bclass\s+AllocationScenarioLifecycleService\b|\bimplementLifecycleTransition", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Governance rules engine", new Regex(@"\bnew\s+AllocationGovernanceService\b|\bclass\s+AllocationGovernanceService\b|\bimplementGovernanceRule", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    private static readonly string[] ForbiddenNpmPackages =
    [
        "@prisma/client", "typeorm", "sequelize", "knex", "pg", "mssql", "mysql2", "better-sqlite3", "tedious",
    ];

    private static readonly string[] ForbiddenDomainProjectTokens =
    [
        "Abhyanvaya.Application", "Abhyanvaya.API", "abhyanvaya-ui", "Abhyanvaya.UI",
    ];

    public static Ai291DArchitectureComplianceReport Validate(string? repositoryRoot = null)
        => Validate(new Ai291DArchitectureGuardOptions { RepositoryRoot = repositoryRoot });

    public static Ai291DArchitectureComplianceReport Validate(Ai291DArchitectureGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var checks = new List<string>();
        var violations = new List<string>();
        var notes = new List<string>();

        checks.Add("Required layering: UI → API / Application Contracts → Domain Services");
        checks.Add("Domain assembly/project must not reference Application / API / UI");
        checks.Add("Application Academic types must not reference UI (fields/properties/methods/generics/attributes)");
        checks.Add("UI must not access EF Core / DbContext / database tables");
        checks.Add("UI must not access allocation / scheduling / attendance database entities");
        checks.Add("UI must not calculate authoritative capacity / allocation scores");
        checks.Add("UI must not implement AttendanceSessionResolver / eligibility / SectionGroup / lifecycle / governance");
        checks.Add("Existing backend services remain authoritative");
        checks.Add("Compliance status: FULLY_VERIFIED | PARTIALLY_VERIFIED | FAILED");

        ValidateDomainDoesNotReferenceUpperLayers(
            options.DomainAssemblyOverride ?? typeof(Section).Assembly,
            options.DomainCsprojPathOverride ?? TryResolveDomainCsproj(options.RepositoryRoot),
            violations);

        ValidateApplicationDoesNotReferenceUi(options.AdditionalApplicationTypesToInspect, violations);

        var backend = ValidateBackendAuthority(checks, violations);

        var platformEvaluator = options.PlatformBoundaryEvaluator
                                ?? (() => AcademicArchitectureGuard.ValidateAllocationBoundaries());
        var allocationReport = platformEvaluator();
        backend = backend with { ExistingPlatformGuardPassed = allocationReport.Passed };
        if (!allocationReport.Passed)
        {
            foreach (var v in allocationReport.Violations)
                violations.Add($"Platform allocation guard: {v}");
        }

        var platformBoundaryPassed = allocationReport.Passed;
        var backendChecksPassed = backend.AllocationEnginePresent
                                  && backend.AttendanceSessionResolverPresent
                                  && backend.AllocationLifecycleServicePresent
                                  && backend.AllocationGovernanceServicePresent
                                  && backend.SectionCapacityEnginePresent
                                  && !violations.Any(IsBackendAuthorityViolation);

        Ai291DUiScanSummary uiScan;
        if (options.SkipUiScan)
        {
            notes.Add("UI scan skipped by options — backend/assembly checks only.");
            uiScan = new Ai291DUiScanSummary { Executed = false };
        }
        else
        {
            var uiRoot = options.UiSourceRootOverride ?? ResolveUiRoot(options.RepositoryRoot);
            if (uiRoot is null)
            {
                notes.Add("UI source tree not found — skipped file scan (assembly/backend checks still applied).");
                uiScan = new Ai291DUiScanSummary { Executed = false };
            }
            else
            {
                uiScan = ScanUiSource(uiRoot, violations);
                var packagePath = options.PackageJsonPathOverride
                                  ?? Path.GetFullPath(Path.Combine(uiRoot, "..", "package.json"));
                ScanUiPackageJsonPath(packagePath, violations);
                notes.Add($"UI source scanned at {uiRoot}");
            }
        }

        var status = ResolveComplianceStatus(uiScan.Executed, violations.Count);
        var statusText = Ai291DArchitectureComplianceStatuses.ToCiString(status);

        if (status == Ai291DArchitectureComplianceStatus.PartiallyVerified)
            notes.Add("Status PARTIALLY_VERIFIED — not equivalent to FULLY_VERIFIED (UI scan did not execute).");

        return new Ai291DArchitectureComplianceReport
        {
            GeneratedUtc = DateTime.UtcNow,
            Passed = status != Ai291DArchitectureComplianceStatus.Failed,
            Status = statusText,
            ComplianceStatus = status,
            FullyVerified = status == Ai291DArchitectureComplianceStatus.FullyVerified,
            UiScanExecuted = uiScan.Executed,
            BackendChecksPassed = backendChecksPassed,
            PlatformBoundaryPassed = platformBoundaryPassed,
            ViolationCount = violations.Count,
            Checks = checks,
            Violations = violations,
            Notes = notes,
            UiScan = uiScan,
            BackendAuthority = backend,
        };
    }

    /// <summary>
    /// CI status rules:
    /// UI available + zero violations → FULLY_VERIFIED;
    /// UI unavailable + zero violations → PARTIALLY_VERIFIED;
    /// any violation → FAILED.
    /// </summary>
    public static Ai291DArchitectureComplianceStatus ResolveComplianceStatus(bool uiScanExecuted, int violationCount)
    {
        if (violationCount > 0)
            return Ai291DArchitectureComplianceStatus.Failed;
        return uiScanExecuted
            ? Ai291DArchitectureComplianceStatus.FullyVerified
            : Ai291DArchitectureComplianceStatus.PartiallyVerified;
    }

    public static string? TryResolveRepositoryRoot(string? start = null)
    {
        var dir = new DirectoryInfo(string.IsNullOrWhiteSpace(start) ? AppContext.BaseDirectory : start);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var ui = Path.Combine(dir.FullName, "abhyanvaya-ui");
            if (Directory.Exists(ui) && File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln")))
                return dir.FullName;
            if (Directory.Exists(ui))
                return dir.FullName;
        }
        return null;
    }

    public static string? ResolveUiRoot(string? repositoryRoot = null)
    {
        var root = repositoryRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = TryResolveRepositoryRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;
        var ui = Path.Combine(root, "abhyanvaya-ui", "src");
        return Directory.Exists(ui) ? ui : null;
    }

    /// <summary>Inspect a type for UI assembly/namespace leakage (fields, properties, methods, generics, attributes, bases).</summary>
    public static IReadOnlyList<string> DescribeUiReferences(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var hits = new List<string>();
        CollectUiReferences(type, hits, new HashSet<Type>());
        return hits;
    }

    public static bool TypeReferencesUi(Type type) => DescribeUiReferences(type).Count > 0;

    /// <summary>Domain dependency check against assembly names and optional csproj XML/text.</summary>
    public static IReadOnlyList<string> FindForbiddenDomainDependencies(
        IEnumerable<string> referencedAssemblyNames,
        string? csprojXml = null)
    {
        var violations = new List<string>();
        foreach (var name in referencedAssemblyNames)
        {
            if (IsForbiddenDomainDependencyToken(name))
                violations.Add($"Domain assembly references forbidden dependency '{name}'.");
        }

        if (!string.IsNullOrWhiteSpace(csprojXml))
        {
            foreach (var token in ForbiddenDomainProjectTokens)
            {
                if (csprojXml.Contains(token, StringComparison.OrdinalIgnoreCase)
                    && (csprojXml.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase)
                        || csprojXml.Contains("PackageReference", StringComparison.OrdinalIgnoreCase)
                        || csprojXml.Contains("Reference Include", StringComparison.OrdinalIgnoreCase)))
                {
                    // Narrow: only flag when the token appears inside a Reference-like Include attribute.
                    if (HasCsprojReferenceTo(csprojXml, token))
                        violations.Add($"Domain project file references forbidden dependency '{token}'.");
                }
            }
        }

        return violations;
    }

    /// <summary>Scan a single UI source text blob (tests / CI probes).</summary>
    public static (int DataHits, int AuthorityHits, IReadOnlyList<string> Violations) ScanUiText(
        string relativePath,
        string sourceText)
    {
        var violations = new List<string>();
        var dataHits = 0;
        var authorityHits = 0;
        foreach (var (name, pattern) in ForbiddenDataAccessPatterns)
        {
            if (!pattern.IsMatch(sourceText)) continue;
            dataHits++;
            violations.Add($"UI data-access violation ({name}) in {relativePath}");
        }

        foreach (var (name, pattern) in ForbiddenAuthorityPatterns)
        {
            if (!pattern.IsMatch(sourceText)) continue;
            authorityHits++;
            violations.Add($"UI authority violation ({name}) in {relativePath}");
        }

        return (dataHits, authorityHits, violations);
    }

    /// <summary>Validate package.json dependency text for forbidden ORM/DB packages.</summary>
    public static IReadOnlyList<string> ValidatePackageJsonText(string packageJson)
    {
        var violations = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(packageJson);
            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (!doc.RootElement.TryGetProperty(section, out var obj) || obj.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var prop in obj.EnumerateObject())
                    deps.Add(prop.Name);
            }

            foreach (var banned in ForbiddenNpmPackages)
            {
                if (deps.Contains(banned))
                    violations.Add($"UI package.json includes forbidden data-access dependency '{banned}'.");
            }
        }
        catch (Exception ex)
        {
            violations.Add($"Unable to parse UI package.json for dependency guard: {ex.Message}");
        }

        return violations;
    }

    private static bool IsBackendAuthorityViolation(string v) =>
        v.Contains("missing —", StringComparison.Ordinal)
        || v.StartsWith("Parallel allocation engine", StringComparison.Ordinal);

    private static void ValidateDomainDoesNotReferenceUpperLayers(
        Assembly domainAssembly,
        string? domainCsprojPath,
        List<string> violations)
    {
        var refNames = domainAssembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        string? csprojXml = null;
        if (!string.IsNullOrWhiteSpace(domainCsprojPath) && File.Exists(domainCsprojPath))
            csprojXml = File.ReadAllText(domainCsprojPath);

        foreach (var v in FindForbiddenDomainDependencies(refNames, csprojXml))
            violations.Add(v);
    }

    private static string? TryResolveDomainCsproj(string? repositoryRoot)
    {
        var root = repositoryRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = TryResolveRepositoryRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;
        var path = Path.Combine(root, "Abhyanvaya.Domain", "Abhyanvaya.Domain.csproj");
        return File.Exists(path) ? path : null;
    }

    private static bool HasCsprojReferenceTo(string csprojXml, string token)
    {
        try
        {
            var doc = XDocument.Parse(csprojXml);
            foreach (var el in doc.Descendants())
            {
                var local = el.Name.LocalName;
                if (local is not ("ProjectReference" or "PackageReference" or "Reference"))
                    continue;
                var include = (string?)el.Attribute("Include") ?? "";
                if (include.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // Fallback textual Include=...token...
            return Regex.IsMatch(
                csprojXml,
                $@"<(ProjectReference|PackageReference|Reference)\b[^>]*Include\s*=\s*""[^""]*{Regex.Escape(token)}",
                RegexOptions.IgnoreCase);
        }

        return false;
    }

    private static bool IsForbiddenDomainDependencyToken(string name) =>
        name.Contains("Abhyanvaya.Application", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Abhyanvaya.API", StringComparison.OrdinalIgnoreCase)
        || name.Contains("abhyanvaya-ui", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Abhyanvaya.UI", StringComparison.OrdinalIgnoreCase);

    private static void ValidateApplicationDoesNotReferenceUi(
        IReadOnlyList<Type>? additionalTypes,
        List<string> violations)
    {
        var academicAsm = typeof(Ai291DArchitectureGuard).Assembly;
        IEnumerable<Type> types;
        try
        {
            types = academicAsm.GetTypes().Where(t => t.Namespace?.Contains(".Academic", StringComparison.Ordinal) == true);
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null && t.Namespace?.Contains(".Academic", StringComparison.Ordinal) == true)!;
        }

        foreach (var type in types)
        {
            foreach (var hit in DescribeUiReferences(type))
                violations.Add($"{type.FullName} references UI ({hit}) — Application must not depend on UI.");
        }

        if (additionalTypes is null) return;
        foreach (var type in additionalTypes)
        {
            foreach (var hit in DescribeUiReferences(type))
                violations.Add($"{type.FullName} references UI ({hit}) — Application must not depend on UI.");
        }
    }

    private static Ai291DBackendAuthoritySummary ValidateBackendAuthority(List<string> checks, List<string> violations)
    {
        var engine = typeof(AllocationEngine);
        var resolver = typeof(AttendanceSessionResolver);
        var lifecycle = typeof(AllocationScenarioLifecycleService);
        var governance = typeof(IAllocationGovernanceService);
        var capacity = typeof(ISectionCapacityEngine);

        checks.Add($"Backend authority: {engine.Name}");
        checks.Add($"Backend authority: {resolver.Name}");
        checks.Add($"Backend authority: {lifecycle.Name}");
        checks.Add($"Backend authority: {governance.Name}");
        checks.Add($"Backend authority: {capacity.Name}");

        var summary = new Ai291DBackendAuthoritySummary
        {
            AllocationEnginePresent = engine.IsClass,
            AttendanceSessionResolverPresent = resolver.IsClass,
            AllocationLifecycleServicePresent = lifecycle.IsClass,
            AllocationGovernanceServicePresent = governance.IsInterface,
            SectionCapacityEnginePresent = capacity.IsInterface,
        };

        if (!summary.AllocationEnginePresent)
            violations.Add("AllocationEngine missing — allocation scoring/placement authority unavailable.");
        if (!summary.AttendanceSessionResolverPresent)
            violations.Add("AttendanceSessionResolver missing — timetable session resolution authority unavailable.");
        if (!summary.AllocationLifecycleServicePresent)
            violations.Add("AllocationScenarioLifecycleService missing — lifecycle authority unavailable.");
        if (!summary.AllocationGovernanceServicePresent)
            violations.Add("IAllocationGovernanceService missing — governance authority unavailable.");
        if (!summary.SectionCapacityEnginePresent)
            violations.Add("ISectionCapacityEngine missing — capacity authority unavailable.");

        var parallel = engine.Assembly.GetTypes()
            .Where(t => t.Name.Contains("AllocationEngine", StringComparison.OrdinalIgnoreCase)
                        && t.Name.Contains("V2", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name)
            .ToList();
        if (parallel.Count > 0)
            violations.Add($"Parallel allocation engine types forbidden: {string.Join(", ", parallel)}");

        return summary;
    }

    private static Ai291DUiScanSummary ScanUiSource(string uiSrcRoot, List<string> violations)
    {
        var files = Directory.EnumerateFiles(uiSrcRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => UiSourceExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataHits = 0;
        var authorityHits = 0;

        foreach (var file in files)
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            var relative = Path.GetRelativePath(uiSrcRoot, file).Replace('\\', '/');
            var (d, a, fileViolations) = ScanUiText(relative, text);
            dataHits += d;
            authorityHits += a;
            violations.AddRange(fileViolations);
        }

        return new Ai291DUiScanSummary
        {
            Executed = true,
            UiRoot = uiSrcRoot,
            FilesScanned = files.Count,
            ForbiddenDataAccessHits = dataHits,
            ForbiddenAuthorityHits = authorityHits,
        };
    }

    private static void ScanUiPackageJsonPath(string packagePath, List<string> violations)
    {
        if (!File.Exists(packagePath)) return;
        try
        {
            violations.AddRange(ValidatePackageJsonText(File.ReadAllText(packagePath)));
        }
        catch (Exception ex)
        {
            violations.Add($"Unable to parse UI package.json for dependency guard: {ex.Message}");
        }
    }

    private static void CollectUiReferences(Type type, List<string> hits, HashSet<Type> visited)
    {
        if (!visited.Add(type)) return;

        if (IsUiType(type))
        {
            hits.Add($"type:{type.FullName}");
            return;
        }

        if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            if (IsUiType(type.BaseType))
                hits.Add($"base:{type.BaseType.FullName}");
            else
                CollectUiReferences(type.BaseType, hits, visited);
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (IsUiType(iface))
                hits.Add($"interface:{iface.FullName}");
        }

        const BindingFlags members =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(members))
        {
            if (IsUiType(field.FieldType))
                hits.Add($"field:{field.Name}:{field.FieldType.FullName}");
            AddGenericUiHits(field.FieldType, $"field-generic:{field.Name}", hits);
        }

        foreach (var prop in type.GetProperties(members))
        {
            if (IsUiType(prop.PropertyType))
                hits.Add($"property:{prop.Name}:{prop.PropertyType.FullName}");
            AddGenericUiHits(prop.PropertyType, $"property-generic:{prop.Name}", hits);
        }

        foreach (var method in type.GetMethods(members))
        {
            if (method.IsSpecialName) continue;

            if (IsUiType(method.ReturnType))
                hits.Add($"return:{method.Name}:{method.ReturnType.FullName}");
            AddGenericUiHits(method.ReturnType, $"return-generic:{method.Name}", hits);

            foreach (var p in method.GetParameters())
            {
                if (IsUiType(p.ParameterType))
                    hits.Add($"parameter:{method.Name}:{p.Name}:{p.ParameterType.FullName}");
                AddGenericUiHits(p.ParameterType, $"parameter-generic:{method.Name}:{p.Name}", hits);
            }
        }

        foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var p in ctor.GetParameters())
            {
                if (IsUiType(p.ParameterType))
                    hits.Add($"ctor-parameter:{p.Name}:{p.ParameterType.FullName}");
                AddGenericUiHits(p.ParameterType, $"ctor-parameter-generic:{p.Name}", hits);
            }
        }

        foreach (var attr in type.GetCustomAttributesData())
        {
            if (IsUiType(attr.AttributeType))
                hits.Add($"attribute:{attr.AttributeType.FullName}");
            foreach (var arg in attr.ConstructorArguments)
                AddAttributeTypedArg(arg, hits);
            foreach (var named in attr.NamedArguments)
                AddAttributeTypedArg(named.TypedValue, hits);
        }
    }

    private static void AddAttributeTypedArg(CustomAttributeTypedArgument arg, List<string> hits)
    {
        if (arg.ArgumentType == typeof(Type) && arg.Value is Type t && IsUiType(t))
            hits.Add($"attribute-arg:{t.FullName}");
        else if (IsUiType(arg.ArgumentType))
            hits.Add($"attribute-arg-type:{arg.ArgumentType.FullName}");
    }

    private static void AddGenericUiHits(Type type, string prefix, List<string> hits)
    {
        var current = Unwrap(type);
        if (!current.IsGenericType) return;
        foreach (var arg in current.GetGenericArguments())
        {
            if (IsUiType(arg))
                hits.Add($"{prefix}:{arg.FullName}");
            AddGenericUiHits(arg, prefix, hits);
        }
    }

    private static Type Unwrap(Type type)
    {
        var current = type;
        while (current.IsByRef || current.IsPointer)
            current = current.GetElementType()!;
        if (current.IsArray)
            return Unwrap(current.GetElementType()!);
        if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Nullable<>))
            return current.GetGenericArguments()[0];
        return current;
    }

    private static bool IsUiType(Type? type)
    {
        if (type is null || type == typeof(void)) return false;
        var current = Unwrap(type);

        if (LooksLikeUi(current))
            return true;

        if (current.IsGenericType)
        {
            foreach (var arg in current.GetGenericArguments())
            {
                if (IsUiType(arg))
                    return true;
            }
        }

        return false;
    }

    private static bool LooksLikeUi(Type type)
    {
        var ns = type.Namespace ?? "";
        var asm = type.Assembly.GetName().Name ?? "";
        return ns.Contains("abhyanvaya_ui", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("Abhyanvaya.UI", StringComparison.OrdinalIgnoreCase)
               || asm.Contains("abhyanvaya-ui", StringComparison.OrdinalIgnoreCase)
               || asm.Contains("Abhyanvaya.UI", StringComparison.OrdinalIgnoreCase);
    }
}
