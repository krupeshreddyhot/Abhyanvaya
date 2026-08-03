using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2B5_ConflictIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingConflictRuleConfigChangeHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThresholdKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OldValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictRuleConfigChangeHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictRuleThresholdSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThresholdKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictRuleThresholdSetting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictWorkspaceBookmark",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FilterJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictWorkspaceBookmark", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictWorkspaceNote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConflictDetectionRunId = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TimetableEntryId = table.Column<int>(type: "integer", nullable: true),
                    NoteText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictWorkspaceNote", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictWorkspacePin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConflictDetectionRunId = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TimetableEntryId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictWorkspacePin", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 9, 22, 54, 11, DateTimeKind.Utc).AddTicks(7357));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictRuleConfigChangeHistory_TenantId_Threshol~",
                table: "SchedulingConflictRuleConfigChangeHistory",
                columns: new[] { "TenantId", "ThresholdKey", "ChangedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictRuleThresholdSetting_TenantId_ThresholdKey",
                table: "SchedulingConflictRuleThresholdSetting",
                columns: new[] { "TenantId", "ThresholdKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictWorkspaceBookmark_TenantId_UserId_Name",
                table: "SchedulingConflictWorkspaceBookmark",
                columns: new[] { "TenantId", "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictWorkspaceNote_TenantId_UserId_ConflictDet~",
                table: "SchedulingConflictWorkspaceNote",
                columns: new[] { "TenantId", "UserId", "ConflictDetectionRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictWorkspacePin_TenantId_UserId_ConflictDete~",
                table: "SchedulingConflictWorkspacePin",
                columns: new[] { "TenantId", "UserId", "ConflictDetectionRunId", "RuleCode", "TimetableEntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingConflictRuleConfigChangeHistory");

            migrationBuilder.DropTable(
                name: "SchedulingConflictRuleThresholdSetting");

            migrationBuilder.DropTable(
                name: "SchedulingConflictWorkspaceBookmark");

            migrationBuilder.DropTable(
                name: "SchedulingConflictWorkspaceNote");

            migrationBuilder.DropTable(
                name: "SchedulingConflictWorkspacePin");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 7, 43, 2, 963, DateTimeKind.Utc).AddTicks(6333));
        }
    }
}
