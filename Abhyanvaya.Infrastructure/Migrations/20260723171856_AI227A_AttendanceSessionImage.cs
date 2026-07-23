using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI227A_AttendanceSessionImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceSessionImage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageSequence = table.Column<short>(type: "smallint", nullable: false),
                    ImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    Orientation = table.Column<short>(type: "smallint", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    UploadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaptureTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaptureDevice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcquisitionMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CaptureLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CaptureLongitude = table.Column<double>(type: "double precision", nullable: true),
                    BlurScore = table.Column<double>(type: "double precision", nullable: true),
                    ThumbnailImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnotatedImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProcessingError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessionImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSessionImage_AttendanceSession_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalTable: "AttendanceSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 23, 17, 18, 54, 849, DateTimeKind.Utc).AddTicks(3023));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessionImage_Session_Sequence",
                table: "AttendanceSessionImage",
                columns: new[] { "AttendanceSessionId", "ImageSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessionImage_Tenant_Session",
                table: "AttendanceSessionImage",
                columns: new[] { "TenantId", "AttendanceSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSessionImage");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 23, 16, 4, 54, 796, DateTimeKind.Utc).AddTicks(6965));
        }
    }
}
