using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassScheduleAndTimetableIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassScheduleId",
                table: "AttendanceSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    SemesterId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    ScheduleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSchedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSchedule_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedule_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedule_Semester_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedule_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedule_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 17, 55, 45, 598, DateTimeKind.Utc).AddTicks(1582));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_ClassScheduleId",
                table: "AttendanceSession",
                column: "ClassScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_CourseId",
                table: "ClassSchedule",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_GroupId",
                table: "ClassSchedule",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_SemesterId",
                table: "ClassSchedule",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_StaffId",
                table: "ClassSchedule",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_SubjectId",
                table: "ClassSchedule",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_Tenant_Date_Period",
                table: "ClassSchedule",
                columns: new[] { "TenantId", "ScheduleDate", "PeriodNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedule_Tenant_Staff_Date",
                table: "ClassSchedule",
                columns: new[] { "TenantId", "StaffId", "ScheduleDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceSession_ClassSchedule_ClassScheduleId",
                table: "AttendanceSession",
                column: "ClassScheduleId",
                principalTable: "ClassSchedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceSession_ClassSchedule_ClassScheduleId",
                table: "AttendanceSession");

            migrationBuilder.DropTable(
                name: "ClassSchedule");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSession_ClassScheduleId",
                table: "AttendanceSession");

            migrationBuilder.DropColumn(
                name: "ClassScheduleId",
                table: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 17, 7, 37, 802, DateTimeKind.Utc).AddTicks(3725));
        }
    }
}
