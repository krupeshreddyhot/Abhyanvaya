using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations;

/// <summary>
/// Applies enrollment schema changes that were present in orphaned manual migrations
/// (20260716143000 / 20260716150000) but never registered in the EF migration chain.
/// </summary>
public partial class FixMissingEnrollmentSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "StudentEnrollmentBatch"
                ADD COLUMN IF NOT EXISTS "ConfigurationSnapshotJson" jsonb NOT NULL DEFAULT '{}';

            ALTER TABLE "StudentEnrollmentBatch"
                ADD COLUMN IF NOT EXISTS "CorrelationId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

            ALTER TABLE "StudentEnrollmentBatch"
                ADD COLUMN IF NOT EXISTS "PhotoProviderName" character varying(64) NOT NULL DEFAULT 'ExamBranch';

            ALTER TABLE "StudentEnrollmentBatch"
                ADD COLUMN IF NOT EXISTS "PipelineVersion" integer NOT NULL DEFAULT 1;

            ALTER TABLE "StudentEnrollmentBatch"
                ADD COLUMN IF NOT EXISTS "Priority" integer NOT NULL DEFAULT 0;
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "StudentEnrollmentProgressSnapshot"
            (
                "Id" uuid NOT NULL,
                "TenantId" integer NOT NULL,
                "BatchId" uuid NOT NULL,
                "CapturedUtc" timestamp with time zone NOT NULL,
                "SnapshotJson" jsonb NOT NULL,
                CONSTRAINT "PK_StudentEnrollmentProgressSnapshot" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_StudentEnrollmentProgressSnapshot_StudentEnrollmentBatch_Bat~"
                    FOREIGN KEY ("BatchId")
                    REFERENCES "StudentEnrollmentBatch" ("Id")
                    ON DELETE CASCADE
            );
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_StudentEnrollmentProgressSnapshot_Batch_CapturedUtc"
                ON "StudentEnrollmentProgressSnapshot" ("BatchId", "CapturedUtc");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS \"StudentEnrollmentProgressSnapshot\";");

        migrationBuilder.Sql(
            """
            ALTER TABLE "StudentEnrollmentBatch" DROP COLUMN IF EXISTS "Priority";
            ALTER TABLE "StudentEnrollmentBatch" DROP COLUMN IF EXISTS "PipelineVersion";
            ALTER TABLE "StudentEnrollmentBatch" DROP COLUMN IF EXISTS "PhotoProviderName";
            ALTER TABLE "StudentEnrollmentBatch" DROP COLUMN IF EXISTS "CorrelationId";
            ALTER TABLE "StudentEnrollmentBatch" DROP COLUMN IF EXISTS "ConfigurationSnapshotJson";
            """);
    }
}
