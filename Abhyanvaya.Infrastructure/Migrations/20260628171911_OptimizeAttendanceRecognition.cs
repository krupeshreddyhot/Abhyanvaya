using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeAttendanceRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecognition_AttendanceSession_AttendanceSessionId",
                table: "AttendanceRecognition");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AttendanceRecognition",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AttendanceRecognition",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "AttendanceRecognition" ar
                SET "TenantId" = s."TenantId"
                FROM "AttendanceSession" s
                WHERE ar."AttendanceSessionId" = s."Id";
                """);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 19, 10, 91, DateTimeKind.Utc).AddTicks(8223));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Tenant_Session",
                table: "AttendanceRecognition",
                columns: new[] { "TenantId", "AttendanceSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Tenant_Session_Status",
                table: "AttendanceRecognition",
                columns: new[] { "TenantId", "AttendanceSessionId", "RecognitionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Tenant_Status",
                table: "AttendanceRecognition",
                columns: new[] { "TenantId", "RecognitionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Tenant_Student",
                table: "AttendanceRecognition",
                columns: new[] { "TenantId", "StudentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecognition_AttendanceSession_AttendanceSessionId",
                table: "AttendanceRecognition",
                column: "AttendanceSessionId",
                principalTable: "AttendanceSession",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecognition_AttendanceSession_AttendanceSessionId",
                table: "AttendanceRecognition");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Tenant_Session",
                table: "AttendanceRecognition");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Tenant_Session_Status",
                table: "AttendanceRecognition");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Tenant_Status",
                table: "AttendanceRecognition");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Tenant_Student",
                table: "AttendanceRecognition");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AttendanceRecognition");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AttendanceRecognition");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 14, 39, 169, DateTimeKind.Utc).AddTicks(9791));

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecognition_AttendanceSession_AttendanceSessionId",
                table: "AttendanceRecognition",
                column: "AttendanceSessionId",
                principalTable: "AttendanceSession",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
