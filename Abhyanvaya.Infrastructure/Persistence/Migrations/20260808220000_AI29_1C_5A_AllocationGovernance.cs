using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI29.1C.5A — concurrency token, version Operation, Status/Lifecycle normalization, Archive permission.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808220000_AI29_1C_5A_AllocationGovernance")]
public partial class AI29_1C_5A_AllocationGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "AllocationEngineScenarios",
            type: "bytea",
            nullable: false,
            defaultValue: new byte[0]);

        migrationBuilder.Sql("""
            UPDATE "AllocationEngineScenarios"
            SET "RowVersion" = decode(replace(gen_random_uuid()::text, '-', ''), 'hex')
            WHERE "RowVersion" IS NULL OR octet_length("RowVersion") = 0;
            """);

        migrationBuilder.AddColumn<string>(
            name: "Operation",
            table: "AllocationScenarioVersions",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        // Compatibility: if Status was mirrored as a lifecycle value, restore execution Status
        // while preserving LifecycleStatus as the governance authority.
        migrationBuilder.Sql("""
            UPDATE "AllocationEngineScenarios"
            SET "Status" = CASE
                WHEN "Status" IN ('Approved','Rejected','Archived','Reviewed','Compared','Saved','Simulated','SimulationAccepted','Draft')
                    THEN 'Completed'
                ELSE "Status"
            END
            WHERE "LifecycleStatus" IS NOT NULL
              AND "Status" IN ('Approved','Rejected','Archived','Reviewed','Compared','Saved','Simulated','SimulationAccepted','Draft');
            """);

        migrationBuilder.Sql("""
            UPDATE "AllocationScenarioVersions"
            SET "Operation" = CASE
                WHEN "Reason" ILIKE '%approv%' THEN 'Approve'
                WHEN "Reason" ILIKE '%reject%' THEN 'Reject'
                WHEN "Reason" ILIKE '%archiv%' THEN 'Archive'
                WHEN "Reason" ILIKE '%review%' THEN 'Review'
                WHEN "Reason" ILIKE '%compar%' THEN 'Compare'
                WHEN "Reason" ILIKE '%replay%' THEN 'Replay'
                WHEN "Reason" ILIKE '%simulat%' THEN 'Simulate'
                WHEN "Reason" ILIKE '%save%' THEN 'Save'
                WHEN "VersionNumber" = 1 THEN 'CreateScenario'
                ELSE 'Save'
            END
            WHERE "Operation" IS NULL OR "Operation" = '';
            """);

        // Table names are singular (Permission / ApplicationRolePermission) — use SQL, not InsertData.
        migrationBuilder.Sql("""
            INSERT INTO "Permission" ("Id", "Key", "Resource", "Action")
            SELECT 237, 'Allocation.Scenario.Archive', 'Allocation', 'ScenarioArchive'
            WHERE NOT EXISTS (
                SELECT 1 FROM "Permission" WHERE "Id" = 237 OR "Key" = 'Allocation.Scenario.Archive'
            );

            INSERT INTO "ApplicationRolePermission" ("ApplicationRoleId", "PermissionId")
            SELECT 100, 237
            WHERE NOT EXISTS (
                SELECT 1 FROM "ApplicationRolePermission"
                WHERE "ApplicationRoleId" = 100 AND "PermissionId" = 237
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "ApplicationRolePermission" WHERE "PermissionId" = 237;
            DELETE FROM "Permission" WHERE "Id" = 237;
            """);

        migrationBuilder.DropColumn(name: "Operation", table: "AllocationScenarioVersions");
        migrationBuilder.DropColumn(name: "RowVersion", table: "AllocationEngineScenarios");
    }
}
