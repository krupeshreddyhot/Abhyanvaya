-- Mark the consolidated EF migration as applied on databases that already ran:
--   Apply_AI29_SectionSchema.sql
--   Apply_AI29_1A_ProgramSchema.sql
--   Apply_AI29_1A5_EnterpriseHardening.sql
--   Apply_AI29_1A6_PerformanceGuard.sql
--
-- Do NOT run `dotnet ef database update` for this migration on those DBs —
-- the objects already exist. This only inserts the history row.

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260806180000_AI29_AcademicHierarchyBaseline'
    ) THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260806180000_AI29_AcademicHierarchyBaseline', '8.0.0');
    END IF;
  END IF;
END $$;
