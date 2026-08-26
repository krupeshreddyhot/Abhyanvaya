using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-TG.3 Prompt 2 — TeachingGroup / TeachingGroupSection / TeachingGroupMembership tables.
/// Hand-authored focused migration (ModelSnapshot drift would otherwise emit unrelated AI29/AI31 diffs).
/// NOT applied in Prompt 2 — inspection only.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260817153000_AI_SCHED_TG_3_TeachingGroup")]
public partial class AI_SCHED_TG_3_TeachingGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SchedulingTeachingGroup",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<int>(type: "integer", nullable: false),
                SubjectAllocationId = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                MembershipSource = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                ActivityKind = table.Column<byte>(type: "smallint", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                ExpectedStudentCount = table.Column<int>(type: "integer", nullable: true),
                MaxTeachingCapacity = table.Column<int>(type: "integer", nullable: true),
                ExclusionGroupKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                table.PrimaryKey("PK_SchedulingTeachingGroup", x => x.Id);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_SchedulingSubjectAllocation_SubjectAllocationId",
                    column: x => x.SubjectAllocationId,
                    principalTable: "SchedulingSubjectAllocation",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_SchedulingAcademicYear_AcademicYearId",
                    column: x => x.AcademicYearId,
                    principalTable: "SchedulingAcademicYear",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_Course_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Course",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_Semester_SemesterId",
                    column: x => x.SemesterId,
                    principalTable: "Semester",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroup_Subject_SubjectId",
                    column: x => x.SubjectId,
                    principalTable: "Subject",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SchedulingTeachingGroupSection",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TeachingGroupId = table.Column<int>(type: "integer", nullable: false),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SchedulingTeachingGroupSection", x => x.Id);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroupSection_SchedulingTeachingGroup_TeachingGroupId",
                    column: x => x.TeachingGroupId,
                    principalTable: "SchedulingTeachingGroup",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroupSection_Sections_SectionId",
                    column: x => x.SectionId,
                    principalTable: "Sections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SchedulingTeachingGroupMembership",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TeachingGroupId = table.Column<int>(type: "integer", nullable: false),
                StudentId = table.Column<int>(type: "integer", nullable: false),
                Inclusion = table.Column<byte>(type: "smallint", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SchedulingTeachingGroupMembership", x => x.Id);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroupMembership_SchedulingTeachingGroup_TeachingGroupId",
                    column: x => x.TeachingGroupId,
                    principalTable: "SchedulingTeachingGroup",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SchedulingTeachingGroupMembership_Student_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Student",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_TenantId_SubjectAllocationId",
            table: "SchedulingTeachingGroup",
            columns: new[] { "TenantId", "SubjectAllocationId" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_TenantId_SubjectAllocationId_ExclusionGroupKey",
            table: "SchedulingTeachingGroup",
            columns: new[] { "TenantId", "SubjectAllocationId", "ExclusionGroupKey" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_TenantId_Status",
            table: "SchedulingTeachingGroup",
            columns: new[] { "TenantId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_SubjectAllocationId",
            table: "SchedulingTeachingGroup",
            column: "SubjectAllocationId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_AcademicYearId",
            table: "SchedulingTeachingGroup",
            column: "AcademicYearId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_CourseId",
            table: "SchedulingTeachingGroup",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_GroupId",
            table: "SchedulingTeachingGroup",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_SemesterId",
            table: "SchedulingTeachingGroup",
            column: "SemesterId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroup_SubjectId",
            table: "SchedulingTeachingGroup",
            column: "SubjectId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupSection_TenantId_TeachingGroupId_SectionId",
            table: "SchedulingTeachingGroupSection",
            columns: new[] { "TenantId", "TeachingGroupId", "SectionId" },
            unique: true,
            filter: "\"IsDeleted\" = FALSE");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupSection_TenantId_SectionId",
            table: "SchedulingTeachingGroupSection",
            columns: new[] { "TenantId", "SectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupSection_TeachingGroupId",
            table: "SchedulingTeachingGroupSection",
            column: "TeachingGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupSection_SectionId",
            table: "SchedulingTeachingGroupSection",
            column: "SectionId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId",
            table: "SchedulingTeachingGroupMembership",
            columns: new[] { "TenantId", "TeachingGroupId", "StudentId" },
            unique: true,
            filter: "\"IsCurrent\" = TRUE AND \"IsDeleted\" = FALSE");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId",
            table: "SchedulingTeachingGroupMembership",
            columns: new[] { "TenantId", "TeachingGroupId" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupMembership_TenantId_StudentId",
            table: "SchedulingTeachingGroupMembership",
            columns: new[] { "TenantId", "StudentId" });

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupMembership_TeachingGroupId",
            table: "SchedulingTeachingGroupMembership",
            column: "TeachingGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_SchedulingTeachingGroupMembership_StudentId",
            table: "SchedulingTeachingGroupMembership",
            column: "StudentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SchedulingTeachingGroupMembership");
        migrationBuilder.DropTable(name: "SchedulingTeachingGroupSection");
        migrationBuilder.DropTable(name: "SchedulingTeachingGroup");
    }
}
