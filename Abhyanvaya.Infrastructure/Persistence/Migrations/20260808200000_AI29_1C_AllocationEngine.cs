using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI29.1C — Allocation engine sessions / scenarios / drafts / sandbox.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808200000_AI29_1C_AllocationEngine")]
public partial class AI29_1C_AllocationEngine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AllocationEngineSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                ContextChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                GroupingMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ActiveScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                ConfigJson = table.Column<string>(type: "text", nullable: false),
                TraceJson = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationEngineSessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AllocationEngineScenarios",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                ContextChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TotalScore = table.Column<double>(type: "double precision", nullable: false),
                ScenarioJson = table.Column<string>(type: "text", nullable: false),
                GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationEngineScenarios", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AllocationEngineDrafts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                DraftJson = table.Column<string>(type: "text", nullable: false),
                Note = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationEngineDrafts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AllocationEngineSandboxItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SandboxId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                Tags = table.Column<string>(type: "text", nullable: true),
                ScenarioJson = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationEngineSandboxItems", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AllocationEngineSessions_Tenant_SessionId",
            table: "AllocationEngineSessions",
            columns: new[] { "TenantId", "SessionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllocationEngineScenarios_Tenant_ScenarioId",
            table: "AllocationEngineScenarios",
            columns: new[] { "TenantId", "ScenarioId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllocationEngineScenarios_Tenant_SessionId",
            table: "AllocationEngineScenarios",
            columns: new[] { "TenantId", "SessionId" });

        migrationBuilder.CreateIndex(
            name: "IX_AllocationEngineDrafts_Tenant_DraftId",
            table: "AllocationEngineDrafts",
            columns: new[] { "TenantId", "DraftId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllocationEngineSandboxItems_Tenant_SandboxId",
            table: "AllocationEngineSandboxItems",
            columns: new[] { "TenantId", "SandboxId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AllocationEngineSandboxItems");
        migrationBuilder.DropTable(name: "AllocationEngineDrafts");
        migrationBuilder.DropTable(name: "AllocationEngineScenarios");
        migrationBuilder.DropTable(name: "AllocationEngineSessions");
    }
}
