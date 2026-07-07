using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeAttendanceSessionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 47, 30, 174, DateTimeKind.Utc).AddTicks(5864));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Date_Period",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "AttendanceDate", "PeriodNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Date_Status",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "AttendanceDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Method",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "AttendanceMethod" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Subject_Date_Period",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "SubjectId", "AttendanceDate", "PeriodNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Date_Period",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Date_Status",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Method",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Subject_Date_Period",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 37, 59, 544, DateTimeKind.Utc).AddTicks(5191));
        }
    }
}
