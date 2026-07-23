using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI227A_ClassroomPhotoCaptureMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageAcquisitionMethod",
                table: "AttendanceSession",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ImageBlurScore",
                table: "AttendanceSession",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ImageCaptureLatitude",
                table: "AttendanceSession",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ImageCaptureLongitude",
                table: "AttendanceSession",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 23, 16, 4, 54, 796, DateTimeKind.Utc).AddTicks(6965));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageAcquisitionMethod",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageBlurScore",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageCaptureLatitude",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageCaptureLongitude",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 18, 12, 54, 21, 462, DateTimeKind.Utc).AddTicks(8410));
        }
    }
}
