using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 TESTDATARESET —
/// Architecture guards for discovery/scripts only (no DB mutation in this prompt).
/// </summary>
public sealed class AiSchedCatalogTimetableP14TestDataResetGuardTests
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

    private static string ReadScript(string fileName)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", fileName));

    [Fact]
    public void Deliverables_Exist()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_DISCOVERY.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_MATRIX.md")));
        Assert.True(File.Exists(Path.Combine(root, "scripts",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql")));
        Assert.True(File.Exists(Path.Combine(root, "scripts",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql")));
        Assert.True(File.Exists(Path.Combine(root, "scripts",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql")));
    }

    [Fact]
    public void Reset_Script_Never_Deletes_Or_Updates_Protected_Masters()
    {
        var sql = ReadScript("AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql");

        Assert.Contains("BEGIN;", sql, StringComparison.Ordinal);
        Assert.Contains("COMMIT;", sql, StringComparison.Ordinal);
        Assert.Contains(":tenant_id", sql, StringComparison.Ordinal);
        Assert.Contains("FAIL CLOSED", sql, StringComparison.Ordinal);

        // Strip SQL line comments before ban-list checks (headers may mention banned phrases).
        var executable = string.Join('\n',
            sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));

        Assert.DoesNotContain("FOREIGN_KEY_CHECKS", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET session_replication_role", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DISABLE TRIGGER", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE TABLE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER SEQUENCE", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RESTART IDENTITY", executable, StringComparison.OrdinalIgnoreCase);

        foreach (var table in new[]
                 {
                     "\"Student\"", "\"Semester\"", "\"Group\"", "\"Course\"", "\"Subject\"",
                     "\"Department\"", "\"Programs\"", "\"College\"", "\"User\"", "\"Permission\"",
                 })
        {
            Assert.DoesNotContain($"DELETE FROM {table}", executable, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"UPDATE {table}", executable, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("UPDATE \"Student\"", executable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Student.SemesterId", executable, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_Script_Uses_Singular_Semester_Table_And_Known_Scheduling_Names()
    {
        var sql = ReadScript("AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql");
        Assert.Contains("\"Semester\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Semesters\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"SchedulingTeachingGroup\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"SchedulingSubjectAllocation\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"SchedulingTimetableEntry\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"TimetableSections\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"StudentSections\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Sections\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_Script_Excludes_Review_Classified_Tables()
    {
        var sql = ReadScript("AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql");
        Assert.DoesNotContain("DELETE FROM \"SchedulingConflictWorkspacePin\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"SchedulingConflictWorkspaceBookmark\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"SchedulingConflictWorkspaceNote\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"SchedulingConflictRuleThresholdSetting\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"SchedulingOptimizationTelemetryAggregate\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"SectionPolicies\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"TenantSectionCapacityPolicies\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"AttendanceRecoveryPreference\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"StudentFaceEmbedding\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"AuditEntry\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_Is_ReadOnly_And_Tenant_Scoped()
    {
        var sql = ReadScript("AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql");
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":tenant_id", sql, StringComparison.Ordinal);
        Assert.Contains("MUTATION: NONE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Script_Is_ReadOnly()
    {
        var sql = ReadScript("AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql");
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VERIFY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_Documents_No_Execution_And_No_Student_Remap()
    {
        var root = FindRepoRoot();
        var discovery = File.ReadAllText(Path.Combine(root, "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_DISCOVERY.md"));
        Assert.Contains("NONE", discovery, StringComparison.Ordinal);
        Assert.Contains("Do **not** reconcile Student.SemesterId", discovery, StringComparison.Ordinal);
        Assert.Contains("\"Semester\"", discovery, StringComparison.Ordinal);
        Assert.DoesNotContain("SET FOREIGN_KEY_CHECKS", discovery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_Runtime_Services_Not_Mutated_By_This_Prompt()
    {
        // This prompt is scripts/docs/guards only — ensure no new production wipe endpoint was required.
        var root = FindRepoRoot();
        var di = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "DependencyInjection.cs"));
        Assert.DoesNotContain("ITestDataResetService", di, StringComparison.Ordinal);
        Assert.DoesNotContain("TestDataResetService", di, StringComparison.Ordinal);
    }
}
