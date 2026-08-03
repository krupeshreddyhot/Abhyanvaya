using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2B_ConflictDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 54, "View", "Scheduling.Conflict.View", "Scheduling.Conflict" },
                    { 55, "Manage", "Scheduling.Conflict.Manage", "Scheduling.Conflict" }
                });

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 54 },
                    { 100, 55 }
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictDetectionRun",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalConflicts = table.Column<int>(type: "integer", nullable: false),
                    FacultyCount = table.Column<int>(type: "integer", nullable: false),
                    RoomCount = table.Column<int>(type: "integer", nullable: false),
                    StudentCount = table.Column<int>(type: "integer", nullable: false),
                    CalendarCount = table.Column<int>(type: "integer", nullable: false),
                    CriticalCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    InformationCount = table.Column<int>(type: "integer", nullable: false),
                    TriggerSource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictDetectionRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingConflictDetectionRun_SchedulingAcademicYear_Acade~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingConflictDetectionRun_SchedulingTimetable_Timetabl~",
                        column: x => x.TimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingConflictFinding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConflictDetectionRunId = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Severity = table.Column<byte>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WhyOccurred = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SuggestedResolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    TimetableEntryId = table.Column<int>(type: "integer", nullable: true),
                    RelatedEntryId = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: true),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: true),
                    StaffId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CourseId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    SemesterId = table.Column<int>(type: "integer", nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    NavigationPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingConflictFinding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingConflictFinding_SchedulingConflictDetectionRun_Co~",
                        column: x => x.ConflictDetectionRunId,
                        principalTable: "SchedulingConflictDetectionRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 7, 43, 2, 963, DateTimeKind.Utc).AddTicks(6333));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictDetectionRun_AcademicYearId",
                table: "SchedulingConflictDetectionRun",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictDetectionRun_TenantId_AcademicYearId_Star~",
                table: "SchedulingConflictDetectionRun",
                columns: new[] { "TenantId", "AcademicYearId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictDetectionRun_TenantId_TimetableId_Started~",
                table: "SchedulingConflictDetectionRun",
                columns: new[] { "TenantId", "TimetableId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictDetectionRun_TimetableId",
                table: "SchedulingConflictDetectionRun",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictFinding_ConflictDetectionRunId",
                table: "SchedulingConflictFinding",
                column: "ConflictDetectionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictFinding_TenantId_ConflictDetectionRunId_C~",
                table: "SchedulingConflictFinding",
                columns: new[] { "TenantId", "ConflictDetectionRunId", "Category", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictFinding_TenantId_RoomId",
                table: "SchedulingConflictFinding",
                columns: new[] { "TenantId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictFinding_TenantId_StaffId",
                table: "SchedulingConflictFinding",
                columns: new[] { "TenantId", "StaffId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingConflictFinding_TenantId_TimetableEntryId",
                table: "SchedulingConflictFinding",
                columns: new[] { "TenantId", "TimetableEntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingConflictFinding");

            migrationBuilder.DropTable(
                name: "SchedulingConflictDetectionRun");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 54 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 55 });

            migrationBuilder.DeleteData(table: "Permission", keyColumn: "Id", keyValue: 54);
            migrationBuilder.DeleteData(table: "Permission", keyColumn: "Id", keyValue: 55);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 18, 53, 17, 24, DateTimeKind.Utc).AddTicks(3545));
        }
    }
}
