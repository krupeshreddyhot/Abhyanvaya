using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeToCourseGroupSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Name",
                table: "Subject");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Subject",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Group",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Course",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Course",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "BCOM");

            migrationBuilder.UpdateData(
                table: "Group",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "FIN");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 26, 12, 44, 37, 714, DateTimeKind.Utc).AddTicks(850));

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Code",
                table: "Subject",
                columns: new[] { "TenantId", "CourseId", "GroupId", "SemesterId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Group_TenantId_CourseId_Code",
                table: "Group",
                columns: new[] { "TenantId", "CourseId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Course_TenantId_Code",
                table: "Course",
                columns: new[] { "TenantId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Code",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_Group_TenantId_CourseId_Code",
                table: "Group");

            migrationBuilder.DropIndex(
                name: "IX_Course_TenantId_Code",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Group");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Course");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 19, 17, 3, 55, 449, DateTimeKind.Utc).AddTicks(7426));

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Name",
                table: "Subject",
                columns: new[] { "TenantId", "CourseId", "GroupId", "SemesterId", "Name" });
        }
    }
}
