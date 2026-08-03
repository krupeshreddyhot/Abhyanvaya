using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2A5_GovernanceEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "NewStatus",
                table: "SchedulingTimetableApprovalHistory",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "OldStatus",
                table: "SchedulingTimetableApprovalHistory",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveComments",
                table: "SchedulingTimetable",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveReasonId",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedBy",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "SchedulingTimetable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreezeReason",
                table: "SchedulingTimetable",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrozenBy",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FrozenDate",
                table: "SchedulingTimetable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "SchedulingTimetable",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceVersionId",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockDate",
                table: "SchedulingTimetable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockReason",
                table: "SchedulingTimetable",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnlockedBy",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveComments",
                table: "SchedulingScheduleVersion",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveReasonId",
                table: "SchedulingScheduleVersion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceVersionId",
                table: "SchedulingScheduleVersion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchedulingArchiveReason",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SchedulingArchiveReason", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableApprovalComment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDecisionNote = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableApprovalComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableApprovalComment_SchedulingTimetableAppro~",
                        column: x => x.RequestId,
                        principalTable: "SchedulingTimetableApprovalRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableDecisionHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<byte>(type: "smallint", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewerRemarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OldStatus = table.Column<byte>(type: "smallint", nullable: true),
                    NewStatus = table.Column<byte>(type: "smallint", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableDecisionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableDecisionHistory_SchedulingTimetableAppro~",
                        column: x => x.RequestId,
                        principalTable: "SchedulingTimetableApprovalRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 46, "View", "Scheduling.VersionCompare.View", "Scheduling.VersionCompare" },
                    { 47, "Export", "Scheduling.VersionCompare.Export", "Scheduling.VersionCompare" },
                    { 48, "View", "Scheduling.ApprovalComments.View", "Scheduling.ApprovalComments" },
                    { 49, "Manage", "Scheduling.ApprovalComments.Manage", "Scheduling.ApprovalComments" },
                    { 50, "Freeze", "Scheduling.Freeze", "Scheduling" },
                    { 51, "Unlock", "Scheduling.Unlock", "Scheduling" },
                    { 52, "View", "Scheduling.Archive.View", "Scheduling.Archive" },
                    { 53, "Manage", "Scheduling.Archive.Manage", "Scheduling.Archive" }
                });

            migrationBuilder.InsertData(
                table: "SchedulingArchiveReason",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 701, (byte)1, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Replaced by a newer schedule version", true, false, "Superseded", 1, 1, null, null },
                    { 702, (byte)2, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Academic term or semester completed", true, false, "Semester Complete", 2, 1, null, null },
                    { 703, (byte)3, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Archived due to corrections", true, false, "Correction", 3, 1, null, null },
                    { 704, (byte)4, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Emergency operational change", true, false, "Emergency", 4, 1, null, null },
                    { 705, (byte)5, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Academic council directive", true, false, "Academic Council", 5, 1, null, null },
                    { 706, (byte)6, null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Other archive reason", true, false, "Other", 6, 1, null, null }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 18, 53, 17, 24, DateTimeKind.Utc).AddTicks(3545));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 46 },
                    { 100, 47 },
                    { 100, 48 },
                    { 100, 49 },
                    { 100, 50 },
                    { 100, 51 },
                    { 100, 52 },
                    { 100, 53 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_ArchiveReasonId",
                table: "SchedulingTimetable",
                column: "ArchiveReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_ReferenceVersionId",
                table: "SchedulingTimetable",
                column: "ReferenceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_TenantId_IsFrozen",
                table: "SchedulingTimetable",
                columns: new[] { "TenantId", "IsFrozen" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_ArchiveReasonId",
                table: "SchedulingScheduleVersion",
                column: "ArchiveReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_ReferenceVersionId",
                table: "SchedulingScheduleVersion",
                column: "ReferenceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingArchiveReason_TenantId_Code",
                table: "SchedulingArchiveReason",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalComment_RequestId",
                table: "SchedulingTimetableApprovalComment",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalComment_TenantId_RequestId_Occur~",
                table: "SchedulingTimetableApprovalComment",
                columns: new[] { "TenantId", "RequestId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableDecisionHistory_RequestId",
                table: "SchedulingTimetableDecisionHistory",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableDecisionHistory_TenantId_RequestId_Occur~",
                table: "SchedulingTimetableDecisionHistory",
                columns: new[] { "TenantId", "RequestId", "OccurredUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingScheduleVersion_SchedulingArchiveReason_ArchiveRe~",
                table: "SchedulingScheduleVersion",
                column: "ArchiveReasonId",
                principalTable: "SchedulingArchiveReason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingScheduleVersion_SchedulingScheduleVersion_Referen~",
                table: "SchedulingScheduleVersion",
                column: "ReferenceVersionId",
                principalTable: "SchedulingScheduleVersion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingTimetable_SchedulingArchiveReason_ArchiveReasonId",
                table: "SchedulingTimetable",
                column: "ArchiveReasonId",
                principalTable: "SchedulingArchiveReason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingTimetable_SchedulingScheduleVersion_ReferenceVers~",
                table: "SchedulingTimetable",
                column: "ReferenceVersionId",
                principalTable: "SchedulingScheduleVersion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingScheduleVersion_SchedulingArchiveReason_ArchiveRe~",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingScheduleVersion_SchedulingScheduleVersion_Referen~",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingTimetable_SchedulingArchiveReason_ArchiveReasonId",
                table: "SchedulingTimetable");

            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingTimetable_SchedulingScheduleVersion_ReferenceVers~",
                table: "SchedulingTimetable");

            migrationBuilder.DropTable(
                name: "SchedulingArchiveReason");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableApprovalComment");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableDecisionHistory");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimetable_ArchiveReasonId",
                table: "SchedulingTimetable");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimetable_ReferenceVersionId",
                table: "SchedulingTimetable");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimetable_TenantId_IsFrozen",
                table: "SchedulingTimetable");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingScheduleVersion_ArchiveReasonId",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingScheduleVersion_ReferenceVersionId",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 46 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 47 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 48 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 49 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 50 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 51 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 52 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 53 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DropColumn(
                name: "NewStatus",
                table: "SchedulingTimetableApprovalHistory");

            migrationBuilder.DropColumn(
                name: "OldStatus",
                table: "SchedulingTimetableApprovalHistory");

            migrationBuilder.DropColumn(
                name: "ArchiveComments",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "ArchiveReasonId",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "FreezeReason",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "FrozenBy",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "FrozenDate",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "ReferenceVersionId",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "UnlockDate",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "UnlockReason",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "UnlockedBy",
                table: "SchedulingTimetable");

            migrationBuilder.DropColumn(
                name: "ArchiveComments",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DropColumn(
                name: "ArchiveReasonId",
                table: "SchedulingScheduleVersion");

            migrationBuilder.DropColumn(
                name: "ReferenceVersionId",
                table: "SchedulingScheduleVersion");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 18, 10, 15, 198, DateTimeKind.Utc).AddTicks(2686));
        }
    }
}
