using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSessionIdToAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceSessionId",
                table: "Attendance",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 37, 59, 544, DateTimeKind.Utc).AddTicks(5191));

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_AttendanceSessionId",
                table: "Attendance",
                column: "AttendanceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Tenant_AttendanceSession",
                table: "Attendance",
                columns: new[] { "TenantId", "AttendanceSessionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Attendance_AttendanceSession_AttendanceSessionId",
                table: "Attendance",
                column: "AttendanceSessionId",
                principalTable: "AttendanceSession",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendance_AttendanceSession_AttendanceSessionId",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_AttendanceSessionId",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_Tenant_AttendanceSession",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "AttendanceSessionId",
                table: "Attendance");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 34, 20, 969, DateTimeKind.Utc).AddTicks(606));
        }
    }
}
