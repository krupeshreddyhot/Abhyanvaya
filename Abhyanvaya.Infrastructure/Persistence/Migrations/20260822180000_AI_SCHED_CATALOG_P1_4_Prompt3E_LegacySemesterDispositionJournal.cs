using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
/// Disposition journal table only. Does not apply Semester GroupId nullability or uniqueness constraints.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822180000_AI_SCHED_CATALOG_P1_4_Prompt3E_LegacySemesterDispositionJournal")]
public partial class AI_SCHED_CATALOG_P1_4_Prompt3E_LegacySemesterDispositionJournal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LegacySemesterDispositionJournals",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                DispositionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                PromptCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AssignedGroupId = table.Column<int>(type: "integer", nullable: true),
                SemesterRowMutated = table.Column<bool>(type: "boolean", nullable: false),
                FinalizedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LegacySemesterDispositionJournals", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LegacySemesterDispositionJournals_TenantId_SemesterId_DispositionCode",
            table: "LegacySemesterDispositionJournals",
            columns: new[] { "TenantId", "SemesterId", "DispositionCode" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LegacySemesterDispositionJournals");
    }
}
