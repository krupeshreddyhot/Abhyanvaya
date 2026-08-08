-- Recovery only — mark AI29.1A.7 applied when AcademicArchitectureTrends already exists.

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260807120000_AI29_1A7_Observability'
    ) THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260807120000_AI29_1A7_Observability', '8.0.0');
    END IF;
  END IF;
END $$;
