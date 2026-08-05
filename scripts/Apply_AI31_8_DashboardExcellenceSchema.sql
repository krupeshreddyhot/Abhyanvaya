-- AI31.8 — additive columns for dashboard personalization excellence (idempotent).

ALTER TABLE "DashboardPreferences" ADD COLUMN IF NOT EXISTS "PinnedWidgetsJson" text NOT NULL DEFAULT '[]';
ALTER TABLE "DashboardPreferences" ADD COLUMN IF NOT EXISTS "FilterJson" text NOT NULL DEFAULT '{}';
ALTER TABLE "DashboardPreferences" ADD COLUMN IF NOT EXISTS "RefreshIntervalSeconds" integer NOT NULL DEFAULT 60;
ALTER TABLE "DashboardPreferences" ADD COLUMN IF NOT EXISTS "HighContrast" boolean NOT NULL DEFAULT FALSE;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805120000_AI31_8_DashboardExcellence') THEN
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('20260805120000_AI31_8_DashboardExcellence', '8.0.0');
    END IF;
  END IF;
END $$;
