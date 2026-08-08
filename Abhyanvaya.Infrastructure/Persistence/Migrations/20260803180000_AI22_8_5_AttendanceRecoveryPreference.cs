using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI22.8.5.3 — tenant-scoped faculty recovery preferences.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803180000_AI22_8_5_AttendanceRecoveryPreference")]
public partial class AI22_8_5_AttendanceRecoveryPreference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceRecoveryPreference",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                StaffId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: false),
                AutoSaveFrequencySeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                ResumeConfirmation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DefaultLandingPage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "pending"),
                NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                SessionTimeoutWarning = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                SessionTimeoutWarningMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                PromptOnLogin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceRecoveryPreference", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceRecoveryPreference_TenantId_StaffId",
            table: "AttendanceRecoveryPreference",
            columns: new[] { "TenantId", "StaffId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceRecoveryPreference_TenantId_UserId",
            table: "AttendanceRecoveryPreference",
            columns: new[] { "TenantId", "UserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceRecoveryPreference");
    }
}
