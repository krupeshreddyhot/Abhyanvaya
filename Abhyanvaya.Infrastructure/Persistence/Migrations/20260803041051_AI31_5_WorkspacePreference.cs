using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI31_5_WorkspacePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingWorkspacePreference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LandingPage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DashboardLayout = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DefaultTimetableView = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FavoriteQuickActionsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ThemePreference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NotificationPreferencesJson = table.Column<string>(type: "text", nullable: false),
                    OneHandedMode = table.Column<bool>(type: "boolean", nullable: false),
                    HighContrast = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingWorkspacePreference", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 3, 4, 10, 45, 324, DateTimeKind.Utc).AddTicks(465));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingWorkspacePreference_TenantId_StaffId",
                table: "SchedulingWorkspacePreference",
                columns: new[] { "TenantId", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingWorkspacePreference_TenantId_UserId",
                table: "SchedulingWorkspacePreference",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingWorkspacePreference");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 18, 53, 40, 689, DateTimeKind.Utc).AddTicks(3836));
        }
    }
}
