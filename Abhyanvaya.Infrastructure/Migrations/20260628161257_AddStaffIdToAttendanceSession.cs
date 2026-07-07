using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffIdToAttendanceSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StaffId",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 16, 12, 56, 287, DateTimeKind.Utc).AddTicks(1025));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_StaffId",
                table: "AttendanceSession",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Staff_Date",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "StaffId", "AttendanceDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceSession_StaffMembers_StaffId",
                table: "AttendanceSession",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceSession_StaffMembers_StaffId",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_StaffId",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Staff_Date",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 47, 30, 174, DateTimeKind.Utc).AddTicks(5864));
        }
    }
}
