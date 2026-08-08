-- Recovery only: mark AI29.1C allocation engine migration applied when schema already exists.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260808200000_AI29_1C_AllocationEngine', '8.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808200000_AI29_1C_AllocationEngine'
);
