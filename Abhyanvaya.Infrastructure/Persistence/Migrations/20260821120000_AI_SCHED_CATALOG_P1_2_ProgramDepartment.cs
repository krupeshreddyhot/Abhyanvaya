using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-2 — Programs.DepartmentId (Program → Department ownership).
/// Additive column; deterministic backfill only when exactly one Department exists for the same TenantId+CollegeId.
/// Aborts if any Program row cannot be mapped (no invented/default Department).
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821120000_AI_SCHED_CATALOG_P1_2_ProgramDepartment")]
public partial class AI_SCHED_CATALOG_P1_2_ProgramDepartment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DepartmentId",
            table: "Programs",
            type: "integer",
            nullable: true);

        // Deterministic only: exactly one non-deleted Department for the same TenantId + CollegeId.
        migrationBuilder.Sql("""
            UPDATE "Programs" AS p
            SET "DepartmentId" = sole."Id"
            FROM (
                SELECT d."TenantId", d."CollegeId", MIN(d."Id") AS "Id"
                FROM "Department" AS d
                WHERE d."IsDeleted" = FALSE
                GROUP BY d."TenantId", d."CollegeId"
                HAVING COUNT(*) = 1
            ) AS sole
            WHERE p."TenantId" = sole."TenantId"
              AND p."CollegeId" = sole."CollegeId"
              AND p."DepartmentId" IS NULL;
            """);

        migrationBuilder.Sql("""
            DO $$
            DECLARE unmapped_count integer;
            BEGIN
              SELECT COUNT(*) INTO unmapped_count
              FROM "Programs"
              WHERE "DepartmentId" IS NULL;

              IF unmapped_count > 0 THEN
                RAISE EXCEPTION
                  'P1-2 STOP: % Program row(s) lack a deterministic Department mapping (exactly one Department per TenantId+CollegeId). Manual mapping required; no default Department assigned.',
                  unmapped_count;
              END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "DepartmentId",
            table: "Programs",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Programs_TenantId_DepartmentId",
            table: "Programs",
            columns: new[] { "TenantId", "DepartmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_Programs_DepartmentId",
            table: "Programs",
            column: "DepartmentId");

        migrationBuilder.AddForeignKey(
            name: "FK_Programs_Department_DepartmentId",
            table: "Programs",
            column: "DepartmentId",
            principalTable: "Department",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Programs_Department_DepartmentId",
            table: "Programs");

        migrationBuilder.DropIndex(
            name: "IX_Programs_TenantId_DepartmentId",
            table: "Programs");

        migrationBuilder.DropIndex(
            name: "IX_Programs_DepartmentId",
            table: "Programs");

        migrationBuilder.DropColumn(
            name: "DepartmentId",
            table: "Programs");
    }
}
