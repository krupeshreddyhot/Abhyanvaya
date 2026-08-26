using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A —
/// Adds Semester.IsHistoricalArchive (default false). Additive only; no GroupId nullability change.
/// Column is rollback-safe (drop column). Soft-delete is intentionally unused.
/// Table name must match EF mapping: <c>Semester</c> (singular), not Semesters.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260823190000_AI_SCHED_CATALOG_P1_4_Prompt3JA_SemesterHistoricalArchive")]
public partial class AI_SCHED_CATALOG_P1_4_Prompt3JA_SemesterHistoricalArchive : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: safe if column already present from a prior partial attempt.
        migrationBuilder.Sql(
            """
            ALTER TABLE "Semester"
            ADD COLUMN IF NOT EXISTS "IsHistoricalArchive" boolean NOT NULL DEFAULT FALSE;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_Semester_TenantId_IsHistoricalArchive"
            ON "Semester" ("TenantId", "IsHistoricalArchive");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "IX_Semester_TenantId_IsHistoricalArchive";
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Semester"
            DROP COLUMN IF EXISTS "IsHistoricalArchive";
            """);
    }
}
