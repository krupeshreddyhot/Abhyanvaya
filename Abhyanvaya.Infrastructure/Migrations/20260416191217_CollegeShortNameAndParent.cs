using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    public partial class CollegeShortNameAndParent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "College",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentCollegeId",
                table: "College",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_College_College_ParentCollegeId",
                table: "College",
                column: "ParentCollegeId",
                principalTable: "College",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 16, 19, 12, 14, 516, DateTimeKind.Utc).AddTicks(4431));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_College_College_ParentCollegeId",
                table: "College");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "College");

            migrationBuilder.DropColumn(
                name: "ParentCollegeId",
                table: "College");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 16, 5, 14, 4, 378, DateTimeKind.Utc).AddTicks(6212));
        }
    }
}
