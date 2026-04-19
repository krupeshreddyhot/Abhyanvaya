using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubjectLanguageStudentFirstLang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LanguageSubjectSlot",
                table: "Subject",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeachingLanguageId",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstLanguageId",
                table: "Student",
                type: "integer",
                nullable: true);

            // Ensure each tenant has "English" in the shared Language catalog, then backfill FirstLanguageId.
            migrationBuilder.Sql(
                """
                INSERT INTO "Language" ("TenantId", "Name", "CreatedDate", "IsDeleted")
                SELECT DISTINCT c."TenantId", 'English', NOW() AT TIME ZONE 'utc', false
                FROM "College" c
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Language" l
                    WHERE l."TenantId" = c."TenantId"
                      AND l."Name" = 'English'
                      AND NOT l."IsDeleted");

                INSERT INTO "Language" ("TenantId", "Name", "CreatedDate", "IsDeleted")
                SELECT DISTINCT s."TenantId", 'English', NOW() AT TIME ZONE 'utc', false
                FROM "Student" s
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Language" l
                    WHERE l."TenantId" = s."TenantId"
                      AND l."Name" = 'English'
                      AND NOT l."IsDeleted");

                UPDATE "Student" st SET "FirstLanguageId" = l."Id"
                FROM "Language" l
                WHERE l."TenantId" = st."TenantId"
                  AND l."Name" = 'English'
                  AND NOT l."IsDeleted"
                  AND st."FirstLanguageId" IS NULL;

                UPDATE "Student" st SET "FirstLanguageId" = (
                    SELECT l."Id" FROM "Language" l
                    WHERE l."TenantId" = st."TenantId" AND NOT l."IsDeleted"
                    ORDER BY l."Id" LIMIT 1
                )
                WHERE st."FirstLanguageId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "FirstLanguageId",
                table: "Student",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subject_TeachingLanguageId",
                table: "Subject",
                column: "TeachingLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Student_FirstLanguageId",
                table: "Student",
                column: "FirstLanguageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Student_Language_FirstLanguageId",
                table: "Student",
                column: "FirstLanguageId",
                principalTable: "Language",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subject_Language_TeachingLanguageId",
                table: "Subject",
                column: "TeachingLanguageId",
                principalTable: "Language",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Student_Language_FirstLanguageId",
                table: "Student");

            migrationBuilder.DropForeignKey(
                name: "FK_Subject_Language_TeachingLanguageId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_Subject_TeachingLanguageId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_Student_FirstLanguageId",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "LanguageSubjectSlot",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "TeachingLanguageId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "FirstLanguageId",
                table: "Student");
        }
    }
}
