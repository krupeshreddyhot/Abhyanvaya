using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAttendanceSessionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remap legacy AI-only status values to the expanded workflow enum.
            migrationBuilder.Sql("""
                UPDATE "AttendanceSession"
                SET "Status" = CASE "Status"
                    WHEN 0 THEN 1
                    WHEN 1 THEN 2
                    WHEN 2 THEN 4
                    WHEN 3 THEN 5
                    ELSE "Status"
                END
                WHERE "Status" IN (0, 1, 2, 3);
                """);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 34, 20, 969, DateTimeKind.Utc).AddTicks(606));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 23, 16, 371, DateTimeKind.Utc).AddTicks(7643));

            migrationBuilder.Sql("""
                UPDATE "AttendanceSession"
                SET "Status" = CASE "Status"
                    WHEN 1 THEN 0
                    WHEN 2 THEN 1
                    WHEN 3 THEN 2
                    WHEN 4 THEN 2
                    WHEN 5 THEN 3
                    WHEN 6 THEN 0
                    ELSE "Status"
                END
                WHERE "Status" IN (1, 2, 3, 4, 5, 6);
                """);
        }
    }
}
