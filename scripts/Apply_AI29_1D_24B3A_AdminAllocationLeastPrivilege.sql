-- AI29.1D.24B.3A — Least-privilege ADMIN Allocation role links (idempotent).
-- 1) Ensure Allocation.* permission catalog rows exist (ids match StaffHubSeed).
-- 2) Ensure ADMIN receives operator set only: Run, Scenario.View/Create/Compare/Replay/Review.
-- 3) Remove Prompt-3 blanket governance/ops grants from ADMIN if present:
--      Approve, Operations.View, Reject, Export, Scenario.Archive
-- Does NOT grant Allocation.* to FACULTY.
-- Does NOT use tenant bypass beyond Code='ADMIN' role filter.

DO $$
DECLARE
  op_keys text[] := ARRAY[
    'Allocation.Run',
    'Allocation.Scenario.View',
    'Allocation.Scenario.Create',
    'Allocation.Scenario.Compare',
    'Allocation.Scenario.Replay',
    'Allocation.Scenario.Review'
  ];
  gov_keys text[] := ARRAY[
    'Allocation.Approve',
    'Allocation.Operations.View',
    'Allocation.Reject',
    'Allocation.Export',
    'Allocation.Scenario.Archive'
  ];
BEGIN
  -- Catalog: insert missing Allocation permissions (stable ids when free).
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Run') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (227, 'Allocation.Run', 'Allocation', 'Run')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Approve') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (228, 'Allocation.Approve', 'Allocation', 'Approve')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Operations.View') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (229, 'Allocation.Operations.View', 'Allocation', 'OperationsView')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.View') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (230, 'Allocation.Scenario.View', 'Allocation', 'ScenarioView')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.Create') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (231, 'Allocation.Scenario.Create', 'Allocation', 'ScenarioCreate')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.Compare') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (232, 'Allocation.Scenario.Compare', 'Allocation', 'ScenarioCompare')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.Replay') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (233, 'Allocation.Scenario.Replay', 'Allocation', 'ScenarioReplay')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.Review') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (234, 'Allocation.Scenario.Review', 'Allocation', 'ScenarioReview')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Reject') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (235, 'Allocation.Reject', 'Allocation', 'Reject')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Export') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (236, 'Allocation.Export', 'Allocation', 'Export')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Allocation.Scenario.Archive') THEN
    INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
      (237, 'Allocation.Scenario.Archive', 'Allocation', 'ScenarioArchive')
    ON CONFLICT ("Id") DO NOTHING;
  END IF;

  -- Ensure operator links on every non-deleted ADMIN role.
  INSERT INTO "ApplicationRolePermission" ("ApplicationRoleId", "PermissionId")
  SELECT r."Id", p."Id"
  FROM "ApplicationRole" r
  CROSS JOIN "Permission" p
  WHERE UPPER(r."Code") = 'ADMIN'
    AND COALESCE(r."IsDeleted", FALSE) = FALSE
    AND p."Key" = ANY (op_keys)
    AND NOT EXISTS (
      SELECT 1 FROM "ApplicationRolePermission" arp
      WHERE arp."ApplicationRoleId" = r."Id" AND arp."PermissionId" = p."Id"
    );

  -- Remove governance/ops over-grants from ADMIN (Prompt 3 blanket / old seed range).
  DELETE FROM "ApplicationRolePermission" arp
  USING "ApplicationRole" r, "Permission" p
  WHERE arp."ApplicationRoleId" = r."Id"
    AND arp."PermissionId" = p."Id"
    AND UPPER(r."Code") = 'ADMIN'
    AND COALESCE(r."IsDeleted", FALSE) = FALSE
    AND p."Key" = ANY (gov_keys);

  RAISE NOTICE 'AI29.1D.24B.3A: ADMIN Allocation least-privilege links reconciled.';
EXCEPTION
  WHEN undefined_table THEN
    RAISE NOTICE 'Permission tables not ready; skip AI29.1D.24B.3A RBAC hardening.';
END $$;
