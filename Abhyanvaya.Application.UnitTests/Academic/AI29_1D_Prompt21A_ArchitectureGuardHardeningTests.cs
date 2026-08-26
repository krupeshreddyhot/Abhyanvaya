using System.Text.Json;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.UI.ArchitectureProbe;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D Prompt 21A — architecture guard hardening (status + dependency depth).</summary>
[Collection("AI29.1D.ArchitectureGuard")]
public sealed class AI29_1D_Prompt21A_ArchitectureGuardHardeningTests
{
    private static string RepoRoot()
    {
        var root = Ai291DArchitectureGuard.TryResolveRepositoryRoot(AppContext.BaseDirectory);
        Assert.False(string.IsNullOrWhiteSpace(root), "Repository root with abhyanvaya-ui must be resolvable.");
        return root!;
    }

    [Fact]
    public void Full_Verification_When_Ui_Exists()
    {
        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        Assert.Equal(Ai291DArchitectureComplianceStatuses.FullyVerified, report.Status);
        Assert.Equal(Ai291DArchitectureComplianceStatus.FullyVerified, report.ComplianceStatus);
        Assert.True(report.FullyVerified);
        Assert.True(report.UiScanExecuted);
        Assert.True(report.Passed);
        Assert.True(report.BackendChecksPassed);
        Assert.True(report.PlatformBoundaryPassed);
        Assert.Equal(0, report.ViolationCount);
    }

    [Fact]
    public void Partial_Verification_When_Ui_Source_Unavailable()
    {
        var report = Ai291DArchitectureGuard.Validate(new Ai291DArchitectureGuardOptions
        {
            RepositoryRoot = RepoRoot(),
            SkipUiScan = true,
        });

        Assert.Equal(Ai291DArchitectureComplianceStatuses.PartiallyVerified, report.Status);
        Assert.False(report.FullyVerified);
        Assert.False(report.UiScanExecuted);
        Assert.True(report.Passed, "PARTIALLY_VERIFIED must remain Passed=true for backward compatibility.");
        Assert.Equal(0, report.ViolationCount);
        Assert.Contains(report.Notes, n => n.Contains("PARTIALLY_VERIFIED", StringComparison.Ordinal));
    }

    [Fact]
    public void Failure_When_Ui_Forbidden_Dependency_Detected()
    {
        var scan = Ai291DArchitectureGuard.ScanUiText(
            "probe/BadDbAccess.ts",
            "import { DbContext } from './fake'; const x = new DbContext();");
        Assert.True(scan.DataHits > 0);
        Assert.Contains(scan.Violations, v => v.Contains("DbContext", StringComparison.OrdinalIgnoreCase));

        var status = Ai291DArchitectureGuard.ResolveComplianceStatus(uiScanExecuted: true, violationCount: scan.Violations.Count);
        Assert.Equal(Ai291DArchitectureComplianceStatus.Failed, status);
        Assert.Equal(Ai291DArchitectureComplianceStatuses.Failed, Ai291DArchitectureComplianceStatuses.ToCiString(status));
    }

    [Fact]
    public void Failure_When_Forbidden_Authority_Pattern_Detected()
    {
        var scan = Ai291DArchitectureGuard.ScanUiText(
            "probe/BadAuthority.ts",
            "export class AttendanceSessionResolver { }");
        Assert.True(scan.AuthorityHits > 0);
        Assert.Contains(scan.Violations, v => v.Contains("Timetable session resolution", StringComparison.Ordinal));
        Assert.Equal(
            Ai291DArchitectureComplianceStatus.Failed,
            Ai291DArchitectureGuard.ResolveComplianceStatus(true, scan.Violations.Count));
    }

    [Fact]
    public void Application_Ui_Dependency_Through_Property()
    {
        var hits = Ai291DArchitectureGuard.DescribeUiReferences(typeof(AcademicProbeWithUiProperty));
        Assert.Contains(hits, h => h.StartsWith("property:", StringComparison.Ordinal));
        Assert.True(Ai291DArchitectureGuard.TypeReferencesUi(typeof(AcademicProbeWithUiProperty)));

        var report = Ai291DArchitectureGuard.Validate(new Ai291DArchitectureGuardOptions
        {
            RepositoryRoot = RepoRoot(),
            SkipUiScan = true,
            AdditionalApplicationTypesToInspect = [typeof(AcademicProbeWithUiProperty)],
        });
        Assert.Equal(Ai291DArchitectureComplianceStatuses.Failed, report.Status);
        Assert.Contains(report.Violations, v => v.Contains(nameof(AcademicProbeWithUiProperty), StringComparison.Ordinal));
    }

    [Fact]
    public void Application_Ui_Dependency_Through_Method_Parameter()
    {
        var hits = Ai291DArchitectureGuard.DescribeUiReferences(typeof(AcademicProbeWithUiParameter));
        Assert.Contains(hits, h => h.StartsWith("parameter:", StringComparison.Ordinal));

        var report = Ai291DArchitectureGuard.Validate(new Ai291DArchitectureGuardOptions
        {
            RepositoryRoot = RepoRoot(),
            SkipUiScan = true,
            AdditionalApplicationTypesToInspect = [typeof(AcademicProbeWithUiParameter)],
        });
        Assert.Equal(Ai291DArchitectureComplianceStatuses.Failed, report.Status);
    }

