using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentBatchSnapshotColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationSnapshotJson",
                table: "StudentEnrollmentBatch",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "StudentEnrollmentBatch",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PhotoProviderName",
                table: "StudentEnrollmentBatch",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "ExamBranch");

            migrationBuilder.AddColumn<int>(
                name: "PipelineVersion",
                table: "StudentEnrollmentBatch",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "StudentEnrollmentBatch",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigurationSnapshotJson",
                table: "StudentEnrollmentBatch");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "StudentEnrollmentBatch");

            migrationBuilder.DropColumn(
                name: "PhotoProviderName",
                table: "StudentEnrollmentBatch");

            migrationBuilder.DropColumn(
                name: "PipelineVersion",
                table: "StudentEnrollmentBatch");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "StudentEnrollmentBatch");
        }
    }
}
