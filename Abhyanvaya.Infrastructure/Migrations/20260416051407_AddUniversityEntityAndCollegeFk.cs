using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    public partial class AddUniversityEntityAndCollegeFk : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "University",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_University", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_University_Code",
                table: "University",
                column: "Code",
                unique: true);

            migrationBuilder.Sql("INSERT INTO \"University\" (\"Code\", \"Name\", \"CreatedDate\") SELECT DISTINCT UPPER(\"UniversityCode\"), UPPER(\"UniversityCode\"), NOW() FROM \"College\" WHERE \"UniversityCode\" IS NOT NULL AND \"UniversityCode\" <> ''; ");

            migrationBuilder.AddColumn<int>(
                name: "UniversityId",
                table: "College",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"College\" c SET \"UniversityId\" = u.\"Id\" FROM \"University\" u WHERE UPPER(c.\"UniversityCode\") = u.\"Code\";");

            migrationBuilder.Sql("INSERT INTO \"University\" (\"Code\", \"Name\", \"CreatedDate\") SELECT 'DEFAULT_UNIV', 'DEFAULT_UNIV', NOW() WHERE NOT EXISTS (SELECT 1 FROM \"University\" WHERE \"Code\" = 'DEFAULT_UNIV');");

            migrationBuilder.Sql("UPDATE \"College\" SET \"UniversityId\" = (SELECT \"Id\" FROM \"University\" WHERE \"Code\" = 'DEFAULT_UNIV') WHERE \"UniversityId\" IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_College_UniversityCode_Code",
                table: "College");

            migrationBuilder.AlterColumn<int>(
                name: "UniversityId",
                table: "College",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UniversityCode",
                table: "College");

            migrationBuilder.CreateIndex(
                name: "IX_College_UniversityId_Code",
                table: "College",
                columns: new[] { "UniversityId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_College_University_UniversityId",
                table: "College",
                column: "UniversityId",
                principalTable: "University",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 16, 5, 14, 4, 378, DateTimeKind.Utc).AddTicks(6212));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_College_University_UniversityId",
                table: "College");

            migrationBuilder.DropIndex(
                name: "IX_College_UniversityId_Code",
                table: "College");

            migrationBuilder.AddColumn<string>(
                name: "UniversityCode",
                table: "College",
                type: "text",
                nullable: false,
                defaultValue: "DEFAULT_UNIV");

            migrationBuilder.Sql("UPDATE \"College\" c SET \"UniversityCode\" = u.\"Code\" FROM \"University\" u WHERE c.\"UniversityId\" = u.\"Id\";");

            migrationBuilder.DropColumn(
                name: "UniversityId",
                table: "College");

            migrationBuilder.CreateIndex(
                name: "IX_College_UniversityCode_Code",
                table: "College",
                columns: new[] { "UniversityCode", "Code" },
                unique: true);

            migrationBuilder.DropTable(
                name: "University");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 16, 3, 50, 16, 725, DateTimeKind.Utc).AddTicks(2674));
        }
    }
}
