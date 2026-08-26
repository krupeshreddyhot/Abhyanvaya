using Abhyanvaya.IntegrationTests.Fixtures;
using Npgsql;

namespace Abhyanvaya.IntegrationTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.6 Final Gate Prompt 21 — Read-only PostgreSQL integrity audit (non-destructive).
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
public sealed class AiSchedTg6FinalGatePrompt21PostgreSqlIntegrityAuditTests
{
    private readonly PostgreSqlFixture _fixture;

    public AiSchedTg6FinalGatePrompt21PostgreSqlIntegrityAuditTests(PostgreSqlFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task Read_only_integrity_checks_return_zero_violations()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        // Cross-tenant TeachingGroupId FK mismatch (active rows)
        var crossTenantTg = await ScalarLongAsync(conn, """
            SELECT COUNT(*)::bigint
            FROM "SchedulingTimetableEntry" e
            JOIN "SchedulingTeachingGroup" tg ON tg."Id" = e."TeachingGroupId"
            WHERE e."IsDeleted" = FALSE
              AND tg."IsDeleted" = FALSE
              AND e."TenantId" <> tg."TenantId"
            """);
        Assert.Equal(0, crossTenantTg);

        // Orphan TeachingGroupId (broken FK reference among non-deleted)
        var orphanTg = await ScalarLongAsync(conn, """
            SELECT COUNT(*)::bigint
            FROM "SchedulingTimetableEntry" e
            WHERE e."IsDeleted" = FALSE
              AND e."TeachingGroupId" IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM "SchedulingTeachingGroup" tg
                  WHERE tg."Id" = e."TeachingGroupId" AND tg."IsDeleted" = FALSE)
            """);
        Assert.Equal(0, orphanTg);

        // Duplicate TeachingGroupSection (TG, Section) among active
        var dupTgSection = await ScalarLongAsync(conn, """
            SELECT COUNT(*)::bigint FROM (
              SELECT "TeachingGroupId", "SectionId"
              FROM "SchedulingTeachingGroupSection"
              WHERE "IsDeleted" = FALSE
              GROUP BY "TeachingGroupId", "SectionId"
              HAVING COUNT(*) > 1
            ) d
            """);
        Assert.Equal(0, dupTgSection);

        // Duplicate active TimetableSection projection (TimetableId, EntryId, SectionId)
        var dupProjection = await ScalarLongAsync(conn, """
            SELECT COUNT(*)::bigint FROM (
              SELECT "TimetableId", "TimetableEntryId", "SectionId"
              FROM "TimetableSections"
              WHERE "IsDeleted" = FALSE
              GROUP BY "TimetableId", "TimetableEntryId", "SectionId"
              HAVING COUNT(*) > 1
            ) d
            """);
        Assert.Equal(0, dupProjection);

        // TeachingGroupSection tenant must match TeachingGroup tenant
        var tgSectionTenant = await ScalarLongAsync(conn, """
            SELECT COUNT(*)::bigint
            FROM "SchedulingTeachingGroupSection" tgs
            JOIN "SchedulingTeachingGroup" tg ON tg."Id" = tgs."TeachingGroupId"
            WHERE tgs."IsDeleted" = FALSE
              AND tg."IsDeleted" = FALSE
              AND tgs."TenantId" <> tg."TenantId"
            """);
        Assert.Equal(0, tgSectionTenant);
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
