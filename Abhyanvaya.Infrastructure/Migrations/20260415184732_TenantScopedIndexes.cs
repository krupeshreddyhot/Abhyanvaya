using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Student_StudentNumber",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentId_SubjectId_Date",
                table: "Attendance");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_TenantId_StudentId_SubjectId_Date",
                table: "Attendance",
                columns: new[] { "TenantId", "StudentId", "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_TenantId_StudentNumber",
                table: "Student",
                columns: new[] { "TenantId", "StudentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Name",
                table: "Subject",
                columns: new[] { "TenantId", "CourseId", "GroupId", "SemesterId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId_Username",
                table: "User",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 15, 18, 47, 31, 873, DateTimeKind.Utc).AddTicks(7186));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendance_TenantId_StudentId_SubjectId_Date",
                table: "Attendance");

            migrationBuilder.DropIndex(
                name: "IX_Student_TenantId_StudentNumber",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Name",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_User_TenantId_Username",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId_SubjectId_Date",
                table: "Attendance",
                columns: new[] { "StudentId", "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_StudentNumber",
                table: "Student",
                column: "StudentNumber",
                unique: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 5, 13, 4, 21, 26, DateTimeKind.Utc).AddTicks(4500));
        }
    }
}
