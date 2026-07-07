using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    SemesterId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnnotatedImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RecognitionProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalStudents = table.Column<int>(type: "integer", nullable: false),
                    DetectedFaces = table.Column<int>(type: "integer", nullable: false),
                    RecognizedFaces = table.Column<int>(type: "integer", nullable: false),
                    UnknownFaces = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Semester_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 14, 26, 44, 937, DateTimeKind.Utc).AddTicks(9552));

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_CourseId",
                table: "AttendanceSession",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_GroupId",
                table: "AttendanceSession",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_SemesterId",
                table: "AttendanceSession",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_SubjectId",
                table: "AttendanceSession",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Status",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_Tenant_Subject_Date",
                table: "AttendanceSession",
                columns: new[] { "TenantId", "SubjectId", "AttendanceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSession");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 28, 11, 39, 47, 828, DateTimeKind.Utc).AddTicks(5152));
        }
    }
}
