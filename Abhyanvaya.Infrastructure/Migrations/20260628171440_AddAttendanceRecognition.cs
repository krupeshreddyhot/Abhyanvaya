using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRecognition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    FaceNumber = table.Column<int>(type: "integer", nullable: false),
                    RecognitionStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    EmbeddingDistance = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    BoundingBoxX = table.Column<int>(type: "integer", nullable: false),
                    BoundingBoxY = table.Column<int>(type: "integer", nullable: false),
                    BoundingBoxWidth = table.Column<int>(type: "integer", nullable: false),
                    BoundingBoxHeight = table.Column<int>(type: "integer", nullable: false),
                    RecognitionTimeMilliseconds = table.Column<int>(type: "integer", nullable: true),
                    VerifiedByTeacher = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TeacherOverride = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecognition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecognition_AttendanceSession_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalTable: "AttendanceSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecognition_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 17, 14, 39, 169, DateTimeKind.Utc).AddTicks(9791));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_AttendanceSessionId",
                table: "AttendanceRecognition",
                column: "AttendanceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_Session_FaceNumber",
                table: "AttendanceRecognition",
                columns: new[] { "AttendanceSessionId", "FaceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecognition_StudentId",
                table: "AttendanceRecognition",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecognition");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 16, 15, 25, 839, DateTimeKind.Utc).AddTicks(6039));
        }
    }
}
