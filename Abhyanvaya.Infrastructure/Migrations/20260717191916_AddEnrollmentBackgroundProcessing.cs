using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentBackgroundProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptUtc",
                table: "StudentEnrollmentItem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnrollmentDeadLetterEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExceptionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetryHistoryJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentDeadLetterEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentWorkLease",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcquiredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    PipelineState = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ReleasedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentWorkLease", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrollmentWorkLease_StudentEnrollmentItem_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StudentEnrollmentItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 19, 19, 13, 468, DateTimeKind.Utc).AddTicks(5034));

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_Status_NextAttempt",
                table: "StudentEnrollmentItem",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EnrollmentDeadLetterEntry_Item",
                table: "EnrollmentDeadLetterEntry",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentWorkLease_Active_Expires",
                table: "EnrollmentWorkLease",
                columns: new[] { "IsActive", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EnrollmentWorkLease_ActiveItem",
                table: "EnrollmentWorkLease",
                column: "ItemId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentDeadLetterEntry");

            migrationBuilder.DropTable(
                name: "EnrollmentWorkLease");

            migrationBuilder.DropIndex(
                name: "IX_StudentEnrollmentItem_Status_NextAttempt",
                table: "StudentEnrollmentItem");

            migrationBuilder.DropColumn(
                name: "NextAttemptUtc",
                table: "StudentEnrollmentItem");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 18, 9, 41, 160, DateTimeKind.Utc).AddTicks(9843));
        }
    }
}
