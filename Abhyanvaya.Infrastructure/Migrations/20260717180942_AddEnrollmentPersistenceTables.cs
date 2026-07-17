using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentPersistenceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentEmbeddingVersionSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentFaceEmbeddingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingModelVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PipelineVersion = table.Column<int>(type: "integer", nullable: false),
                    ValidationVersion = table.Column<int>(type: "integer", nullable: false),
                    StorageVersion = table.Column<int>(type: "integer", nullable: false),
                    ManifestVersion = table.Column<int>(type: "integer", nullable: false),
                    ArtifactVersion = table.Column<int>(type: "integer", nullable: false),
                    FrameworkVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OnnxVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentEmbeddingVersionSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrollmentEmbeddingVersionSnapshot_StudentEnrollmentItem_En~",
                        column: x => x.EnrollmentItemId,
                        principalTable: "StudentEnrollmentItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnrollmentEmbeddingVersionSnapshot_StudentFaceEmbedding_Stu~",
                        column: x => x.StudentFaceEmbeddingId,
                        principalTable: "StudentFaceEmbedding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentPersistenceAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingId = table.Column<Guid>(type: "uuid", nullable: true),
                    PipelineVersion = table.Column<int>(type: "integer", nullable: false),
                    StorageVersion = table.Column<int>(type: "integer", nullable: false),
                    ValidationVersion = table.Column<int>(type: "integer", nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentPersistenceAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrollmentPersistenceAudit_StudentEnrollmentItem_Enrollment~",
                        column: x => x.EnrollmentItemId,
                        principalTable: "StudentEnrollmentItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 18, 9, 41, 160, DateTimeKind.Utc).AddTicks(9843));

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentEmbeddingVersionSnapshot_EnrollmentItemId",
                table: "EnrollmentEmbeddingVersionSnapshot",
                column: "EnrollmentItemId");

            migrationBuilder.CreateIndex(
                name: "UX_EnrollmentEmbeddingVersionSnapshot_Embedding",
                table: "EnrollmentEmbeddingVersionSnapshot",
                column: "StudentFaceEmbeddingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPersistenceAudit_Item_Timestamp",
                table: "EnrollmentPersistenceAudit",
                columns: new[] { "EnrollmentItemId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentEmbeddingVersionSnapshot");

            migrationBuilder.DropTable(
                name: "EnrollmentPersistenceAudit");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 8, 6, 28, 716, DateTimeKind.Utc).AddTicks(1496));
        }
    }
}
