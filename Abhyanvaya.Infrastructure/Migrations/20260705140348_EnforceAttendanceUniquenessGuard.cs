using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <summary>
    /// AI13.DOMAIN.1 — Defense-in-depth guard for the attendance uniqueness invariant:
    /// "at most one Attendance row per (TenantId, StudentId, SubjectId, Date)".
    /// <para>
    /// The unique index <c>IX_Attendance_TenantId_StudentId_SubjectId_Date</c> was originally created by the
    /// <c>TenantScopedIndexes</c> migration (2026-04-15) and already matches the current EF model — this migration
    /// intentionally does NOT re-create or alter that index under normal circumstances. Instead it:
    /// 1) Pre-flight checks live data for any violating duplicate groups and ABORTS the migration (no data is ever
    ///    deleted) if any are found, printing the offending (TenantId, StudentId, SubjectId, Date) tuples.
    /// 2) Re-asserts the unique index idempotently (CREATE UNIQUE INDEX IF NOT EXISTS) so this migration is a
    ///    self-sufficient safety net even if run against an environment where the original index migration was
    ///    somehow skipped (e.g. a restored/older database dump), without depending on migration history ordering.
    /// </para>
    /// <para>
    /// See docs/AI13_DOMAIN1_DATABASE_UNIQUENESS.md for the full architecture review, verification SQL, and
    /// rollback plan.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class EnforceAttendanceUniquenessGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Abort (raise + roll back) if any duplicate (TenantId, StudentId, SubjectId, Date) groups exist.
            //    Never deletes data — the operator must resolve duplicates first and re-run the migration.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    dup_count integer;
    dup_report text;
BEGIN
    SELECT count(*) INTO dup_count
    FROM (
        SELECT ""TenantId"", ""StudentId"", ""SubjectId"", ""Date""
        FROM ""Attendance""
        GROUP BY ""TenantId"", ""StudentId"", ""SubjectId"", ""Date""
        HAVING count(*) > 1
    ) d;

    IF dup_count > 0 THEN
        SELECT string_agg(
                 format('(Tenant=%s, Student=%s, Subject=%s, Date=%s, Rows=%s)',
                        ""TenantId"", ""StudentId"", ""SubjectId"", ""Date"", cnt),
                 '; ')
        INTO dup_report
        FROM (
            SELECT ""TenantId"", ""StudentId"", ""SubjectId"", ""Date"", count(*) AS cnt
            FROM ""Attendance""
            GROUP BY ""TenantId"", ""StudentId"", ""SubjectId"", ""Date""
            HAVING count(*) > 1
            ORDER BY count(*) DESC
            LIMIT 20
        ) top_dups;

        RAISE EXCEPTION
            'AI13.DOMAIN.1: Attendance uniqueness guard aborted migration. Found % duplicate (TenantId, StudentId, SubjectId, Date) group(s). First offenders: %. Resolve duplicates (see docs/AI13_DOMAIN1_DATABASE_UNIQUENESS.md verification SQL) before re-running this migration. No data was modified.',
            dup_count, dup_report;
    END IF;
END $$;
");

            // 2) Idempotent safety net: ensure the unique index exists exactly as declared in AttendanceConfiguration.
            //    No-ops if it already exists (expected in production — created by TenantScopedIndexes on 2026-04-15).
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Attendance_TenantId_StudentId_SubjectId_Date""
    ON ""Attendance"" (""TenantId"", ""StudentId"", ""SubjectId"", ""Date"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: this migration does not own the index's lifecycle (it predates this
            // migration and other code paths rely on it). Rolling back this migration must not remove the
            // uniqueness safeguard from the database.
        }
    }
}
