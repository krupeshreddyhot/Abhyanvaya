using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase1A_SchedulingFoundationEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchedulingSubjectAllocation_TenantId_AcademicYearId_Subject~",
                table: "SchedulingSubjectAllocation");

            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLabEquipment",
                table: "Subject",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "RequiresRoomType",
                table: "Subject",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectCategoryId",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeSlotTemplateId",
                table: "SchedulingTimeSlotSet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "SchedulingSubjectAllocation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Department",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Department",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "SchedulingFacultyAvailability",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityType = table.Column<byte>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartSlotId = table.Column<int>(type: "integer", nullable: true),
                    EndSlotId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingFacultyAvailability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyAvailability_SchedulingAcademicYear_Academ~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyAvailability_SchedulingTimeSlot_EndSlotId",
                        column: x => x.EndSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyAvailability_SchedulingTimeSlot_StartSlotId",
                        column: x => x.StartSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyAvailability_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingRoomAvailability",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityType = table.Column<byte>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartSlotId = table.Column<int>(type: "integer", nullable: true),
                    EndSlotId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingRoomAvailability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAvailability_SchedulingAcademicYear_AcademicY~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAvailability_SchedulingRoom_RoomId",
                        column: x => x.RoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAvailability_SchedulingTimeSlot_EndSlotId",
                        column: x => x.EndSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAvailability_SchedulingTimeSlot_StartSlotId",
                        column: x => x.StartSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingSubjectCategory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_SchedulingSubjectCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimeSlotTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TemplateType = table.Column<byte>(type: "smallint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimeSlotTemplate", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 20, "View", "Scheduling.Department.View", "Scheduling.Department" },
                    { 21, "Manage", "Scheduling.Department.Manage", "Scheduling.Department" },
                    { 22, "View", "Scheduling.RoomAvailability.View", "Scheduling.RoomAvailability" },
                    { 23, "Manage", "Scheduling.RoomAvailability.Manage", "Scheduling.RoomAvailability" },
                    { 24, "View", "Scheduling.FacultyAvailability.View", "Scheduling.FacultyAvailability" },
                    { 25, "Manage", "Scheduling.FacultyAvailability.Manage", "Scheduling.FacultyAvailability" },
                    { 26, "View", "Scheduling.Template.View", "Scheduling.Template" },
                    { 27, "Manage", "Scheduling.Template.Manage", "Scheduling.Template" }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 15, 50, 4, 534, DateTimeKind.Utc).AddTicks(890));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 20 },
                    { 100, 21 },
                    { 100, 22 },
                    { 100, 23 },
                    { 100, 24 },
                    { 100, 25 },
                    { 100, 26 },
                    { 100, 27 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subject_SubjectCategoryId",
                table: "Subject",
                column: "SubjectCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlotSet_TimeSlotTemplateId",
                table: "SchedulingTimeSlotSet",
                column: "TimeSlotTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_DepartmentId",
                table: "SchedulingSubjectAllocation",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_TenantId_AcademicYearId_Subject~",
                table: "SchedulingSubjectAllocation",
                columns: new[] { "TenantId", "AcademicYearId", "SubjectId", "CourseId", "GroupId", "SemesterId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyAvailability_AcademicYearId",
                table: "SchedulingFacultyAvailability",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyAvailability_EndSlotId",
                table: "SchedulingFacultyAvailability",
                column: "EndSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyAvailability_StaffId",
                table: "SchedulingFacultyAvailability",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyAvailability_StartSlotId",
                table: "SchedulingFacultyAvailability",
                column: "StartSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyAvailability_TenantId_StaffId_AcademicYear~",
                table: "SchedulingFacultyAvailability",
                columns: new[] { "TenantId", "StaffId", "AcademicYearId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAvailability_AcademicYearId",
                table: "SchedulingRoomAvailability",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAvailability_EndSlotId",
                table: "SchedulingRoomAvailability",
                column: "EndSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAvailability_RoomId",
                table: "SchedulingRoomAvailability",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAvailability_StartSlotId",
                table: "SchedulingRoomAvailability",
                column: "StartSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAvailability_TenantId_RoomId_AcademicYearId_S~",
                table: "SchedulingRoomAvailability",
                columns: new[] { "TenantId", "RoomId", "AcademicYearId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectCategory_TenantId_Code",
                table: "SchedulingSubjectCategory",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlotTemplate_TenantId_Name",
                table: "SchedulingTimeSlotTemplate",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingSubjectAllocation_Department_DepartmentId",
                table: "SchedulingSubjectAllocation",
                column: "DepartmentId",
                principalTable: "Department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingTimeSlotSet_SchedulingTimeSlotTemplate_TimeSlotTe~",
                table: "SchedulingTimeSlotSet",
                column: "TimeSlotTemplateId",
                principalTable: "SchedulingTimeSlotTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subject_SchedulingSubjectCategory_SubjectCategoryId",
                table: "Subject",
                column: "SubjectCategoryId",
                principalTable: "SchedulingSubjectCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingSubjectAllocation_Department_DepartmentId",
                table: "SchedulingSubjectAllocation");

            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingTimeSlotSet_SchedulingTimeSlotTemplate_TimeSlotTe~",
                table: "SchedulingTimeSlotSet");

            migrationBuilder.DropForeignKey(
                name: "FK_Subject_SchedulingSubjectCategory_SubjectCategoryId",
                table: "Subject");

            migrationBuilder.DropTable(
                name: "SchedulingFacultyAvailability");

            migrationBuilder.DropTable(
                name: "SchedulingRoomAvailability");

            migrationBuilder.DropTable(
                name: "SchedulingSubjectCategory");

            migrationBuilder.DropTable(
                name: "SchedulingTimeSlotTemplate");

            migrationBuilder.DropIndex(
                name: "IX_Subject_SubjectCategoryId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingTimeSlotSet_TimeSlotTemplateId",
                table: "SchedulingTimeSlotSet");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingSubjectAllocation_DepartmentId",
                table: "SchedulingSubjectAllocation");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingSubjectAllocation_TenantId_AcademicYearId_Subject~",
                table: "SchedulingSubjectAllocation");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 20 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 21 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 22 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 23 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 24 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 25 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 26 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 27 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "RequiresLabEquipment",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "RequiresRoomType",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "SubjectCategoryId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "TimeSlotTemplateId",
                table: "SchedulingTimeSlotSet");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "SchedulingSubjectAllocation");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Department");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 14, 36, 51, 205, DateTimeKind.Utc).AddTicks(3344));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_TenantId_AcademicYearId_Subject~",
                table: "SchedulingSubjectAllocation",
                columns: new[] { "TenantId", "AcademicYearId", "SubjectId", "CourseId", "GroupId", "SemesterId" },
                unique: true);
        }
    }
}
