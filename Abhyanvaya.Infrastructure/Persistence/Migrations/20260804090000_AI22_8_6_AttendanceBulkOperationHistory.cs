using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>AI22.8.6.4 — admin bulk operation audit history (additive).</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804090000_AI22_8_6_AttendanceBulkOperationHistory")]
public partial class AI22_8_6_AttendanceBulkOperationHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceBulkOperationHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RequestedCount = table.Column<int>(type: "integer", nullable: false),
                SucceededCount = table.Column<int>(type: "integer", nullable: false),
                SkippedCount = table.Column<int>(type: "integer", nullable: false),
                FailedCount = table.Column<int>(type: "integer", nullable: false),
                SessionIdsJson = table.Column<string>(type: "text", nullable: true),
                ResultJson = table.Column<string>(type: "text", nullable: true),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                PerformedBy = table.Column<int>(type: "integer", nullable: false),
                StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AttendanceBulkOperationHistory", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceBulkOperationHistory_TenantId_StartedUtc",
            table: "AttendanceBulkOperationHistory",
            columns: new[] { "TenantId", "StartedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceBulkOperationHistory");
    }
}
