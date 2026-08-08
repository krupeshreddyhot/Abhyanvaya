-- Recovery only — mark AI29.1B.5 applied when tables already exist outside EF.

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260808120000_AI29_1B_5_SectionOperationsHardening'
    ) THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260808120000_AI29_1B_5_SectionOperationsHardening', '8.0.0');
    END IF;
  END IF;
END $$;
