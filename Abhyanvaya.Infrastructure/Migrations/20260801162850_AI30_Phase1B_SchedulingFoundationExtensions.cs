using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase1B_SchedulingFoundationExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryTypeId",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedCapacity",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredRoomFeatureId",
                table: "Subject",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAttendance",
                table: "Subject",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Colour",
                table: "SchedulingHoliday",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HolidayTypeCatalogId",
                table: "SchedulingHoliday",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWorkingDayOverride",
                table: "SchedulingHoliday",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "SchedulingHoliday",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresRescheduling",
                table: "SchedulingHoliday",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SchedulingFacultyTeachingPreference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    PreferredCampusId = table.Column<int>(type: "integer", nullable: true),
                    PreferredBuildingId = table.Column<int>(type: "integer", nullable: true),
                    PreferredFloorId = table.Column<int>(type: "integer", nullable: true),
                    PreferredRoomId = table.Column<int>(type: "integer", nullable: true),
                    PreferredSubjectId = table.Column<int>(type: "integer", nullable: true),
                    PreferredDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    PreferredCourseId = table.Column<int>(type: "integer", nullable: true),
                    PreferredGroupId = table.Column<int>(type: "integer", nullable: true),
                    PreferredSemesterId = table.Column<int>(type: "integer", nullable: true),
                    PreferredFirstPeriod = table.Column<int>(type: "integer", nullable: true),
                    PreferredLastPeriod = table.Column<int>(type: "integer", nullable: true),
                    PreferredWorkingDaysFlags = table.Column<byte>(type: "smallint", nullable: false),
                    MaximumContinuousClasses = table.Column<int>(type: "integer", nullable: false),
                    MinimumBreakBetweenClasses = table.Column<int>(type: "integer", nullable: false),
                    PreferredTeachingMode = table.Column<byte>(type: "smallint", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_SchedulingFacultyTeachingPreference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_Course_PreferredCourseId",
                        column: x => x.PreferredCourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_Department_PreferredDep~",
                        column: x => x.PreferredDepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_Group_PreferredGroupId",
                        column: x => x.PreferredGroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_SchedulingAcademicYear_~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_SchedulingBuilding_Pref~",
                        column: x => x.PreferredBuildingId,
                        principalTable: "SchedulingBuilding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_SchedulingCampus_Prefer~",
                        column: x => x.PreferredCampusId,
                        principalTable: "SchedulingCampus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_SchedulingFloor_Preferr~",
                        column: x => x.PreferredFloorId,
                        principalTable: "SchedulingFloor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_SchedulingRoom_Preferre~",
                        column: x => x.PreferredRoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_Semester_PreferredSemes~",
                        column: x => x.PreferredSemesterId,
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTeachingPreference_Subject_PreferredSubjec~",
                        column: x => x.PreferredSubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingHolidayTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Colour = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SchedulingHolidayTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingRoomFeature",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_SchedulingRoomFeature", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingSubjectDeliveryType",
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
                    table.PrimaryKey("PK_SchedulingSubjectDeliveryType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingRoomFeatureAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    RoomFeatureId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingRoomFeatureAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomFeatureAssignment_SchedulingRoomFeature_RoomF~",
                        column: x => x.RoomFeatureId,
                        principalTable: "SchedulingRoomFeature",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomFeatureAssignment_SchedulingRoom_RoomId",
                        column: x => x.RoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 28, "View", "Scheduling.FacultyPreferences.View", "Scheduling.FacultyPreferences" },
                    { 29, "Manage", "Scheduling.FacultyPreferences.Manage", "Scheduling.FacultyPreferences" },
                    { 30, "View", "Scheduling.RoomFeatures.View", "Scheduling.RoomFeatures" },
                    { 31, "Manage", "Scheduling.RoomFeatures.Manage", "Scheduling.RoomFeatures" },
                    { 32, "View", "Scheduling.SubjectDelivery.View", "Scheduling.SubjectDelivery" },
                    { 33, "Manage", "Scheduling.SubjectDelivery.Manage", "Scheduling.SubjectDelivery" },
                    { 34, "View", "Scheduling.HolidayTypes.View", "Scheduling.HolidayTypes" },
                    { 35, "Manage", "Scheduling.HolidayTypes.Manage", "Scheduling.HolidayTypes" }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 16, 28, 47, 865, DateTimeKind.Utc).AddTicks(7815));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 28 },
                    { 100, 29 },
                    { 100, 30 },
                    { 100, 31 },
                    { 100, 32 },
                    { 100, 33 },
                    { 100, 34 },
                    { 100, 35 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subject_DeliveryTypeId",
                table: "Subject",
                column: "DeliveryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Subject_PreferredRoomFeatureId",
                table: "Subject",
                column: "PreferredRoomFeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingHoliday_HolidayTypeCatalogId",
                table: "SchedulingHoliday",
                column: "HolidayTypeCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_AcademicYearId",
                table: "SchedulingFacultyTeachingPreference",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredBuildingId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredBuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredCampusId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredCampusId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredCourseId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredDepartmentId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredFloorId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredFloorId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredGroupId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredRoomId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredSemesterId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredSemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_PreferredSubjectId",
                table: "SchedulingFacultyTeachingPreference",
                column: "PreferredSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_StaffId",
                table: "SchedulingFacultyTeachingPreference",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTeachingPreference_TenantId_StaffId_Academ~",
                table: "SchedulingFacultyTeachingPreference",
                columns: new[] { "TenantId", "StaffId", "AcademicYearId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingHolidayTypes_TenantId_Code",
                table: "SchedulingHolidayTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomFeature_TenantId_Code",
                table: "SchedulingRoomFeature",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomFeatureAssignment_RoomFeatureId",
                table: "SchedulingRoomFeatureAssignment",
                column: "RoomFeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomFeatureAssignment_RoomId",
                table: "SchedulingRoomFeatureAssignment",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomFeatureAssignment_TenantId_RoomId_RoomFeature~",
                table: "SchedulingRoomFeatureAssignment",
                columns: new[] { "TenantId", "RoomId", "RoomFeatureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectDeliveryType_TenantId_Code",
                table: "SchedulingSubjectDeliveryType",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SchedulingHoliday_SchedulingHolidayTypes_HolidayTypeCatalog~",
                table: "SchedulingHoliday",
                column: "HolidayTypeCatalogId",
                principalTable: "SchedulingHolidayTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subject_SchedulingRoomFeature_PreferredRoomFeatureId",
                table: "Subject",
                column: "PreferredRoomFeatureId",
                principalTable: "SchedulingRoomFeature",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subject_SchedulingSubjectDeliveryType_DeliveryTypeId",
                table: "Subject",
                column: "DeliveryTypeId",
                principalTable: "SchedulingSubjectDeliveryType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchedulingHoliday_SchedulingHolidayTypes_HolidayTypeCatalog~",
                table: "SchedulingHoliday");

            migrationBuilder.DropForeignKey(
                name: "FK_Subject_SchedulingRoomFeature_PreferredRoomFeatureId",
                table: "Subject");

            migrationBuilder.DropForeignKey(
                name: "FK_Subject_SchedulingSubjectDeliveryType_DeliveryTypeId",
                table: "Subject");

            migrationBuilder.DropTable(
                name: "SchedulingFacultyTeachingPreference");

            migrationBuilder.DropTable(
                name: "SchedulingHolidayTypes");

            migrationBuilder.DropTable(
                name: "SchedulingRoomFeatureAssignment");

            migrationBuilder.DropTable(
                name: "SchedulingSubjectDeliveryType");

            migrationBuilder.DropTable(
                name: "SchedulingRoomFeature");

            migrationBuilder.DropIndex(
                name: "IX_Subject_DeliveryTypeId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_Subject_PreferredRoomFeatureId",
                table: "Subject");

            migrationBuilder.DropIndex(
                name: "IX_SchedulingHoliday_HolidayTypeCatalogId",
                table: "SchedulingHoliday");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 28 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 29 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 30 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 31 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 32 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 33 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 34 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 35 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DropColumn(
                name: "DeliveryTypeId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "ExpectedCapacity",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "PreferredRoomFeatureId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "RequiresAttendance",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "Colour",
                table: "SchedulingHoliday");

            migrationBuilder.DropColumn(
                name: "HolidayTypeCatalogId",
                table: "SchedulingHoliday");

            migrationBuilder.DropColumn(
                name: "IsWorkingDayOverride",
                table: "SchedulingHoliday");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "SchedulingHoliday");

            migrationBuilder.DropColumn(
                name: "RequiresRescheduling",
                table: "SchedulingHoliday");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 15, 50, 4, 534, DateTimeKind.Utc).AddTicks(890));
        }
    }
}
