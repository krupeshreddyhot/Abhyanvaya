using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 2 — Course.DepartmentId (Option A catalog ownership).
/// Backfill: Program.DepartmentId when Course.ProgramId set; else exactly-one Department per TenantId
/// (Course has no CollegeId — sole non-deleted Department in the tenant is the only deterministic null-Program map).
/// Aborts if any Course remains unmapped.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822100000_AI_SCHED_CATALOG_P1_3_CourseDepartment")]
public partial class AI_SCHED_CATALOG_P1_3_CourseDepartment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DepartmentId",
            table: "Course",
            type: "integer",
            nullable: true);

        // Preferred: Courses with ProgramId ← Program.DepartmentId (same tenant).
        migrationBuilder.Sql("""
            UPDATE "Course" AS c
            SET "DepartmentId" = p."DepartmentId"
            FROM "Programs" AS p
            WHERE c."ProgramId" IS NOT NULL
              AND p."Id" = c."ProgramId"
              AND p."TenantId" = c."TenantId"
              AND c."DepartmentId" IS NULL;
            """);

        // Null ProgramId: only when exactly one non-deleted Department exists for the Course tenant.
        migrationBuilder.Sql("""
            UPDATE "Course" AS c
            SET "DepartmentId" = sole."Id"
            FROM (
                SELECT d."TenantId", MIN(d."Id") AS "Id"
                FROM "Department" AS d
                WHERE d."IsDeleted" = FALSE
                GROUP BY d."TenantId"
                HAVING COUNT(*) = 1
            ) AS sole
            WHERE c."ProgramId" IS NULL
              AND c."TenantId" = sole."TenantId"
              AND c."DepartmentId" IS NULL;
            """);

        migrationBuilder.Sql("""
            DO $$
            DECLARE unmapped_count integer;
            BEGIN
              SELECT COUNT(*) INTO unmapped_count
              FROM "Course"
              WHERE "DepartmentId" IS NULL;

              IF unmapped_count > 0 THEN
                RAISE EXCEPTION
                  'P1-3 STOP: % Course row(s) lack a deterministic Department mapping (Program.DepartmentId or exactly one Department per tenant). Manual mapping required; no default Department assigned.',
                  unmapped_count;
              END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "DepartmentId",
            table: "Course",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Course_TenantId_DepartmentId",
            table: "Course",
            columns: new[] { "TenantId", "DepartmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_Course_DepartmentId",
            table: "Course",
            column: "DepartmentId");

        migrationBuilder.AddForeignKey(
            name: "FK_Course_Department_DepartmentId",
            table: "Course",
            column: "DepartmentId",
            principalTable: "Department",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Course_Department_DepartmentId",
            table: "Course");

        migrationBuilder.DropIndex(
            name: "IX_Course_TenantId_DepartmentId",
            table: "Course");

        migrationBuilder.DropIndex(
            name: "IX_Course_DepartmentId",
            table: "Course");

        migrationBuilder.DropColumn(
            name: "DepartmentId",
            table: "Course");
    }
}
