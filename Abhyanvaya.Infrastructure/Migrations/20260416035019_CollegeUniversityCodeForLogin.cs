using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollegeUniversityCodeForLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UniversityCode",
                table: "College",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"College\" SET \"UniversityCode\" = 'DEFAULT_UNIV' WHERE \"UniversityCode\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_College_UniversityCode_Code",
                table: "College",
                columns: new[] { "UniversityCode", "Code" },
                unique: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 16, 3, 50, 16, 725, DateTimeKind.Utc).AddTicks(2674));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_College_UniversityCode_Code",
                table: "College");

            migrationBuilder.DropColumn(
                name: "UniversityCode",
                table: "College");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 15, 18, 47, 31, 873, DateTimeKind.Utc).AddTicks(7186));
        }
    }
}
