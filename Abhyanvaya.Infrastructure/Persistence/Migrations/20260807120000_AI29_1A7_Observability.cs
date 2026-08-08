using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI29.1A.7 — Architecture trend persistence for observability.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807120000_AI29_1A7_Observability")]
public partial class AI29_1A7_Observability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AcademicArchitectureTrends",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                DependencyViolations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                ForbiddenReferences = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LayerViolations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_AcademicArchitectureTrends", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AcademicArchitectureTrends_Tenant_Recorded",
            table: "AcademicArchitectureTrends",
            columns: new[] { "TenantId", "RecordedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AcademicArchitectureTrends");
    }
}
