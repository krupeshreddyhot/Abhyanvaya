using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2B6_OptimizationReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationMetricSnapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    MetricKind = table.Column<byte>(type: "smallint", nullable: false),
                    MetricName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationMetricSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationSimulationRun",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    StrategyKind = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ScenarioName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CurrentScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProjectedScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ScoreDelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentConflictCount = table.Column<int>(type: "integer", nullable: false),
                    ProjectedConflictCount = table.Column<int>(type: "integer", nullable: false),
                    ScoringTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    ProposedChangesJson = table.Column<string>(type: "text", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationSimulationRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationTelemetryAggregate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetricKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CounterValue = table.Column<long>(type: "bigint", nullable: false),
                    AverageValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationTelemetryAggregate", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 9, 53, 0, 300, DateTimeKind.Utc).AddTicks(8555));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationMetricSnapshot_SnapshotId",
                table: "SchedulingOptimizationMetricSnapshot",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationMetricSnapshot_TenantId_AcademicYearI~",
                table: "SchedulingOptimizationMetricSnapshot",
                columns: new[] { "TenantId", "AcademicYearId", "CapturedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationSimulationRun_TenantId_AcademicYearId~",
                table: "SchedulingOptimizationSimulationRun",
                columns: new[] { "TenantId", "AcademicYearId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationSimulationRun_TenantId_SimulationId",
                table: "SchedulingOptimizationSimulationRun",
                columns: new[] { "TenantId", "SimulationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationTelemetryAggregate_TenantId_MetricKey",
                table: "SchedulingOptimizationTelemetryAggregate",
                columns: new[] { "TenantId", "MetricKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingOptimizationMetricSnapshot");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationSimulationRun");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationTelemetryAggregate");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 9, 22, 54, 11, DateTimeKind.Utc).AddTicks(7357));
        }
    }
}
