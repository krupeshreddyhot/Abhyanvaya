INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260808210000_AI29_1C_5_AllocationOperations', '8.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808210000_AI29_1C_5_AllocationOperations'
);
