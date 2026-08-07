using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI29 consolidated baseline — replaces script-only delivery for:
/// Apply_AI29_SectionSchema.sql,
/// Apply_AI29_1A_ProgramSchema.sql,
/// Apply_AI29_1A5_EnterpriseHardening.sql,
/// Apply_AI29_1A6_PerformanceGuard.sql.
///
/// Existing databases that already ran those scripts should mark this migration
/// applied without running Up() (see scripts/MarkApplied_AI29_AcademicHierarchyBaseline.sql).
/// </summary>
public partial class AI29_AcademicHierarchyBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- AI29 Sections ---
        migrationBuilder.CreateTable(
            name: "Sections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                SectionCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                MaximumStrength = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_Sections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Sections_Tenant_Scope_Code",
            table: "Sections",
            columns: new[] { "TenantId", "AcademicYearId", "CourseId", "GroupId", "SemesterId", "SectionCode" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "StudentSections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StudentId = table.Column<int>(type: "integer", nullable: false),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                TransferReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_StudentSections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_StudentSections_Student_Current",
            table: "StudentSections",
            columns: new[] { "TenantId", "StudentId", "IsCurrent" },
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "FacultySectionAssignments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FacultyId = table.Column<int>(type: "integer", nullable: false),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Primary"),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_FacultySectionAssignments", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_FacultySectionAssignments_Faculty",
            table: "FacultySectionAssignments",
            columns: new[] { "TenantId", "FacultyId", "IsCurrent" },
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "TimetableSections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TimetableId = table.Column<int>(type: "integer", nullable: false),
                TimetableEntryId = table.Column<int>(type: "integer", nullable: true),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_TimetableSections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_TimetableSections_Entry_Section",
            table: "TimetableSections",
            columns: new[] { "TenantId", "TimetableId", "TimetableEntryId", "SectionId" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "AttendanceSessionSections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_AttendanceSessionSections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSessionSections_Session",
            table: "AttendanceSessionSections",
            columns: new[] { "TenantId", "AttendanceSessionId" },
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "SectionAllocationPreferences",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                Strategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Alphabetical"),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionAllocationPreferences", x => x.Id));

        // --- AI29.1A Programs ---
        migrationBuilder.CreateTable(
            name: "Programs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                ProgramCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProgramName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ThemeColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                AcademicCalendarId = table.Column<int>(type: "integer", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_Programs", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Programs_Tenant_Code",
            table: "Programs",
            columns: new[] { "TenantId", "ProgramCode" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateTable(
            name: "TenantAcademicConfigurations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                EnablePrograms = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_TenantAcademicConfigurations", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_TenantAcademicConfigurations_Tenant",
            table: "TenantAcademicConfigurations",
            column: "TenantId",
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.AddColumn<int>(
            name: "ProgramId",
            table: "Course",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Course_ProgramId",
            table: "Course",
            column: "ProgramId");

        // --- AI29.1A.5 DisplayOrder + ProgramPolicy ---
        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            table: "Course",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            table: "Group",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            table: "Semester",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            table: "Subject",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "ProgramPolicies",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ProgramId = table.Column<int>(type: "integer", nullable: false),
                MinimumAttendancePercent = table.Column<decimal>(type: "numeric", nullable: true),
                CreditsRequired = table.Column<decimal>(type: "numeric", nullable: true),
                PassMarks = table.Column<decimal>(type: "numeric", nullable: true),
                MaximumBacklogs = table.Column<int>(type: "integer", nullable: true),
                MaximumSubjects = table.Column<int>(type: "integer", nullable: true),
                AcademicRules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_ProgramPolicies", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ProgramPolicies_Tenant_Program",
            table: "ProgramPolicies",
            columns: new[] { "TenantId", "ProgramId" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        // --- AI29.1A.6 Snapshots ---
        migrationBuilder.CreateTable(
            name: "AcademicHierarchySnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                Programs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Courses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Groups = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Semesters = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Sections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Subjects = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                HierarchyJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                GeneratedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_AcademicHierarchySnapshots", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AcademicHierarchySnapshots_Tenant_Date",
            table: "AcademicHierarchySnapshots",
            columns: new[] { "TenantId", "SnapshotDate" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        // Permission seeds (idempotent) — Section.* + Program.*
        migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Permission') THEN
                IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Section.View') THEN
                  INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
                    (210, 'Section.View', 'Section', 'View'),
                    (211, 'Section.Create', 'Section', 'Create'),
                    (212, 'Section.Edit', 'Section', 'Edit'),
                    (213, 'Section.Delete', 'Section', 'Delete'),
                    (214, 'Section.AssignStudents', 'Section', 'AssignStudents'),
                    (215, 'Section.AssignFaculty', 'Section', 'AssignFaculty');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "Permission" WHERE "Key" = 'Program.View') THEN
                  INSERT INTO "Permission" ("Id", "Key", "Resource", "Action") VALUES
                    (220, 'Program.View', 'Program', 'View'),
                    (221, 'Program.Create', 'Program', 'Create'),
                    (222, 'Program.Edit', 'Program', 'Edit'),
                    (223, 'Program.Delete', 'Program', 'Delete'),
                    (224, 'Program.Manage', 'Program', 'Manage');
                END IF;

                IF EXISTS (SELECT 1 FROM "ApplicationRole" WHERE "Id" = 100) THEN
                  INSERT INTO "ApplicationRolePermission" ("ApplicationRoleId", "PermissionId")
                  SELECT 100, p."Id"
                  FROM "Permission" p
                  WHERE p."Id" BETWEEN 210 AND 224
                    AND NOT EXISTS (
                      SELECT 1 FROM "ApplicationRolePermission" arp
                      WHERE arp."ApplicationRoleId" = 100 AND arp."PermissionId" = p."Id"
                    );
                END IF;
              END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AcademicHierarchySnapshots");
        migrationBuilder.DropTable(name: "ProgramPolicies");
        migrationBuilder.DropTable(name: "TenantAcademicConfigurations");
        migrationBuilder.DropTable(name: "Programs");
        migrationBuilder.DropTable(name: "SectionAllocationPreferences");
        migrationBuilder.DropTable(name: "AttendanceSessionSections");
        migrationBuilder.DropTable(name: "TimetableSections");
        migrationBuilder.DropTable(name: "FacultySectionAssignments");
        migrationBuilder.DropTable(name: "StudentSections");
        migrationBuilder.DropTable(name: "Sections");

        migrationBuilder.DropIndex(name: "IX_Course_ProgramId", table: "Course");
        migrationBuilder.DropColumn(name: "ProgramId", table: "Course");
        migrationBuilder.DropColumn(name: "DisplayOrder", table: "Course");
        migrationBuilder.DropColumn(name: "DisplayOrder", table: "Group");
        migrationBuilder.DropColumn(name: "DisplayOrder", table: "Semester");
        migrationBuilder.DropColumn(name: "DisplayOrder", table: "Subject");
    }
}
