using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase3_EnterpriseOptimizationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationEngineRun",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    StrategyKind = table.Column<byte>(type: "smallint", nullable: false),
                    StrategyPipelineCsv = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    SourceScheduleVersionId = table.Column<int>(type: "integer", nullable: true),
                    SandboxScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultDraftScheduleVersionId = table.Column<int>(type: "integer", nullable: true),
                    BaselineScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProjectedScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImprovementDelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BaselineConflictCount = table.Column<int>(type: "integer", nullable: false),
                    ProjectedConflictCount = table.Column<int>(type: "integer", nullable: false),
                    CurrentStrategy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedRemainingMs = table.Column<long>(type: "bigint", nullable: true),
                    CandidatesJson = table.Column<string>(type: "text", nullable: false),
                    ComparisonJson = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    IntermediateResultsJson = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationEngineRun", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 18, 53, 40, 689, DateTimeKind.Utc).AddTicks(3836));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationEngineRun_TenantId_AcademicYearId_Sta~",
                table: "SchedulingOptimizationEngineRun",
                columns: new[] { "TenantId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationEngineRun_TenantId_RunId",
                table: "SchedulingOptimizationEngineRun",
                columns: new[] { "TenantId", "RunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationEngineRun_TenantId_StartedUtc",
                table: "SchedulingOptimizationEngineRun",
                columns: new[] { "TenantId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingOptimizationEngineRun");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 11, 30, 25, 183, DateTimeKind.Utc).AddTicks(2585));
        }
    }
}
