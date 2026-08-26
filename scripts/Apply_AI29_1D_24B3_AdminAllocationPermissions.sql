-- AI29.1D.24B.3 Prompt 3 — Idempotent ADMIN ApplicationRole → Allocation.* permission links.
-- Does NOT weaken tenant isolation. Does NOT grant Allocation.Run to FACULTY.
-- Safe to re-run.

DO $$
BEGIN
  -- Ensure Allocation permission catalog rows exist (ids match StaffHubSeed).
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Run') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (227, 'Allocation.Run', 'Allocation', 'Run'),
      (228, 'Allocation.Approve', 'Allocation', 'Approve'),
      (229, 'Allocation.Operations.View', 'Allocation', 'OperationsView'),
      (230, 'Allocation.Scenario.View', 'Allocation', 'ScenarioView'),
      (231, 'Allocation.Scenario.Create', 'Allocation', 'ScenarioCreate'),
      (232, 'Allocation.Scenario.Compare', 'Allocation', 'ScenarioCompare'),
      (233, 'Allocation.Scenario.Replay', 'Allocation', 'ScenarioReplay'),
      (234, 'Allocation.Scenario.Review', 'Allocation', 'ScenarioReview'),
      (235, 'Allocation.Reject', 'Allocation', 'Reject'),
      (236, 'Allocation.Export', 'Allocation', 'Export'),
      (237, 'Allocation.Scenario.Archive', 'Allocation', 'ScenarioArchive')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;

  -- Link Allocation permission ids to every non-deleted ADMIN application role (all tenants).
  INSERT INTO "ApplicationRolePermission" ("ApplicationRoleId", "PermissionId")
  SELECT r."Id", p."Id"
  FROM "ApplicationRole" r
  CROSS JOIN "Permission" p
  WHERE UPPER(r."Code") = 'ADMIN'
    AND COALESCE(r."IsDeleted", FALSE) = FALSE
    AND p."Key" IN (
      'Allocation.Run',
      'Allocation.Approve',
      'Allocation.Operations.View',
      'Allocation.Scenario.View',
      'Allocation.Scenario.Create',
      'Allocation.Scenario.Compare',
      'Allocation.Scenario.Replay',
      'Allocation.Scenario.Review',
      'Allocation.Reject',
      'Allocation.Export',
      'Allocation.Scenario.Archive'
    )
    AND NOT EXISTS (
      SELECT 1
      FROM "ApplicationRolePermission" arp
      WHERE arp."ApplicationRoleId" = r."Id"
        AND arp."PermissionId" = p."Id"
    );

  RAISE NOTICE 'AI29.1D.24B.3: ADMIN Allocation permission links reconciled.';
EXCEPTION
  WHEN undefined_table THEN
    RAISE NOTICE 'Permission/ApplicationRole tables not ready; skip AI29.1D.24B.3 allocation RBAC repair.';
END $$;
