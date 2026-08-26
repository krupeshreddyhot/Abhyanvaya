using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 TESTDATARESET Final —
/// Safety-gate guards (discovery only; no DB mutation).
/// </summary>
public sealed class AiSchedCatalogTimetableP14TestDataResetSafetyGateTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }

    private static string GateDoc()
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_SAFETY_GATE.md"));

    private static string ResetSql()
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql"));

    [Fact]
    public void Safety_Gate_Document_Exists_And_Blocks_Execution()
    {
        var doc = GateDoc();
        Assert.Contains("RESET BLOCKED — DO NOT EXECUTE", doc, StringComparison.Ordinal);
        Assert.Contains("TEST DATA BOUNDARY NOT DETERMINISTIC", doc, StringComparison.Ordinal);
        Assert.Contains("REQUIRES CHIEF ARCHITECT APPROVAL", doc, StringComparison.Ordinal);
        Assert.DoesNotContain("# RESET READY FOR CHIEF ARCHITECT APPROVAL", doc, StringComparison.Ordinal);
        Assert.Contains("DO NOT execute", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safety_Gate_Flags_Teaching_Group_And_TimetableSection_Deletion()
    {
        var doc = GateDoc();
        Assert.Contains("SchedulingTeachingGroup", doc, StringComparison.Ordinal);
        Assert.Contains("TimetableSections", doc, StringComparison.Ordinal);
        Assert.Contains("B2", doc, StringComparison.Ordinal);
        Assert.Contains("projector-owned", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reset_Sql_Has_No_Unscoped_Delete_And_Preserves_Masters()
    {
        var sql = ResetSql();
        var executable = string.Join('\n',
            sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));

        Assert.DoesNotContain("DELETE FROM \"Student\"", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM \"Semester\"", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE \"Student\"", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE \"Semester\"", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Semesters\"", executable, StringComparison.Ordinal);

        // Every DELETE FROM line must mention TenantId or Recognition join tenant scope.
        foreach (var line in executable.Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase))
                continue;
            Assert.True(
                t.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
                || t.Contains("AttendanceRecognitionReviewHistory", StringComparison.Ordinal),
                $"Unscoped or weakly scoped DELETE: {t}");
        }

        Assert.Contains("DELETE FROM \"SchedulingTeachingGroup\"", executable, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM \"TimetableSections\"", executable, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_Sql_Does_Not_Disable_Fk_Checks_Or_Change_Schema()
    {
        var executable = string.Join('\n',
            ResetSql().Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        Assert.DoesNotContain("FOREIGN_KEY_CHECKS", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET session_replication_role", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DISABLE TRIGGER", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE TABLE", executable, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frozen_Architecture_Surfaces_Remain_Untouched_By_Safety_Gate()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "Abhyanvaya.Application", "Scheduling",
            "SubjectAllocationCourseDepartmentRules.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Abhyanvaya.Application", "Scheduling",
            "TimetableEntryCourseDepartmentRules.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Abhyanvaya.Application", "Academic",
            "OperationalSemesterRules.cs")));
        var ops = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic",
            "OperationalSemesterRules.cs"));
        Assert.Contains("GroupId != null", ops, StringComparison.Ordinal);
        Assert.Contains("!s.IsHistoricalArchive", ops, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Production_Wipe_Service_Registered_For_This_Gate()
    {
        var di = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "DependencyInjection.cs"));
        Assert.DoesNotContain("ITestDataResetService", di, StringComparison.Ordinal);
        Assert.DoesNotContain("TestDataResetExecutionService", di, StringComparison.Ordinal);
    }

    [Fact]
    public void Domain_Has_No_Deterministic_TestData_Discriminator()
    {
        // Governance: do not invent a discriminator. Absence proves B1 (boundary not deterministic).
        var root = FindRepoRoot();
        var common = Path.Combine(root, "Abhyanvaya.Domain", "Common");
        var entities = Path.Combine(root, "Abhyanvaya.Domain", "Entities");
        Assert.True(Directory.Exists(common));
        Assert.True(Directory.Exists(entities));

        foreach (var file in Directory.EnumerateFiles(common, "*.cs")
                     .Concat(Directory.EnumerateFiles(entities, "*.cs", SearchOption.AllDirectories)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("IsTestData", text, StringComparison.Ordinal);
            Assert.DoesNotContain("TestDataBatchId", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IsDisposableTestRow", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Reset_Sql_Delete_Count_Matches_Allowlist_Inventory()
    {
        var executable = string.Join('\n',
            ResetSql().Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        var deletes = executable.Split('\n')
            .Select(l => l.Trim())
            .Count(t => t.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase));
        // 58 DELETE statements in AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql (allowlist).
        Assert.Equal(58, deletes);
    }

    [Fact]
    public void Safety_Gate_Documents_Statement_Classifications()
    {
        var doc = GateDoc();
        Assert.Contains("SAFE WITH CONDITION", doc, StringComparison.Ordinal);
        Assert.Contains("UNSAFE without Architect approval", doc, StringComparison.Ordinal);
        Assert.Contains("Expected row counts:", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Not established", doc, StringComparison.OrdinalIgnoreCase);
    }
}
