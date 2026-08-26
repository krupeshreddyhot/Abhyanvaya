using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 3 — align SubjectAllocation.DepartmentId to Course.DepartmentId.
/// Deterministic repair only: never invent Departments; never change Course.DepartmentId.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822120000_AI_SCHED_CATALOG_P1_3_SubjectAllocationDepartmentAlign")]
public partial class AI_SCHED_CATALOG_P1_3_SubjectAllocationDepartmentAlign : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "SchedulingSubjectAllocation" AS sa
            SET "DepartmentId" = c."DepartmentId"
            FROM "Course" AS c
            WHERE sa."CourseId" = c."Id"
              AND sa."TenantId" = c."TenantId"
              AND sa."DepartmentId" <> c."DepartmentId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data alignment — previous mismatched DepartmentIds are not reconstructed.
    }
}
