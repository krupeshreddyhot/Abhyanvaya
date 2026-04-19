using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSemester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SemesterId",
                table: "Student",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 4, 20, 17, 58, 799, DateTimeKind.Utc).AddTicks(8342));

            migrationBuilder.CreateIndex(
                name: "IX_Student_SemesterId",
                table: "Student",
                column: "SemesterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Student_Semester_SemesterId",
                table: "Student",
                column: "SemesterId",
                principalTable: "Semester",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Student_Semester_SemesterId",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Student_SemesterId",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "SemesterId",
                table: "Student");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 4, 19, 21, 3, 572, DateTimeKind.Utc).AddTicks(2639));
        }
    }
}
