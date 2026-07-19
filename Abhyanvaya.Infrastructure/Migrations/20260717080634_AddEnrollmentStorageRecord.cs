using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentStorageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentStorageRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CollegeId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYear = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ImageWidth = table.Column<int>(type: "integer", nullable: true),
                    ImageHeight = table.Column<int>(type: "integer", nullable: true),
                    ArtifactVersion = table.Column<int>(type: "integer", nullable: false),
                    StorageVersion = table.Column<int>(type: "integer", nullable: false),
                    PipelineVersion = table.Column<int>(type: "integer", nullable: false),
                    ValidationVersion = table.Column<int>(type: "integer", nullable: false),
                    ValidationProfile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentStorageRecord", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 8, 6, 28, 716, DateTimeKind.Utc).AddTicks(1496));

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentStorageRecord_StorageGroupId",
                table: "EnrollmentStorageRecord",
                column: "StorageGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentStorageRecord_Tenant_Student_Type_Checksum",
                table: "EnrollmentStorageRecord",
                columns: new[] { "TenantId", "StudentId", "ArtifactType", "Checksum" });

            migrationBuilder.CreateIndex(
                name: "UX_EnrollmentStorageRecord_Tenant_Student_Type_Version",
                table: "EnrollmentStorageRecord",
                columns: new[] { "TenantId", "StudentId", "ArtifactType", "ArtifactVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentStorageRecord");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 16, 6, 7, 53, 303, DateTimeKind.Utc).AddTicks(4676));
        }
    }
}
