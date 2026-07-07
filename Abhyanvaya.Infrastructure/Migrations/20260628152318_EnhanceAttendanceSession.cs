using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAttendanceSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "AttendanceSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceMethod",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ImageHeight",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageWidth",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "AttendanceSession",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodNumber",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "AttendanceSession",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMilliseconds",
                table: "AttendanceSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecognitionModel",
                table: "AttendanceSession",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 15, 23, 16, 371, DateTimeKind.Utc).AddTicks(7643));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_ApprovedBy",
                table: "AttendanceSession",
                column: "ApprovedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceSession_User_ApprovedBy",
                table: "AttendanceSession",
                column: "ApprovedBy",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceSession_User_ApprovedBy",
                table: "AttendanceSession");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_ApprovedBy",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "AttendanceMethod",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageHeight",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageWidth",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "PeriodNumber",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ProcessingMilliseconds",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "RecognitionModel",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 14, 26, 44, 937, DateTimeKind.Utc).AddTicks(9552));
        }
    }
}