    [Fact]
    public void Application_Ui_Dependency_Through_Return_Type()
    {
        var hits = Ai291DArchitectureGuard.DescribeUiReferences(typeof(AcademicProbeWithUiReturn));
        Assert.Contains(hits, h => h.StartsWith("return:", StringComparison.Ordinal));

        var report = Ai291DArchitectureGuard.Validate(new Ai291DArchitectureGuardOptions
        {
            RepositoryRoot = RepoRoot(),
            SkipUiScan = true,
            AdditionalApplicationTypesToInspect = [typeof(AcademicProbeWithUiReturn)],
        });
        Assert.Equal(Ai291DArchitectureComplianceStatuses.Failed, report.Status);
    }

    [Fact]
    public void Domain_Application_Dependency_Is_Forbidden()
    {
        var violations = Ai291DArchitectureGuard.FindForbiddenDomainDependencies(
            ["Abhyanvaya.Application"],
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Abhyanvaya.Application\Abhyanvaya.Application.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("Abhyanvaya.Application", StringComparison.Ordinal));
        Assert.Equal(
            Ai291DArchitectureComplianceStatus.Failed,
            Ai291DArchitectureGuard.ResolveComplianceStatus(true, violations.Count));
    }

    [Fact]
    public void Platform_Allocation_Guard_Failure_Fails_Compliance()
    {
        var report = Ai291DArchitectureGuard.Validate(new Ai291DArchitectureGuardOptions
        {
            RepositoryRoot = RepoRoot(),
            SkipUiScan = true,
            PlatformBoundaryEvaluator = () => new AllocationArchitectureReport
            {
                Passed = false,
                Violations = ["engine must remain context-only (probe)"],
            },
        });

        Assert.Equal(Ai291DArchitectureComplianceStatuses.Failed, report.Status);
        Assert.False(report.PlatformBoundaryPassed);
        Assert.False(report.Passed);
        Assert.True(report.ViolationCount > 0);
        Assert.Contains(report.Violations, v => v.Contains("Platform allocation guard", StringComparison.Ordinal));
    }

    [Fact]
    public void Package_Json_Forbidden_Dependency_Fails()
    {
        var violations = Ai291DArchitectureGuard.ValidatePackageJsonText(
            """{ "dependencies": { "pg": "8.0.0", "react": "18.0.0" }, "devDependencies": {} }""");
        Assert.Contains(violations, v => v.Contains("'pg'", StringComparison.Ordinal));
        Assert.Equal(
            Ai291DArchitectureComplianceStatus.Failed,
            Ai291DArchitectureGuard.ResolveComplianceStatus(true, violations.Count));
    }

    [Fact]
    public void Zero_Violations_With_Ui_Scan_Is_Fully_Verified()
    {
        Assert.Equal(
            Ai291DArchitectureComplianceStatus.FullyVerified,
            Ai291DArchitectureGuard.ResolveComplianceStatus(uiScanExecuted: true, violationCount: 0));

        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        Assert.Equal(0, report.ViolationCount);
        Assert.True(report.UiScanExecuted);
        Assert.Equal(Ai291DArchitectureComplianceStatuses.FullyVerified, report.Status);
        Assert.True(report.FullyVerified);
        Assert.False(
            report.Status == Ai291DArchitectureComplianceStatuses.PartiallyVerified && report.FullyVerified);
    }

    [Fact]
    public void Hardening_Doc_And_Ci_Snapshot_Include_Status()
    {
        var doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "AI29_1D_PROMPT_21A_ARCHITECTURE_GUARD_HARDENING.md"));
        Assert.Contains("FULLY_VERIFIED", doc);
        Assert.Contains("PARTIALLY_VERIFIED", doc);
        Assert.Contains("FAILED", doc);
        Assert.Contains("Do not report PARTIALLY_VERIFIED as FULLY_VERIFIED", doc);

        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        Assert.Contains("\"Status\": \"FULLY_VERIFIED\"", json);
        Assert.Contains("\"FullyVerified\": true", json);
        Assert.Contains("\"UiScanExecuted\": true", json);
        Assert.Contains("\"BackendChecksPassed\": true", json);
        Assert.Contains("\"PlatformBoundaryPassed\": true", json);
        Assert.Contains("\"ViolationCount\": 0", json);

        // Snapshot write is owned by Prompt 21 tests; assert on-disk file when present (no concurrent write).
        var path = Path.Combine(RepoRoot(), "docs", "architecture", "AI29_1D_architecture_compliance.json");
        if (File.Exists(path))
        {
            var onDisk = File.ReadAllText(path);
            Assert.Contains("FULLY_VERIFIED", onDisk);
        }
    }

    // --- probe types (UnitTests only; not production Academic types) ---

    private sealed class AcademicProbeWithUiProperty
    {
        public ProbeUiType Widget { get; set; } = null!;
    }

    private sealed class AcademicProbeWithUiParameter
    {
        public void Apply(ProbeUiType widget) { _ = widget; }
    }

    private sealed class AcademicProbeWithUiReturn
    {
        public ProbeUiType Build() => new();
    }
}
