using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase2B7_OptimizationSandbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    SemesterId = table.Column<int>(type: "integer", nullable: true),
                    TimetableId = table.Column<int>(type: "integer", nullable: true),
                    SourceSimulationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    TagsCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrentScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProjectedScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ConflictCount = table.Column<int>(type: "integer", nullable: false),
                    ReplayCount = table.Column<int>(type: "integer", nullable: false),
                    ComparisonCount = table.Column<int>(type: "integer", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplayedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastComparedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsImmutable = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioApprovalRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioApprovalRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioBookmark",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioBookmark", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioComment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CommentText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioComment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioFavorite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioFavorite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<byte>(type: "smallint", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioNote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    NoteText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioNote", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationScenarioShare",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    SharedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SharedWithUserId = table.Column<int>(type: "integer", nullable: false),
                    ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SharedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationScenarioShare", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingOptimizationSnapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptimizationScenarioId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimetableSummaryJson = table.Column<string>(type: "text", nullable: false),
                    SimulationJson = table.Column<string>(type: "text", nullable: false),
                    ScoresJson = table.Column<string>(type: "text", nullable: false),
                    ConflictSummaryJson = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "text", nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsImmutable = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingOptimizationSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingOptimizationSnapshot_SchedulingOptimizationScenar~",
                        column: x => x.OptimizationScenarioId,
                        principalTable: "SchedulingOptimizationScenario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 11, 30, 25, 183, DateTimeKind.Utc).AddTicks(2585));

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenario_TenantId_AcademicYearId_Stat~",
                table: "SchedulingOptimizationScenario",
                columns: new[] { "TenantId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenario_TenantId_OwnerUserId_IsFavor~",
                table: "SchedulingOptimizationScenario",
                columns: new[] { "TenantId", "OwnerUserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenario_TenantId_ScenarioId",
                table: "SchedulingOptimizationScenario",
                columns: new[] { "TenantId", "ScenarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenarioFavorite_TenantId_UserId_Opti~",
                table: "SchedulingOptimizationScenarioFavorite",
                columns: new[] { "TenantId", "UserId", "OptimizationScenarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenarioHistory_TenantId_Optimization~",
                table: "SchedulingOptimizationScenarioHistory",
                columns: new[] { "TenantId", "OptimizationScenarioId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationScenarioShare_TenantId_OptimizationSc~",
                table: "SchedulingOptimizationScenarioShare",
                columns: new[] { "TenantId", "OptimizationScenarioId", "SharedWithUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationSnapshot_OptimizationScenarioId_Seque~",
                table: "SchedulingOptimizationSnapshot",
                columns: new[] { "OptimizationScenarioId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingOptimizationSnapshot_TenantId_SnapshotId",
                table: "SchedulingOptimizationSnapshot",
                columns: new[] { "TenantId", "SnapshotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioApprovalRequest");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioBookmark");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioComment");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioFavorite");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioHistory");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioNote");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenarioShare");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationSnapshot");

            migrationBuilder.DropTable(
                name: "SchedulingOptimizationScenario");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 2, 9, 53, 0, 300, DateTimeKind.Utc).AddTicks(8555));
        }
    }
}
