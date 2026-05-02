using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantSubjectLookupAndAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantSubject",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubject", x => x.Id);
                });

            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_Code",
                table: "Subject");

            migrationBuilder.AddColumn<decimal>(
                name: "Credits",
                table: "Subject",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamHours",
                table: "Subject",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HPW",
                table: "Subject",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Marks",
                table: "Subject",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantSubjectId",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "TenantSubject" ("Code", "Name", "TenantId", "CreatedDate", "CreatedBy", "UpdatedDate", "UpdatedBy", "IsDeleted")
                SELECT DISTINCT
                    NULLIF(TRIM(s."Code"), ''),
                    s."Name",
                    s."TenantId",
                    COALESCE(s."CreatedDate", NOW()),
                    s."CreatedBy",
                    s."UpdatedDate",
                    s."UpdatedBy",
                    FALSE
                FROM "Subject" s
                WHERE s."Name" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Subject" s
                SET "TenantSubjectId" = ts."Id"
                FROM "TenantSubject" ts
                WHERE ts."TenantId" = s."TenantId"
                  AND ts."Name" = s."Name"
                  AND COALESCE(ts."Code", '') = COALESCE(NULLIF(TRIM(s."Code"), ''), '');
                """);

            migrationBuilder.Sql("""
                UPDATE "Subject"
                SET "TenantSubjectId" = (
                    SELECT ts."Id"
                    FROM "TenantSubject" ts
                    WHERE ts."TenantId" = "Subject"."TenantId"
                    ORDER BY ts."Id"
                    LIMIT 1
                )
                WHERE "TenantSubjectId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TenantSubjectId",
                table: "Subject",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Subject");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 26, 16, 4, 53, 424, DateTimeKind.Utc).AddTicks(6276));

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_TenantSubjectId",
                table: "Subject",
                columns: new[] { "TenantId", "CourseId", "GroupId", "SemesterId", "TenantSubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TenantSubjectId",
                table: "Subject",
                column: "TenantSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubject_TenantId_Name",
                table: "TenantSubject",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_Subject_TenantSubject_TenantSubjectId",
                table: "Subject",
                column: "TenantSubjectId",
                principalTable: "TenantSubject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subject_TenantSubject_TenantSubjectId",
                table: "Subject");

            migrationBuilder.DropTable(
                name: "TenantSubject");

            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantId_CourseId_GroupId_SemesterId_TenantSubjectId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_Subject_TenantSubjectId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "Credits",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "ExamHours",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "HPW",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "Marks",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "TenantSubjectId",
                table: "Subject");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Subject",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Subject",
                type: "text",
                nullable: false,
                defaultValue: "");

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
        }
    }
}
