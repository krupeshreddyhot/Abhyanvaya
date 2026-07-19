using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelLifecycleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiModelDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ModelType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoldenDatasetDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SamplesJson = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsImmutable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldenDatasetDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelLifecycleAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ToVersion = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelLifecycleAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetrainingCandidateEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrectionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecognitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetrainingCandidateEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecognitionVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PipelineVersion = table.Column<int>(type: "integer", nullable: false),
                    TrainingDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DatasetVersion = table.Column<string>(type: "text", nullable: true),
                    Accuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiModelVersions_AiModelDefinitions_ModelDefinitionId",
                        column: x => x.ModelDefinitionId,
                        principalTable: "AiModelDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelRolloutPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RolloutKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyType = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric", nullable: true),
                    IsCanary = table.Column<bool>(type: "boolean", nullable: false),
                    TargetState = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelRolloutPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelRolloutPlans_AiModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "AiModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 20, 33, 50, 584, DateTimeKind.Utc).AddTicks(5110));

            migrationBuilder.CreateIndex(
                name: "IX_AiModelDefinitions_ModelKey",
                table: "AiModelDefinitions",
                column: "ModelKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModelVersions_IsActive",
                table: "AiModelVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelVersions_ModelDefinitionId_Version",
                table: "AiModelVersions",
                columns: new[] { "ModelDefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldenDatasetDefinitions_DatasetKey_Version",
                table: "GoldenDatasetDefinitions",
                columns: new[] { "DatasetKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelRolloutPlans_ModelVersionId",
                table: "ModelRolloutPlans",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RetrainingCandidateEntries_TenantId",
                table: "RetrainingCandidateEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoldenDatasetDefinitions");

            migrationBuilder.DropTable(
                name: "ModelLifecycleAuditEntries");

            migrationBuilder.DropTable(
                name: "ModelRolloutPlans");

            migrationBuilder.DropTable(
                name: "RetrainingCandidateEntries");

            migrationBuilder.DropTable(
                name: "AiModelVersions");

            migrationBuilder.DropTable(
                name: "AiModelDefinitions");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 19, 19, 13, 468, DateTimeKind.Utc).AddTicks(5034));
        }
    }
}
