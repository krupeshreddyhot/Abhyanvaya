using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecognitionReviewHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRecognitionReviewHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecognitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    OldStudentId = table.Column<int>(type: "integer", nullable: true),
                    NewStudentId = table.Column<int>(type: "integer", nullable: true),
                    ReviewAction = table.Column<int>(type: "integer", nullable: false),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedBy = table.Column<int>(type: "integer", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecognitionReviewHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecognitionReviewHistory_AttendanceRecognition_Re~",
                        column: x => x.RecognitionId,
                        principalTable: "AttendanceRecognition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecognitionReviewHistory_User_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 16, 19, 12, 713, DateTimeKind.Utc).AddTicks(6249));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognitionReviewHistory_Recognition_ReviewedUtc",
                table: "AttendanceRecognitionReviewHistory",
                columns: new[] { "RecognitionId", "ReviewedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognitionReviewHistory_RecognitionId",
                table: "AttendanceRecognitionReviewHistory",
                column: "RecognitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognitionReviewHistory_ReviewedBy",
                table: "AttendanceRecognitionReviewHistory",
                column: "ReviewedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecognitionReviewHistory");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 1, 15, 59, 3, 576, DateTimeKind.Utc).AddTicks(5182));
        }
    }
}
