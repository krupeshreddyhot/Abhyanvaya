using System.Text.Json;
using Abhyanvaya.Application.Academic.Architecture;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D Prompt 21 — architecture guard &amp; compliance report.</summary>
/// <remarks>
/// Serialized: snapshot write shares <c>docs/architecture/AI29_1D_architecture_compliance.json</c>
/// with other guard tests; parallel writers caused intermittent 28-pass/1-fail runs under --no-build.
/// </remarks>
[Collection("AI29.1D.ArchitectureGuard")]
public sealed class AI29_1D_Prompt21_ArchitectureGuardTests
{
    private static string RepoRoot()
    {
        var root = Ai291DArchitectureGuard.TryResolveRepositoryRoot(AppContext.BaseDirectory);
        Assert.False(string.IsNullOrWhiteSpace(root), "Repository root with abhyanvaya-ui must be resolvable for Prompt 21.");
        return root!;
    }

    [Fact]
    public void Architecture_Compliance_Report_Passes()
    {
        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Equal(Ai291DArchitectureComplianceStatuses.FullyVerified, report.Status);
        Assert.True(report.FullyVerified);
        Assert.Equal("UI → API / Application Contracts → Domain Services", report.RequiredLayering);
        Assert.NotNull(report.UiScan);
        Assert.True(report.UiScan!.Executed);
        Assert.True(report.UiScanExecuted);
        Assert.True(report.UiScan.FilesScanned > 0);
        Assert.Equal(0, report.UiScan.ForbiddenDataAccessHits);
        Assert.Equal(0, report.UiScan.ForbiddenAuthorityHits);
        Assert.Equal(0, report.ViolationCount);
    }

    [Fact]
    public void Backend_Services_Remain_Authoritative()
    {
        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        Assert.True(report.BackendAuthority.AllocationEnginePresent);
        Assert.True(report.BackendAuthority.AttendanceSessionResolverPresent);
        Assert.True(report.BackendAuthority.AllocationLifecycleServicePresent);
        Assert.True(report.BackendAuthority.AllocationGovernanceServicePresent);
        Assert.True(report.BackendAuthority.SectionCapacityEnginePresent);
        Assert.True(report.BackendAuthority.ExistingPlatformGuardPassed);
    }

    [Fact]
    public void Ui_Package_Json_Has_No_Direct_Database_Drivers()
    {
        var packagePath = Path.Combine(RepoRoot(), "abhyanvaya-ui", "package.json");
        Assert.True(File.Exists(packagePath));
        using var doc = JsonDocument.Parse(File.ReadAllText(packagePath));
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in new[] { "dependencies", "devDependencies" })
        {
            if (!doc.RootElement.TryGetProperty(section, out var obj)) continue;
            foreach (var p in obj.EnumerateObject()) deps.Add(p.Name);
        }

        foreach (var banned in new[] { "@prisma/client", "typeorm", "sequelize", "knex", "pg", "mssql", "mysql2" })
            Assert.DoesNotContain(banned, deps);
    }

    [Fact]
    public void Compliance_Markdown_Documents_Layering_Contract()
    {
        var doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "AI29_1D_PROMPT_21_ARCHITECTURE_GUARD.md"));
        Assert.Contains("UI → API / Application Contracts → Domain Services", doc);
        Assert.Contains("DbContext", doc);
        Assert.Contains("authoritative capacity", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AttendanceSessionResolver", doc);
        Assert.Contains("architecture/ai29-1d-report", doc);
    }

    [Fact]
    public void Writes_Machine_Readable_Compliance_Snapshot()
    {
        var report = Ai291DArchitectureGuard.Validate(RepoRoot());
        var outDir = Path.Combine(RepoRoot(), "docs", "architecture");
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "AI29_1D_architecture_compliance.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.WriteAllText(path, json);
                break;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }

        Assert.True(File.Exists(path));
        Assert.Contains("\"Passed\": true", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Status\": \"FULLY_VERIFIED\"", json);
        Assert.Contains("\"FullyVerified\": true", json);
    }
}
