using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentFaceEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentFaceEmbedding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingVector = table.Column<float[]>(type: "real[]", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingQuality = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    PhotoKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GeneratedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    GeneratedBy = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFaceEmbedding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFaceEmbedding_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 16, 42, 8, 561, DateTimeKind.Utc).AddTicks(4799));

            migrationBuilder.CreateIndex(
                name: "IX_StudentFaceEmbedding_Student_Active",
                table: "StudentFaceEmbedding",
                columns: new[] { "StudentId", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFaceEmbedding_Tenant_Student",
                table: "StudentFaceEmbedding",
                columns: new[] { "TenantId", "StudentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentFaceEmbedding");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 16, 3, 52, 733, DateTimeKind.Utc).AddTicks(4814));
        }
    }
}
