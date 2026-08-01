using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2_EnterpriseTimetableDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "RequiresAttendance",
                table: "Subject",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "SchedulingTimetable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    TimeSlotSetId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetable_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetable_SchedulingAcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetable_SchedulingTimeSlotSet_TimeSlotSetId",
                        column: x => x.TimeSlotSetId,
                        principalTable: "SchedulingTimeSlotSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimetableEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimetableId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: false),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: false),
                    SubjectAllocationId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    SemesterId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimetableEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_SchedulingRoom_RoomId",
                        column: x => x.RoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_SchedulingSubjectAllocation_Subjec~",
                        column: x => x.SubjectAllocationId,
                        principalTable: "SchedulingSubjectAllocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_SchedulingTimeSlot_TimeSlotId",
                        column: x => x.TimeSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_SchedulingTimetable_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "SchedulingTimetable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_Semester_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingTimetableEntry_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 36, "View", "Scheduling.Timetable.View", "Scheduling.Timetable" },
                    { 37, "Manage", "Scheduling.Timetable.Manage", "Scheduling.Timetable" }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 17, 7, 21, 977, DateTimeKind.Utc).AddTicks(5690));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 36 },
                    { 100, 37 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_AcademicYearId",
                table: "SchedulingTimetable",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_DepartmentId",
                table: "SchedulingTimetable",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_TenantId_AcademicYearId_Code",
                table: "SchedulingTimetable",
                columns: new[] { "TenantId", "AcademicYearId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetable_TimeSlotSetId",
                table: "SchedulingTimetable",
                column: "TimeSlotSetId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_CourseId",
                table: "SchedulingTimetableEntry",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_DepartmentId",
                table: "SchedulingTimetableEntry",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_GroupId",
                table: "SchedulingTimetableEntry",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_RoomId",
                table: "SchedulingTimetableEntry",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_SemesterId",
                table: "SchedulingTimetableEntry",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_StaffId",
                table: "SchedulingTimetableEntry",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_SubjectAllocationId",
                table: "SchedulingTimetableEntry",
                column: "SubjectAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_SubjectId",
                table: "SchedulingTimetableEntry",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TenantId_TimetableId_CourseId_Grou~",
                table: "SchedulingTimetableEntry",
                columns: new[] { "TenantId", "TimetableId", "CourseId", "GroupId", "SemesterId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TenantId_TimetableId_DayOfWeek_Tim~",
                table: "SchedulingTimetableEntry",
                columns: new[] { "TenantId", "TimetableId", "DayOfWeek", "TimeSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TenantId_TimetableId_RoomId",
                table: "SchedulingTimetableEntry",
                columns: new[] { "TenantId", "TimetableId", "RoomId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TenantId_TimetableId_StaffId",
                table: "SchedulingTimetableEntry",
                columns: new[] { "TenantId", "TimetableId", "StaffId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TimeSlotId",
                table: "SchedulingTimetableEntry",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimetableEntry_TimetableId",
                table: "SchedulingTimetableEntry",
                column: "TimetableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingTimetableEntry");

            migrationBuilder.DropTable(
                name: "SchedulingTimetable");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 36 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 37 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresAttendance",
                table: "Subject",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 16, 28, 47, 865, DateTimeKind.Utc).AddTicks(7815));
        }
    }
}
