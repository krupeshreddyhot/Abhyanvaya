-- Recovery only — mark AI29.1B.7 applied when SectionAllocationSnapshots already exists.

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260808180000_AI29_1B_7_AllocationPlatform'
    ) THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260808180000_AI29_1B_7_AllocationPlatform', '8.0.0');
    END IF;
  END IF;
END $$;
