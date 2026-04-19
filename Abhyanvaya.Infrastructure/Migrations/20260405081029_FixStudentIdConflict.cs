using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStudentIdConflict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendance_Student_StudentNumber",
                table: "Attendance");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Student_StudentNumber",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentNumber",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Attendance");

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "Attendance",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Attendance",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 5, 8, 10, 27, 137, DateTimeKind.Utc).AddTicks(3628));

            migrationBuilder.CreateIndex(
                name: "IX_Student_StudentNumber",
                table: "Student",
                column: "StudentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId",
                table: "Attendance",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendance_Student_StudentId",
                table: "Attendance",
                column: "StudentId",
                principalTable: "Student",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendance_Student_StudentId",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Student_StudentNumber",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentId",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Attendance");

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                table: "Attendance",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Attendance",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Student_StudentNumber",
                table: "Student",
                column: "StudentNumber");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 4, 20, 17, 58, 799, DateTimeKind.Utc).AddTicks(8342));

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentNumber",
                table: "Attendance",
                column: "StudentNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendance_Student_StudentNumber",
                table: "Attendance",
                column: "StudentNumber",
                principalTable: "Student",
                principalColumn: "StudentNumber",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
