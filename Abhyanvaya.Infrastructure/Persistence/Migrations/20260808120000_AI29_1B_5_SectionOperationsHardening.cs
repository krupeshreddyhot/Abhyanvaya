using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI29.1B.5 — Section versioning, capacity history, hierarchical policies (EF-only).</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808120000_AI29_1B_5_SectionOperationsHardening")]
public partial class AI29_1B_5_SectionOperationsHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SectionVersions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                VersionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ChangedBy = table.Column<int>(type: "integer", nullable: true),
                Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PreviousVersionId = table.Column<int>(type: "integer", nullable: true),
                SectionCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SectionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                MaximumCapacity = table.Column<int>(type: "integer", nullable: false),
                MinimumCapacity = table.Column<int>(type: "integer", nullable: false),
                RecommendedCapacity = table.Column<int>(type: "integer", nullable: false),
                ReservedSeats = table.Column<int>(type: "integer", nullable: false),
                WaitingListCount = table.Column<int>(type: "integer", nullable: false),
                CurrentStrength = table.Column<int>(type: "integer", nullable: false),
                OccupancyPercent = table.Column<double>(type: "double precision", nullable: false),
                ParentSectionId = table.Column<int>(type: "integer", nullable: true),
                SectionGroupId = table.Column<int>(type: "integer", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionVersions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionCapacityHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                MaximumCapacity = table.Column<int>(type: "integer", nullable: false),
                MinimumCapacity = table.Column<int>(type: "integer", nullable: false),
                CurrentStrength = table.Column<int>(type: "integer", nullable: false),
                ReservedSeats = table.Column<int>(type: "integer", nullable: false),
                OccupancyPercent = table.Column<double>(type: "double precision", nullable: false),
                RecordedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                RecordedBy = table.Column<int>(type: "integer", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionCapacityHistories", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionPolicies",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                ScopeLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProgramId = table.Column<int>(type: "integer", nullable: true),
                CourseId = table.Column<int>(type: "integer", nullable: true),
                SectionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                MaximumCapacity = table.Column<int>(type: "integer", nullable: true),
                MinimumCapacity = table.Column<int>(type: "integer", nullable: true),
                RecommendedCapacity = table.Column<int>(type: "integer", nullable: true),
                MaximumCombinedSections = table.Column<int>(type: "integer", nullable: true),
                MaximumFaculty = table.Column<int>(type: "integer", nullable: true),
                MaximumRoomOccupancy = table.Column<int>(type: "integer", nullable: true),
                AllowMerge = table.Column<bool>(type: "boolean", nullable: true),
                AllowSplit = table.Column<bool>(type: "boolean", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionPolicies", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SectionVersions_Tenant_Section_Version",
            table: "SectionVersions",
            columns: new[] { "TenantId", "SectionId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SectionCapacityHistories_Tenant_Section_Date",
            table: "SectionCapacityHistories",
            columns: new[] { "TenantId", "SectionId", "RecordedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_SectionPolicies_Tenant_Scope",
            table: "SectionPolicies",
            columns: new[] { "TenantId", "ScopeLevel", "ProgramId", "CourseId", "SectionTypeCode" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SectionVersions");
        migrationBuilder.DropTable(name: "SectionCapacityHistories");
        migrationBuilder.DropTable(name: "SectionPolicies");
    }
}
