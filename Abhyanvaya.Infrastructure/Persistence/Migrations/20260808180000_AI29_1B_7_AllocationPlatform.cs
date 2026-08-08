using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI29.1B.7 — Allocation platform snapshots (EF-only).</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808180000_AI29_1B_7_AllocationPlatform")]
public partial class AI29_1B_7_AllocationPlatform : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SectionAllocationSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                ContextVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                GeneratedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                GeneratedBy = table.Column<int>(type: "integer", nullable: true),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                ContextJson = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionAllocationSnapshots", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SectionAllocationSnapshots_Tenant_SnapshotId",
            table: "SectionAllocationSnapshots",
            columns: new[] { "TenantId", "SnapshotId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SectionAllocationSnapshots_Tenant_Scope_Date",
            table: "SectionAllocationSnapshots",
            columns: new[] { "TenantId", "AcademicYearId", "CourseId", "GroupId", "SemesterId", "GeneratedDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SectionAllocationSnapshots");
    }
}
