using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantReviewHistoryRecognitionIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecognitionReviewHistory_RecognitionId",
                table: "AttendanceRecognitionReviewHistory");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 18, 53, 33, 476, DateTimeKind.Utc).AddTicks(7699));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 16, 51, 24, 469, DateTimeKind.Utc).AddTicks(9948));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognitionReviewHistory_RecognitionId",
                table: "AttendanceRecognitionReviewHistory",
                column: "RecognitionId");
        }
    }
}
