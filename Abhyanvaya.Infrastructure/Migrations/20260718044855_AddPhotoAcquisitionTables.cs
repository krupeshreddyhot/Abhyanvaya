using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAcquisitionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPhotoAcquisitionBatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcademicYear = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    SucceededCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    RetryQueuedCount = table.Column<int>(type: "integer", nullable: false),
                    ReadyForEnrollmentCount = table.Column<int>(type: "integer", nullable: false),
                    ManifestJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPhotoAcquisitionBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentPhotoAcquisitionItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CollegeCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SourceReference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PhotoByteSize = table.Column<int>(type: "integer", nullable: true),
                    PhotoBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    ValidationReportJson = table.Column<string>(type: "jsonb", nullable: true),
                    QualityReportJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPhotoAcquisitionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPhotoAcquisitionItem_StudentPhotoAcquisitionBatch_Ba~",
                        column: x => x.BatchId,
                        principalTable: "StudentPhotoAcquisitionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 18, 4, 48, 53, 498, DateTimeKind.Utc).AddTicks(5227));

            migrationBuilder.CreateIndex(
                name: "IX_StudentPhotoAcquisitionItem_BatchId_StudentId",
                table: "StudentPhotoAcquisitionItem",
                columns: new[] { "BatchId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPhotoAcquisitionItem_ContentHash",
                table: "StudentPhotoAcquisitionItem",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPhotoAcquisitionItem");

            migrationBuilder.DropTable(
                name: "StudentPhotoAcquisitionBatch");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 20, 33, 50, 584, DateTimeKind.Utc).AddTicks(5110));
        }
    }
}
