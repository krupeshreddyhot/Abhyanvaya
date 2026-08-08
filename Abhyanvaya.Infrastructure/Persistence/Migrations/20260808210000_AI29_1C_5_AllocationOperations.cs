using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808210000_AI29_1C_5_AllocationOperations")]
public partial class AI29_1C_5_AllocationOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "CurrentVersionNumber", table: "AllocationEngineScenarios", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>(name: "LifecycleStatus", table: "AllocationEngineScenarios", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Generated");
        migrationBuilder.AddColumn<string>(name: "ContextVersion", table: "AllocationEngineScenarios", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "1");
        migrationBuilder.AddColumn<string>(name: "StrategyConfigurationVersion", table: "AllocationEngineScenarios", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "1");
        migrationBuilder.AddColumn<string>(name: "ConstraintConfigurationVersion", table: "AllocationEngineScenarios", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "1");
        migrationBuilder.AddColumn<string>(name: "ScenarioChecksum", table: "AllocationEngineScenarios", type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "AcademicYearId", table: "AllocationEngineScenarios", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "CourseId", table: "AllocationEngineScenarios", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "GroupId", table: "AllocationEngineScenarios", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "SemesterId", table: "AllocationEngineScenarios", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>(name: "ParentScenarioId", table: "AllocationEngineScenarios", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReviewNotes", table: "AllocationEngineScenarios", type: "text", nullable: true);

        migrationBuilder.CreateTable(
            name: "AllocationScenarioVersions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                ContextVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ContextChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                StrategyConfigurationVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ConstraintConfigurationVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Score = table.Column<double>(type: "double precision", nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ScenarioJson = table.Column<string>(type: "text", nullable: false),
                ConfigJson = table.Column<string>(type: "text", nullable: false),
                TraceJson = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationScenarioVersions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AllocationAuditEntries",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                VersionNumber = table.Column<int>(type: "integer", nullable: true),
                ContextVersion = table.Column<string>(type: "text", nullable: true),
                Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Detail = table.Column<string>(type: "text", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ActorUserId = table.Column<int>(type: "integer", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AllocationAuditEntries", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AllocationScenarioVersions_Tenant_Scenario_Version",
            table: "AllocationScenarioVersions",
            columns: new[] { "TenantId", "ScenarioId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllocationAuditEntries_Tenant_AuditId",
            table: "AllocationAuditEntries",
            columns: new[] { "TenantId", "AuditId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllocationAuditEntries_Tenant_OccurredAt",
            table: "AllocationAuditEntries",
            columns: new[] { "TenantId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AllocationAuditEntries");
        migrationBuilder.DropTable(name: "AllocationScenarioVersions");
        migrationBuilder.DropColumn(name: "CurrentVersionNumber", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "LifecycleStatus", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "ContextVersion", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "StrategyConfigurationVersion", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "ConstraintConfigurationVersion", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "ScenarioChecksum", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "AcademicYearId", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "CourseId", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "GroupId", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "SemesterId", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "ParentScenarioId", table: "AllocationEngineScenarios");
        migrationBuilder.DropColumn(name: "ReviewNotes", table: "AllocationEngineScenarios");
    }
}
