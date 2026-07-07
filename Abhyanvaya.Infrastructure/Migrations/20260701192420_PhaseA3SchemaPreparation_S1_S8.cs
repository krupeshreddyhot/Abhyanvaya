using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhaseA3SchemaPreparation_S1_S8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Session_FaceNumber",
                table: "AttendanceRecognition");

            migrationBuilder.Sql(
                "UPDATE \"AttendanceSession\" SET \"SessionNumber\" = 1 WHERE \"SessionNumber\" IS NULL;");

            migrationBuilder.AlterColumn<short>(
                name: "SessionNumber",
                table: "AttendanceSession",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaptureDevice",
                table: "AttendanceSession",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CaptureTimestamp",
                table: "AttendanceSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "ImageOrientation",
                table: "AttendanceSession",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalImageHash",
                table: "AttendanceSession",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecognitionPipelineVersion",
                table: "AttendanceSession",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceImageKey",
                table: "AttendanceRecognition",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "ImageSequence",
                table: "AttendanceRecognition",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 19, 24, 18, 972, DateTimeKind.Utc).AddTicks(3400));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Session_ImageSequence",
                table: "AttendanceRecognition",
                columns: new[] { "AttendanceSessionId", "ImageSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Session_ImageSequence_FaceNumber",
                table: "AttendanceRecognition",
                columns: new[] { "AttendanceSessionId", "ImageSequence", "FaceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Session_ImageSequence",
                table: "AttendanceRecognition");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognition_Session_ImageSequence_FaceNumber",
                table: "AttendanceRecognition");

            migrationBuilder.DropColumn(
                name: "CaptureDevice",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "CaptureTimestamp",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ImageOrientation",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "OriginalImageHash",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "RecognitionPipelineVersion",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "FaceImageKey",
                table: "AttendanceRecognition");

            migrationBuilder.DropColumn(
                name: "ImageSequence",
                table: "AttendanceRecognition");

            migrationBuilder.AlterColumn<short>(
                name: "SessionNumber",
                table: "AttendanceSession",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)1);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 18, 53, 33, 476, DateTimeKind.Utc).AddTicks(7699));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Session_FaceNumber",
                table: "AttendanceRecognition",
                columns: new[] { "AttendanceSessionId", "FaceNumber" },
                unique: true);
        }
    }
}
