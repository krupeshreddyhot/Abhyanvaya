using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEnrollmentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentEnrollmentBatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    CollegeId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYear = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalStudents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PendingCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DownloadingCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValidatingCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EmbeddingCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RetryRequiredCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CancellationRequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollmentBatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentEnrollmentBatch_College_CollegeId",
                        column: x => x.CollegeId,
                        principalTable: "College",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollmentBatch_University_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "University",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentEnrollmentItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailureCategory = table.Column<int>(type: "integer", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PhotoKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ByteSize = table.Column<int>(type: "integer", nullable: true),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ImageWidth = table.Column<int>(type: "integer", nullable: true),
                    ImageHeight = table.Column<int>(type: "integer", nullable: true),
                    EmbeddingVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QualityScore = table.Column<float>(type: "real", nullable: true),
                    StudentFaceEmbeddingId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    DownloadStartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DownloadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidationStartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmbeddingStartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollmentItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentEnrollmentItem_StudentEnrollmentBatch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StudentEnrollmentBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentEnrollmentItem_StudentFaceEmbedding_StudentFaceEmbed~",
                        column: x => x.StudentFaceEmbeddingId,
                        principalTable: "StudentFaceEmbedding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentEnrollmentItem_Student_StudentId",
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
                value: new DateTime(2026, 7, 16, 6, 7, 53, 303, DateTimeKind.Utc).AddTicks(4676));

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentBatch_CollegeId",
                table: "StudentEnrollmentBatch",
                column: "CollegeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentBatch_Tenant_Status",
                table: "StudentEnrollmentBatch",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentBatch_University_College_Year",
                table: "StudentEnrollmentBatch",
                columns: new[] { "UniversityId", "CollegeId", "AcademicYear" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_Batch_Status",
                table: "StudentEnrollmentItem",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_Status_LastAttempt",
                table: "StudentEnrollmentItem",
                columns: new[] { "Status", "LastAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_StudentFaceEmbeddingId",
                table: "StudentEnrollmentItem",
                column: "StudentFaceEmbeddingId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_StudentId",
                table: "StudentEnrollmentItem",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollmentItem_Tenant_Student",
                table: "StudentEnrollmentItem",
                columns: new[] { "TenantId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentEnrollmentItem_Batch_Student",
                table: "StudentEnrollmentItem",
                columns: new[] { "BatchId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentEnrollmentItem");

            migrationBuilder.DropTable(
                name: "StudentEnrollmentBatch");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 5, 14, 3, 46, 17, DateTimeKind.Utc).AddTicks(7044));
        }
    }
}
