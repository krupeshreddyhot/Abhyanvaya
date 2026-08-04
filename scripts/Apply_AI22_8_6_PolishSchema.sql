-- AI22.8.6 — idempotent schema for enterprise operations polish (bulk history).
-- Prefer this on live DB when EF Designer/snapshot does not include the migration.

CREATE TABLE IF NOT EXISTS "AttendanceBulkOperationHistory" (
  "Id" uuid NOT NULL,
  "TenantId" integer NOT NULL,
  "Operation" character varying(64) NOT NULL,
  "RequestedCount" integer NOT NULL,
  "SucceededCount" integer NOT NULL,
  "FailedCount" integer NOT NULL,
  "SkippedCount" integer NOT NULL,
  "SessionIdsJson" text NULL,
  "ResultJson" text NULL,
  "Reason" character varying(2000) NULL,
  "PerformedBy" integer NOT NULL,
  "StartedUtc" timestamp with time zone NOT NULL,
  "CompletedUtc" timestamp with time zone NULL,
  CONSTRAINT "PK_AttendanceBulkOperationHistory" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AttendanceBulkOperationHistory_TenantId_StartedUtc"
  ON "AttendanceBulkOperationHistory" ("TenantId", "StartedUtc");

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260804090000_AI22_8_6_AttendanceBulkOperationHistory') THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260804090000_AI22_8_6_AttendanceBulkOperationHistory', '8.0.0');
    END IF;
  END IF;
END $$;
