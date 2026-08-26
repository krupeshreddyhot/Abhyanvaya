using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-TG.4 Prompt 2 — Add nullable TimetableEntry.TeachingGroupId FK.
/// Additive column + Restrict FK + indexes only.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818110000_AI_SCHED_TG_4_TimetableEntryTeachingGroupId")]
public partial class AI_SCHED_TG_4_TimetableEntryTeachingGroupId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TeachingGroupId",
            table: "SchedulingTimetableEntry",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTimetableEntry_TenantId_TeachingGroupId",
            table: "SchedulingTimetableEntry",
            columns: new[] { "TenantId", "TeachingGroupId" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTimetableEntry_TeachingGroupId",
            table: "SchedulingTimetableEntry",
            column: "TeachingGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_SchedulingTimetableEntry_SchedulingTeachingGroup_TeachingGroupId",
            table: "SchedulingTimetableEntry",
            column: "TeachingGroupId",
            principalTable: "SchedulingTeachingGroup",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SchedulingTimetableEntry_SchedulingTeachingGroup_TeachingGroupId",
            table: "SchedulingTimetableEntry");

        migrationBuilder.DropIndex(
            name: "IX_SchedulingTimetableEntry_TenantId_TeachingGroupId",
            table: "SchedulingTimetableEntry");

        migrationBuilder.DropIndex(
            name: "IX_SchedulingTimetableEntry_TeachingGroupId",
            table: "SchedulingTimetableEntry");

        migrationBuilder.DropColumn(
            name: "TeachingGroupId",
            table: "SchedulingTimetableEntry");
    }
}
