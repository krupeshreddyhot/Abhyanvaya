using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomImageUploadColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ImageFileSize",
                table: "AttendanceSession",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImageUploadedUtc",
                table: "AttendanceSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 3, 6, 25, 35, 940, DateTimeKind.Utc).AddTicks(9540));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageFileSize",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageUploadedUtc",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 17, 55, 45, 598, DateTimeKind.Utc).AddTicks(1582));
        }
    }
}
