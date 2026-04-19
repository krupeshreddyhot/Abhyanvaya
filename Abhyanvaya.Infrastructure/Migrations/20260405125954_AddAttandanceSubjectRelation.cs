using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttandanceSubjectRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentId",
                table: "Attendance");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 5, 12, 59, 52, 72, DateTimeKind.Utc).AddTicks(4616));

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId_SubjectId_Date",
                table: "Attendance",
                columns: new[] { "StudentId", "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_SubjectId",
                table: "Attendance",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendance_Subject_SubjectId",
                table: "Attendance",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendance_Subject_SubjectId",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentId_SubjectId_Date",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_SubjectId",
                table: "Attendance");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 5, 8, 10, 27, 137, DateTimeKind.Utc).AddTicks(3628));

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId",
                table: "Attendance",
                column: "StudentId");
        }
    }
}
