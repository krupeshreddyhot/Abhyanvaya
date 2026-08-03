using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI22.8 — additive workflow recovery columns + retry history. Does not replace AttendanceSession.Status.</summary>
public partial class AI22_8_AttendanceRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WorkflowStatus",
            table: "AttendanceSession",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastActivityUtc",
            table: "AttendanceSession",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ResumeCheckpointJson",
            table: "AttendanceSession",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "WorkflowExpiredUtc",
            table: "AttendanceSession",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecoveryPreferencesJson",
            table: "SchedulingWorkspacePreference",
            type: "text",
            nullable: false,
            defaultValue: """{"promptOnLogin":true,"dismissAutoResumeUntilUtc":null}""");

        migrationBuilder.CreateTable(
            name: "AttendanceRetryHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Success = table.Column<bool>(type: "boolean", nullable: false),
                ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                WorkflowStatusBefore = table.Column<int>(type: "integer", nullable: true),
                WorkflowStatusAfter = table.Column<int>(type: "integer", nullable: true),
                PerformedBy = table.Column<int>(type: "integer", nullable: false),
                PerformedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceRetryHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceRetryHistory_AttendanceSession_AttendanceSessionId",
                    column: x => x.AttendanceSessionId,
                    principalTable: "AttendanceSession",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSession_TenantId_WorkflowStatus_LastActivityUtc",
            table: "AttendanceSession",
            columns: new[] { "TenantId", "WorkflowStatus", "LastActivityUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSession_TenantId_StaffId_WorkflowExpiredUtc",
            table: "AttendanceSession",
            columns: new[] { "TenantId", "StaffId", "WorkflowExpiredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceRetryHistory_TenantId_AttendanceSessionId_PerformedUtc",
            table: "AttendanceRetryHistory",
            columns: new[] { "TenantId", "AttendanceSessionId", "PerformedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceRetryHistory");
        migrationBuilder.DropIndex(name: "IX_AttendanceSession_TenantId_WorkflowStatus_LastActivityUtc", table: "AttendanceSession");
        migrationBuilder.DropIndex(name: "IX_AttendanceSession_TenantId_StaffId_WorkflowExpiredUtc", table: "AttendanceSession");
        migrationBuilder.DropColumn(name: "WorkflowStatus", table: "AttendanceSession");
        migrationBuilder.DropColumn(name: "LastActivityUtc", table: "AttendanceSession");
        migrationBuilder.DropColumn(name: "ResumeCheckpointJson", table: "AttendanceSession");
        migrationBuilder.DropColumn(name: "WorkflowExpiredUtc", table: "AttendanceSession");
        migrationBuilder.DropColumn(name: "RecoveryPreferencesJson", table: "SchedulingWorkspacePreference");
    }
}
