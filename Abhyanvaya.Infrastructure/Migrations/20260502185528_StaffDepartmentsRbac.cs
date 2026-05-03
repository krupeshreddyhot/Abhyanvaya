using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffDepartmentsRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StaffId",
                table: "User",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRole", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollegeRoleLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsExclusivePerCollege = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollegeRoleLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Department",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollegeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Department_College_CollegeId",
                        column: x => x.CollegeId,
                        principalTable: "College",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentRoleLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsExclusivePerDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentRoleLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesignationLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_DesignationLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentStatusLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_EmploymentStatusLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonTitleLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_PersonTitleLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualificationLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_QualificationLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_StaffTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserApplicationRole",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ApplicationRoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApplicationRole", x => new { x.UserId, x.ApplicationRoleId });
                    table.ForeignKey(
                        name: "FK_UserApplicationRole_ApplicationRole_ApplicationRoleId",
                        column: x => x.ApplicationRoleId,
                        principalTable: "ApplicationRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserApplicationRole_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationRolePermission",
                columns: table => new
                {
                    ApplicationRoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRolePermission", x => new { x.ApplicationRoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_ApplicationRolePermission_ApplicationRole_ApplicationRoleId",
                        column: x => x.ApplicationRoleId,
                        principalTable: "ApplicationRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationRolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollegeId = table.Column<int>(type: "integer", nullable: false),
                    StaffCode = table.Column<string>(type: "text", nullable: true),
                    StaffTypeId = table.Column<int>(type: "integer", nullable: false),
                    PersonTitleId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: false),
                    QualificationId = table.Column<int>(type: "integer", nullable: true),
                    GenderId = table.Column<int>(type: "integer", nullable: true),
                    EmploymentStatusId = table.Column<int>(type: "integer", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    AltPhone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    DateOfJoining = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMembers_College_CollegeId",
                        column: x => x.CollegeId,
                        principalTable: "College",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_DesignationLookup_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "DesignationLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_EmploymentStatusLookup_EmploymentStatusId",
                        column: x => x.EmploymentStatusId,
                        principalTable: "EmploymentStatusLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_Gender_GenderId",
                        column: x => x.GenderId,
                        principalTable: "Gender",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_PersonTitleLookup_PersonTitleId",
                        column: x => x.PersonTitleId,
                        principalTable: "PersonTitleLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_QualificationLookup_QualificationId",
                        column: x => x.QualificationId,
                        principalTable: "QualificationLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMembers_StaffTypeLookup_StaffTypeId",
                        column: x => x.StaffTypeId,
                        principalTable: "StaffTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffCollegeRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    CollegeRoleLookupId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCollegeRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffCollegeRole_CollegeRoleLookup_CollegeRoleLookupId",
                        column: x => x.CollegeRoleLookupId,
                        principalTable: "CollegeRoleLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffCollegeRole_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffDepartment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDepartment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDepartment_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffDepartment_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffSubjectAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffSubjectAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffSubjectAssignment_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffSubjectAssignment_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffDepartmentRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffDepartmentId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentRoleLookupId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDepartmentRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDepartmentRole_DepartmentRoleLookup_DepartmentRoleLook~",
                        column: x => x.DepartmentRoleLookupId,
                        principalTable: "DepartmentRoleLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffDepartmentRole_StaffDepartment_StaffDepartmentId",
                        column: x => x.StaffDepartmentId,
                        principalTable: "StaffDepartment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ApplicationRole",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "Name", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 100, "ADMIN", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Full tenant administration (legacy Admin enum)", false, "Administrator", 1, null, null },
                    { 101, "FACULTY", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Teaching staff (legacy Faculty enum)", false, "Faculty", 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "CollegeRoleLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "IsExclusivePerCollege", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 509, "PRINCIPAL", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, true, "Principal", 1, 1, null, null },
                    { 510, "VP", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, false, "Vice Principal", 2, 1, null, null },
                    { 511, "CORR", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, false, "Correspondent", 3, 1, null, null },
                    { 512, "CCE", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, false, "Chief Controller of Examinations", 4, 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "DepartmentRoleLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "IsExclusivePerDepartment", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 507, "HOD", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, true, "Head of Department", 1, 1, null, null },
                    { 508, "ACAD_COORD", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, false, "Academic Coordinator", 2, 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "DesignationLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 505, "LECT", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Lecturer", 1, 1, null, null },
                    { 506, "PROF", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Professor", 2, 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "EmploymentStatusLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[] { 515, "ACTIVE", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Active", 1, 1, null, null });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 1, "View", "Students.View", "Students" },
                    { 2, "Manage", "Students.Manage", "Students" },
                    { 3, "View", "Attendance.View", "Attendance" },
                    { 4, "Manage", "Attendance.Manage", "Attendance" },
                    { 5, "View", "Reports.View", "Reports" },
                    { 6, "Manage", "Setup.Subjects.Manage", "Setup.Subjects" },
                    { 7, "Manage", "Setup.Departments.Manage", "Setup.Departments" },
                    { 8, "Manage", "Setup.Staff.Manage", "Setup.Staff" },
                    { 9, "View", "Dashboard.View", "Dashboard" },
                    { 10, "Manage", "Organization.Manage", "Organization" },
                    { 11, "View", "Master.View", "Master" },
                    { 12, "Manage", "Setup.Lookups.Manage", "Setup.Lookups" }
                });

            migrationBuilder.InsertData(
                table: "PersonTitleLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 503, "DR", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Dr", 1, 1, null, null },
                    { 504, "MR", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Mr", 2, 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "QualificationLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 513, "PHD", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Ph.D.", 1, 1, null, null },
                    { 514, "MA", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "M.A.", 2, 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "StaffTypeLookup",
                columns: new[] { "Id", "Code", "CreatedBy", "CreatedDate", "IsActive", "IsDeleted", "Name", "SortOrder", "TenantId", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 501, "TEACHING", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Teaching", 1, 1, null, null },
                    { 502, "NONTEACHING", null, new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Non-teaching", 2, 1, null, null }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedDate", "StaffId" },
                values: new object[] { new DateTime(2026, 5, 2, 18, 55, 21, 586, DateTimeKind.Utc).AddTicks(7552), null });

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 1 },
                    { 100, 2 },
                    { 100, 3 },
                    { 100, 4 },
                    { 100, 5 },
                    { 100, 6 },
                    { 100, 7 },
                    { 100, 8 },
                    { 100, 9 },
                    { 100, 10 },
                    { 100, 11 },
                    { 100, 12 },
                    { 101, 1 },
                    { 101, 3 },
                    { 101, 4 },
                    { 101, 5 },
                    { 101, 9 },
                    { 101, 11 }
                });

            migrationBuilder.InsertData(
                table: "UserApplicationRole",
                columns: new[] { "ApplicationRoleId", "UserId" },
                values: new object[] { 100, 1 });

            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Permission"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Permission"), 1));
                SELECT setval(pg_get_serial_sequence('"ApplicationRole"', 'Id'), COALESCE((SELECT MAX("Id") FROM "ApplicationRole"), 1));
                SELECT setval(pg_get_serial_sequence('"StaffTypeLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "StaffTypeLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"PersonTitleLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "PersonTitleLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"DesignationLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "DesignationLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"DepartmentRoleLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "DepartmentRoleLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"CollegeRoleLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "CollegeRoleLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"QualificationLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "QualificationLookup"), 1));
                SELECT setval(pg_get_serial_sequence('"EmploymentStatusLookup"', 'Id'), COALESCE((SELECT MAX("Id") FROM "EmploymentStatusLookup"), 1));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_User_StaffId",
                table: "User",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRole_TenantId_Code",
                table: "ApplicationRole",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRolePermission_PermissionId",
                table: "ApplicationRolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Department_CollegeId_Code",
                table: "Department",
                columns: new[] { "CollegeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Key",
                table: "Permission",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffCollegeRole_CollegeRoleLookupId",
                table: "StaffCollegeRole",
                column: "CollegeRoleLookupId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCollegeRole_StaffId_CollegeRoleLookupId",
                table: "StaffCollegeRole",
                columns: new[] { "StaffId", "CollegeRoleLookupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffDepartment_DepartmentId",
                table: "StaffDepartment",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDepartment_StaffId_DepartmentId",
                table: "StaffDepartment",
                columns: new[] { "StaffId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffDepartmentRole_DepartmentRoleLookupId",
                table: "StaffDepartmentRole",
                column: "DepartmentRoleLookupId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDepartmentRole_StaffDepartmentId_DepartmentRoleLookupId",
                table: "StaffDepartmentRole",
                columns: new[] { "StaffDepartmentId", "DepartmentRoleLookupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_CollegeId_StaffCode",
                table: "StaffMembers",
                columns: new[] { "CollegeId", "StaffCode" },
                unique: true,
                filter: "\"StaffCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_DesignationId",
                table: "StaffMembers",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_EmploymentStatusId",
                table: "StaffMembers",
                column: "EmploymentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_GenderId",
                table: "StaffMembers",
                column: "GenderId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_PersonTitleId",
                table: "StaffMembers",
                column: "PersonTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_QualificationId",
                table: "StaffMembers",
                column: "QualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_StaffTypeId",
                table: "StaffMembers",
                column: "StaffTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffSubjectAssignment_StaffId_SubjectId",
                table: "StaffSubjectAssignment",
                columns: new[] { "StaffId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffSubjectAssignment_SubjectId",
                table: "StaffSubjectAssignment",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApplicationRole_ApplicationRoleId",
                table: "UserApplicationRole",
                column: "ApplicationRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_User_StaffMembers_StaffId",
                table: "User",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_StaffMembers_StaffId",
                table: "User");

            migrationBuilder.DropTable(
                name: "ApplicationRolePermission");

            migrationBuilder.DropTable(
                name: "StaffCollegeRole");

            migrationBuilder.DropTable(
                name: "StaffDepartmentRole");

            migrationBuilder.DropTable(
                name: "StaffSubjectAssignment");

            migrationBuilder.DropTable(
                name: "UserApplicationRole");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "CollegeRoleLookup");

            migrationBuilder.DropTable(
                name: "DepartmentRoleLookup");

            migrationBuilder.DropTable(
                name: "StaffDepartment");

            migrationBuilder.DropTable(
                name: "ApplicationRole");

            migrationBuilder.DropTable(
                name: "Department");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "DesignationLookup");

            migrationBuilder.DropTable(
                name: "EmploymentStatusLookup");

            migrationBuilder.DropTable(
                name: "PersonTitleLookup");

            migrationBuilder.DropTable(
                name: "QualificationLookup");

            migrationBuilder.DropTable(
                name: "StaffTypeLookup");

            migrationBuilder.DropIndex(
                name: "IX_User_StaffId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "User");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 26, 16, 4, 53, 424, DateTimeKind.Utc).AddTicks(6276));
        }
    }
}
