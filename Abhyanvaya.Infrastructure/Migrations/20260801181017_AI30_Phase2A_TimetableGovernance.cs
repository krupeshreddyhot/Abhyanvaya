using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2A_TimetableGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleVersionId",
                table: "SchedulingTimetable",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchedulingScheduleVersion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    AcademicTermId = table.Column<int>(type: "integer", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    VersionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<int>(type: "integer", nullable: true),
                    ArchivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedBy = table.Column<int>(type: "integer", nullable: true),
                    ParentVersionId = table.Column<int>(type: "integer", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingScheduleVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingScheduleVersion_SchedulingAcademicTerm_AcademicTe~",
                        column: x => x.AcademicTermId,
                        principalTable: "SchedulingAcademicTerm",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingScheduleVersion_SchedulingAcademicYear_AcademicYe~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingScheduleVersion_SchedulingScheduleVersion_ParentV~",
                        column: x => x.ParentVersionId,
                        principalTable: "SchedulingScheduleVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableChangeHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimetableId = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Operation = table.Column<byte>(type: "smallint", nullable: false),
                    OldValueJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    NewValueJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableChangeHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableChangeHistory_SchedulingTimetable_Timeta~",
                        column: x => x.TimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableCloneJob",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobType = table.Column<byte>(type: "smallint", nullable: false),
                    SourceTimetableId = table.Column<int>(type: "integer", nullable: false),
                    TargetTimetableId = table.Column<int>(type: "integer", nullable: true),
                    PayloadJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequestedBy = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableCloneJob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableCloneJob_SchedulingTimetable_SourceTimet~",
                        column: x => x.SourceTimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableCloneJob_SchedulingTimetable_TargetTimet~",
                        column: x => x.TargetTimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableWarningDismissal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimetableId = table.Column<int>(type: "integer", nullable: false),
                    WarningCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    StaffId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: true),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: true),
                    DismissedBy = table.Column<int>(type: "integer", nullable: false),
                    DismissedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableWarningDismissal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableWarningDismissal_SchedulingTimetable_Tim~",
                        column: x => x.TimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableApprovalRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleVersionId = table.Column<int>(type: "integer", nullable: false),
                    TimetableId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SubmittedBy = table.Column<int>(type: "integer", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableApprovalRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableApprovalRequest_SchedulingScheduleVersio~",
                        column: x => x.ScheduleVersionId,
                        principalTable: "SchedulingScheduleVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableApprovalRequest_SchedulingTimetable_Time~",
                        column: x => x.TimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableApprovalHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<byte>(type: "smallint", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_SchedulingTimetableApprovalHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableApprovalHistory_SchedulingTimetableAppro~",
                        column: x => x.RequestId,
                        principalTable: "SchedulingTimetableApprovalRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableApprovalStep",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AssignedTo = table.Column<int>(type: "integer", nullable: true),
                    DecidedBy = table.Column<int>(type: "integer", nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<byte>(type: "smallint", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableApprovalStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableApprovalStep_SchedulingTimetableApproval~",
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
                    { 38, "View", "Scheduling.Version.View", "Scheduling.Version" },
                    { 39, "Manage", "Scheduling.Version.Manage", "Scheduling.Version" },
                    { 40, "Review", "Scheduling.Review", "Scheduling" },
                    { 41, "Approve", "Scheduling.Approve", "Scheduling" },
                    { 42, "Publish", "Scheduling.Publish", "Scheduling" },
                    { 43, "Archive", "Scheduling.Archive", "Scheduling" },
                    { 44, "Clone", "Scheduling.Clone", "Scheduling" },
                    { 45, "View", "Scheduling.History.View", "Scheduling.History" }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 18, 10, 15, 198, DateTimeKind.Utc).AddTicks(2686));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 38 },
                    { 100, 39 },
                    { 100, 40 },
                    { 100, 41 },
                    { 100, 42 },
                    { 100, 43 },
                    { 100, 44 },
                    { 100, 45 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_ScheduleVersionId",
                table: "SchedulingTimetable",
                column: "ScheduleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_TenantId_AcademicYearId_DepartmentId_St~",
                table: "SchedulingTimetable",
                columns: new[] { "TenantId", "AcademicYearId", "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_AcademicTermId",
                table: "SchedulingScheduleVersion",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_AcademicYearId",
                table: "SchedulingScheduleVersion",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_ParentVersionId",
                table: "SchedulingScheduleVersion",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingScheduleVersion_TenantId_AcademicYearId_AcademicT~",
                table: "SchedulingScheduleVersion",
                columns: new[] { "TenantId", "AcademicYearId", "AcademicTermId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalHistory_RequestId",
                table: "SchedulingTimetableApprovalHistory",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalRequest_ScheduleVersionId",
                table: "SchedulingTimetableApprovalRequest",
                column: "ScheduleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalRequest_TimetableId",
                table: "SchedulingTimetableApprovalRequest",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalStep_RequestId",
                table: "SchedulingTimetableApprovalStep",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableApprovalStep_TenantId_RequestId_StepOrder",
                table: "SchedulingTimetableApprovalStep",
                columns: new[] { "TenantId", "RequestId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableChangeHistory_TenantId_TimetableId_Occur~",
                table: "SchedulingTimetableChangeHistory",
                columns: new[] { "TenantId", "TimetableId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableChangeHistory_TimetableId",
                table: "SchedulingTimetableChangeHistory",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableCloneJob_SourceTimetableId",
                table: "SchedulingTimetableCloneJob",
                column: "SourceTimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableCloneJob_TargetTimetableId",
                table: "SchedulingTimetableCloneJob",
                column: "TargetTimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableWarningDismissal_TimetableId",
                table: "SchedulingTimetableWarningDismissal",
                column: "TimetableId");

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingTimetable_SchedulingScheduleVersion_ScheduleVersi~",
                table: "SchedulingTimetable",
                column: "ScheduleVersionId",
                principalTable: "SchedulingScheduleVersion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingTimetable_SchedulingScheduleVersion_ScheduleVersi~",
                table: "SchedulingTimetable");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableApprovalHistory");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableApprovalStep");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableChangeHistory");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableCloneJob");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableWarningDismissal");

            migrationBuilder.DropTable(
                name: "SchedulingTimetableApprovalRequest");

            migrationBuilder.DropTable(
                name: "SchedulingScheduleVersion");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimetable_ScheduleVersionId",
                table: "SchedulingTimetable");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimetable_TenantId_AcademicYearId_DepartmentId_St~",
                table: "SchedulingTimetable");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 38 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 39 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 40 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 41 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 42 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 43 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 44 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 45 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DropColumn(
                name: "ScheduleVersionId",
                table: "SchedulingTimetable");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 18, 6, 37, 139, DateTimeKind.Utc).AddTicks(2817));
        }
    }
}
