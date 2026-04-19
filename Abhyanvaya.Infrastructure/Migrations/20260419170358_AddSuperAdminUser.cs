using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "User",
                columns: new[]
                {
                    "Id", "Username", "PasswordHash", "Role", "TenantId", "CourseId", "GroupId", "CreatedDate",
                    "IsDeleted",
                },
                values: new object[]
                {
                    2,
                    "superadmin",
                    "AQAAAAIAAYagAAAAEPhg9IgScv3LsXeH9Ka1UY1RXZB54SRNDRk6iCT1XcFHvhzSVEyaHCaW3CvNOak2Fw==",
                    3,
                    0,
                    1,
                    1,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    false,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "User",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
