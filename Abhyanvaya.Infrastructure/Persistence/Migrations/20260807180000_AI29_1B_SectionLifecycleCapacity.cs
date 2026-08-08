using System;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abhyanvaya.Infrastructure.Persistence.Migrations;

/// <summary>
/// AI29.1B — Enterprise Section Lifecycle & Capacity Engine.
/// Delivered via EF only (<c>Database.MigrateAsync</c> / <c>dotnet ef database update</c>).
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807180000_AI29_1B_SectionLifecycleCapacity")]
public partial class AI29_1B_SectionLifecycleCapacity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MinimumCapacity",
            table: "Sections",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "RecommendedCapacity",
            table: "Sections",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ReservedSeats",
            table: "Sections",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "WaitingListCount",
            table: "Sections",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "SectionTypeCode",
            table: "Sections",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "Regular");

        migrationBuilder.AddColumn<int>(
            name: "ParentSectionId",
            table: "Sections",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SectionGroupId",
            table: "Sections",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "Sections"
            SET "RecommendedCapacity" = "MaximumStrength"
            WHERE "RecommendedCapacity" = 0 AND "MaximumStrength" > 0;
            """);

        migrationBuilder.CreateTable(
            name: "SectionGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                CourseId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<int>(type: "integer", nullable: false),
                SemesterId = table.Column<int>(type: "integer", nullable: false),
                GroupCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                GroupName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionGroups", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionGroupMembers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SectionGroupId = table.Column<int>(type: "integer", nullable: false),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionGroupMembers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionLifecycleTransitions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SectionId = table.Column<int>(type: "integer", nullable: false),
                FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                TransitionedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TransitionedByUserId = table.Column<int>(type: "integer", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionLifecycleTransitions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionMergeTransactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetSectionId = table.Column<int>(type: "integer", nullable: false),
                SourceSectionIdsCsv = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                ReversedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionMergeTransactions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionSplitTransactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceSectionId = table.Column<int>(type: "integer", nullable: false),
                ChildSectionIdsCsv = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                StrategyCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                ReversedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionSplitTransactions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SectionLineages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ParentSectionId = table.Column<int>(type: "integer", nullable: false),
                ChildSectionId = table.Column<int>(type: "integer", nullable: false),
                RelationKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SectionLineages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TenantSectionCapacityPolicies",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollegeId = table.Column<int>(type: "integer", nullable: false),
                EnforceHardLimit = table.Column<bool>(type: "boolean", nullable: false),
                SoftLimitEnabled = table.Column<bool>(type: "boolean", nullable: false),
                WarningPercent = table.Column<int>(type: "integer", nullable: false),
                AutoWarningEnabled = table.Column<bool>(type: "boolean", nullable: false),
                UnderCapacityPercent = table.Column<int>(type: "integer", nullable: false),
                TenantId = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<int>(type: "integer", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TenantSectionCapacityPolicies", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SectionGroups_Tenant_Scope_Code",
            table: "SectionGroups",
            columns: new[] { "TenantId", "AcademicYearId", "CourseId", "GroupId", "SemesterId", "GroupCode" });

        migrationBuilder.CreateIndex(
            name: "IX_SectionGroupMembers_Tenant_Group_Current",
            table: "SectionGroupMembers",
            columns: new[] { "TenantId", "SectionGroupId", "IsCurrent" });

        migrationBuilder.CreateIndex(
            name: "IX_SectionLifecycleTransitions_Tenant_Section_Utc",
            table: "SectionLifecycleTransitions",
            columns: new[] { "TenantId", "SectionId", "TransitionedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SectionMergeTransactions_Tenant_TransactionId",
            table: "SectionMergeTransactions",
            columns: new[] { "TenantId", "TransactionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SectionSplitTransactions_Tenant_TransactionId",
            table: "SectionSplitTransactions",
            columns: new[] { "TenantId", "TransactionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SectionLineages_Tenant_Parent_Child",
            table: "SectionLineages",
            columns: new[] { "TenantId", "ParentSectionId", "ChildSectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_TenantSectionCapacityPolicies_TenantId",
            table: "TenantSectionCapacityPolicies",
            column: "TenantId",
            unique: true);

        // Permission seed (table names match EF model: Permission / ApplicationRolePermission)
        migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Permission') THEN
                INSERT INTO "Permission" ("Id", "Key", "Resource", "Action")
                SELECT v."Id", v."Key", v."Resource", v."Action"
                FROM (VALUES
                  (216, 'SectionLifecycle.View', 'SectionLifecycle', 'View'),
                  (217, 'SectionLifecycle.Edit', 'SectionLifecycle', 'Edit'),
                  (218, 'Section.Merge', 'Section', 'Merge'),
                  (219, 'Section.Split', 'Section', 'Split'),
                  (225, 'Section.Capacity', 'Section', 'Capacity'),
                  (226, 'Section.Readiness', 'Section', 'Readiness')
                ) AS v("Id", "Key", "Resource", "Action")
                WHERE NOT EXISTS (SELECT 1 FROM "Permission" p WHERE p."Id" = v."Id" OR p."Key" = v."Key");

                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ApplicationRolePermission')
                   AND EXISTS (SELECT 1 FROM "ApplicationRole" WHERE "Id" = 100) THEN
                  INSERT INTO "ApplicationRolePermission" ("ApplicationRoleId", "PermissionId")
                  SELECT 100, p."Id"
                  FROM "Permission" p
                  WHERE p."Id" IN (216, 217, 218, 219, 225, 226)
                    AND NOT EXISTS (
                      SELECT 1 FROM "ApplicationRolePermission" arp
                      WHERE arp."ApplicationRoleId" = 100 AND arp."PermissionId" = p."Id"
                    );
                END IF;
              END IF;
            END $$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SectionGroupMembers");
        migrationBuilder.DropTable(name: "SectionGroups");
        migrationBuilder.DropTable(name: "SectionLifecycleTransitions");
        migrationBuilder.DropTable(name: "SectionMergeTransactions");
        migrationBuilder.DropTable(name: "SectionSplitTransactions");
        migrationBuilder.DropTable(name: "SectionLineages");
        migrationBuilder.DropTable(name: "TenantSectionCapacityPolicies");

        migrationBuilder.DropColumn(name: "MinimumCapacity", table: "Sections");
        migrationBuilder.DropColumn(name: "RecommendedCapacity", table: "Sections");
        migrationBuilder.DropColumn(name: "ReservedSeats", table: "Sections");
        migrationBuilder.DropColumn(name: "WaitingListCount", table: "Sections");
        migrationBuilder.DropColumn(name: "SectionTypeCode", table: "Sections");
        migrationBuilder.DropColumn(name: "ParentSectionId", table: "Sections");
        migrationBuilder.DropColumn(name: "SectionGroupId", table: "Sections");

        migrationBuilder.Sql("""
            DELETE FROM "ApplicationRolePermission" WHERE "PermissionId" IN (216, 217, 218, 219, 225, 226);
            DELETE FROM "Permission" WHERE "Id" IN (216, 217, 218, 219, 225, 226);
            """);
    }
}
