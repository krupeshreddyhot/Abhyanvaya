using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAttendanceSessionBusinessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageConfidence",
                table: "AttendanceSession",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuplicateCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IgnoredCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LowConfidenceCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManualAssignmentCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RecognitionCompletionPercent",
                table: "AttendanceSession",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecognizedCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RejectedCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SessionName",
                table: "AttendanceSession",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "SessionNumber",
                table: "AttendanceSession",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnknownCount",
                table: "AttendanceSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 15, 59, 3, 576, DateTimeKind.Utc).AddTicks(5182));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Context_SessionNumber",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "CourseId", "GroupId", "SemesterId", "SubjectId", "AttendanceDate", "PeriodNumber", "SessionNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_Tenant_Context_SessionNumber",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "AverageConfidence",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "DuplicateCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "IgnoredCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "LowConfidenceCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ManualAssignmentCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "RecognitionCompletionPercent",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "RecognizedCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "RejectedCount",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "SessionName",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "SessionNumber",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "UnknownCount",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 54, 13, 886, DateTimeKind.Utc).AddTicks(3615));
        }
    }
}
