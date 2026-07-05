using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceStudentFaceEmbeddingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimension",
                table: "StudentFaceEmbedding",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingStatus",
                table: "StudentFaceEmbedding",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastFailureReason",
                table: "StudentFaceEmbedding",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailureUtc",
                table: "StudentFaceEmbedding",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PhotoVersion",
                table: "StudentFaceEmbedding",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "StudentFaceEmbedding",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Migrate legacy EmbeddingQuality values (Pending=1, Low=2, Medium=3, High=4, Failed=5)
            // into separate lifecycle status and quality enums.
            migrationBuilder.Sql("""
                UPDATE "StudentFaceEmbedding"
                SET "EmbeddingStatus" = CASE
                    WHEN "EmbeddingQuality" = 1 OR "EmbeddingModel" = 'Pending' THEN 0
                    WHEN "EmbeddingQuality" = 5 THEN 3
                    WHEN "EmbeddingQuality" IN (2, 3, 4) THEN 2
                    ELSE 0
                END;

                UPDATE "StudentFaceEmbedding"
                SET "EmbeddingQuality" = CASE
                    WHEN "EmbeddingQuality" = 2 THEN 1
                    WHEN "EmbeddingQuality" = 3 THEN 2
                    WHEN "EmbeddingQuality" = 4 THEN 3
                    ELSE 0
                END;

                UPDATE "StudentFaceEmbedding"
                SET "EmbeddingStatus" = 4
                WHERE "IsActive" = false AND "EmbeddingStatus" = 2;

                UPDATE "StudentFaceEmbedding"
                SET "EmbeddingDimension" = COALESCE(array_length("EmbeddingVector", 1), 0);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "EmbeddingQuality",
                table: "StudentFaceEmbedding",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 17, 7, 37, 802, DateTimeKind.Utc).AddTicks(3725));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "StudentFaceEmbedding"
                SET "EmbeddingQuality" = CASE
                    WHEN "EmbeddingStatus" = 3 THEN 5
                    WHEN "EmbeddingQuality" = 1 THEN 2
                    WHEN "EmbeddingQuality" = 2 THEN 3
                    WHEN "EmbeddingQuality" = 3 THEN 4
                    WHEN "EmbeddingStatus" = 0 THEN 1
                    ELSE 0
                END;
                """);

            migrationBuilder.DropColumn(
                name: "EmbeddingDimension",
                table: "StudentFaceEmbedding");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "StudentFaceEmbedding");

            migrationBuilder.DropColumn(
                name: "LastFailureReason",
                table: "StudentFaceEmbedding");

            migrationBuilder.DropColumn(
                name: "LastFailureUtc",
                table: "StudentFaceEmbedding");

            migrationBuilder.DropColumn(
                name: "PhotoVersion",
                table: "StudentFaceEmbedding");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "StudentFaceEmbedding");

            migrationBuilder.AlterColumn<int>(
                name: "EmbeddingQuality",
                table: "StudentFaceEmbedding",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 2, 16, 42, 8, 561, DateTimeKind.Utc).AddTicks(4799));
        }
    }
}
