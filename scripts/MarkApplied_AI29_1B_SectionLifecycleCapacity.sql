-- Recovery only — use when AI29.1B schema was already applied outside EF
-- (e.g. an old Apply script). Do NOT run `dotnet ef database update` for this
-- migration on those DBs; this only inserts the history row.
--
-- Normal path: Development MigrateAsync / `dotnet ef database update`
--   → Abhyanvaya.Infrastructure.Persistence.Migrations.AI29_1B_SectionLifecycleCapacity

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260807180000_AI29_1B_SectionLifecycleCapacity'
    ) THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260807180000_AI29_1B_SectionLifecycleCapacity', '8.0.0');
    END IF;
  END IF;
END $$;
