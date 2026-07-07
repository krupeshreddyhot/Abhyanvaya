using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceDetail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceId = table.Column<int>(type: "integer", nullable: false),
                    AttendanceRecognitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaptureMethod = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric", nullable: true),
                    TeacherOverride = table.Column<bool>(type: "boolean", nullable: false),
                    FaceNumber = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDetail_AttendanceRecognition_AttendanceRecognitio~",
                        column: x => x.AttendanceRecognitionId,
                        principalTable: "AttendanceRecognition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDetail_Attendance_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 54, 13, 886, DateTimeKind.Utc).AddTicks(3615));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDetail_AttendanceId",
                table: "AttendanceDetail",
                column: "AttendanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDetail_AttendanceRecognitionId",
                table: "AttendanceDetail",
                column: "AttendanceRecognitionId",
                unique: true,
                filter: "\"AttendanceRecognitionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDetail_Tenant_Attendance",
                table: "AttendanceDetail",
                columns: new[] { "TenantId", "AttendanceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceDetail");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 19, 10, 91, DateTimeKind.Utc).AddTicks(8223));
        }
    }
}
