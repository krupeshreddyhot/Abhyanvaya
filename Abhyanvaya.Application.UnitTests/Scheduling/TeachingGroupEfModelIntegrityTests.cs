using System.Reflection;
using System.Text.RegularExpressions;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.3 Prompt 2 — EF Core model integrity for TeachingGroup (no DB apply).</summary>
public sealed class TeachingGroupEfModelIntegrityTests
{
    private sealed class AmbientCurrentUser : ICurrentUserService
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin";
        public int TenantId { get; set; } = 1;
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static ApplicationDbContext CreateDb(int tenantId = 1)
    {
        var ambient = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg-ef-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options, ambient, NullLogger<ApplicationDbContext>.Instance);
    }

    private static IEntityType Tg(ApplicationDbContext db) =>
        db.Model.FindEntityType(typeof(TeachingGroup))
        ?? throw new InvalidOperationException("TeachingGroup not mapped.");

    private static IEntityType TgSection(ApplicationDbContext db) =>
        db.Model.FindEntityType(typeof(TeachingGroupSection))
        ?? throw new InvalidOperationException("TeachingGroupSection not mapped.");

    private static IEntityType TgMembership(ApplicationDbContext db) =>
        db.Model.FindEntityType(typeof(TeachingGroupMembership))
        ?? throw new InvalidOperationException("TeachingGroupMembership not mapped.");

    [Fact]
    public void SubjectAllocation_has_many_TeachingGroups_without_unique_SubjectAllocationId()
    {
        using var db = CreateDb();
        var et = Tg(db);
        var fk = et.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "SubjectAllocationId"));
        Assert.False(fk.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);

        var uniqueOnAllocationAlone = et.GetIndexes()
            .Where(i => i.IsUnique)
            .Any(i => i.Properties.Count == 1 && i.Properties[0].Name == "SubjectAllocationId");
        Assert.False(uniqueOnAllocationAlone);

        Assert.NotNull(db.SchedulingTeachingGroups);
    }

    [Fact]
    public void TeachingGroup_has_many_Sections_and_Memberships()
    {
        using var db = CreateDb();
        var tg = Tg(db);
        Assert.Contains(tg.GetNavigations(), n => n.Name == "Sections" && n.IsCollection);
        Assert.Contains(tg.GetNavigations(), n => n.Name == "Memberships" && n.IsCollection);

        var sectionFk = TgSection(db).GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "TeachingGroupId"));
        Assert.Equal(DeleteBehavior.Cascade, sectionFk.DeleteBehavior);

        var membershipFk = TgMembership(db).GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "TeachingGroupId"));
        Assert.Equal(DeleteBehavior.Cascade, membershipFk.DeleteBehavior);
    }

    [Fact]
    public void TeachingGroupSection_links_one_Section_and_has_no_StudentId()
    {
        using var db = CreateDb();
        var et = TgSection(db);
        Assert.Null(et.FindProperty("StudentId"));
        Assert.Null(typeof(TeachingGroupSection).GetProperty("StudentId"));

        var sectionFk = et.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "SectionId"));
        Assert.Equal(typeof(Section), sectionFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, sectionFk.DeleteBehavior);
    }

    [Fact]
    public void TeachingGroupMembership_links_one_Student_without_duplicated_student_data()
    {
        using var db = CreateDb();
        var et = TgMembership(db);
        Assert.NotNull(et.FindProperty("StudentId"));
        Assert.Null(et.FindProperty("StudentName"));
        Assert.Null(et.FindProperty("StudentNumber"));
        Assert.Null(et.FindProperty("Course"));
        Assert.Null(et.FindProperty("Subject"));

        var studentFk = et.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "StudentId"));
        Assert.Equal(typeof(Student), studentFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, studentFk.DeleteBehavior);
    }

    [Fact]
    public void Dangerous_cascades_do_not_target_Student_Section_StudentSection_StudentSubject_or_SubjectAllocation()
    {
        using var db = CreateDb();

        foreach (var et in new[] { Tg(db), TgSection(db), TgMembership(db) })
        {
            foreach (var fk in et.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType.ClrType;
                if (principal == typeof(Student)
                    || principal == typeof(Section)
                    || principal == typeof(StudentSection)
                    || principal == typeof(StudentSubject)
                    || principal == typeof(SubjectAllocation))
                {
                    Assert.NotEqual(DeleteBehavior.Cascade, fk.DeleteBehavior);
                    Assert.NotEqual(DeleteBehavior.ClientCascade, fk.DeleteBehavior);
                }
            }
        }

        // No FK from TG graph into StudentSection / StudentSubject at all.
        Assert.DoesNotContain(TgMembership(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(StudentSection));
        Assert.DoesNotContain(TgMembership(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(StudentSubject));
        Assert.DoesNotContain(Tg(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(StudentSection));
    }

    [Fact]
    public void Duplicate_TeachingGroupSection_prevented_by_filtered_unique_index()
    {
        using var db = CreateDb();
        var unique = TgSection(db).GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(new[] { "TenantId", "TeachingGroupId", "SectionId" }, unique.Properties.Select(p => p.Name).ToArray());
        Assert.Contains("IsDeleted", unique.GetFilter() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Membership_duplicate_semantics_allow_history_but_unique_current_row()
    {
        using var db = CreateDb();
        var et = TgMembership(db);
        Assert.NotNull(et.FindProperty("EffectiveFrom"));
        Assert.NotNull(et.FindProperty("EffectiveTo"));
        Assert.NotNull(et.FindProperty("IsCurrent"));

        var unique = et.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(new[] { "TenantId", "TeachingGroupId", "StudentId" }, unique.Properties.Select(p => p.Name).ToArray());
        var filter = unique.GetFilter() ?? "";
        Assert.Contains("IsCurrent", filter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsDeleted", filter, StringComparison.OrdinalIgnoreCase);

        // Blind UNIQUE(TeachingGroupId, StudentId) without temporal filter must not exist.
        Assert.DoesNotContain(
            et.GetIndexes().Where(i => i.IsUnique),
            i => i.GetFilter() is null
                 && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TeachingGroupId", "StudentId" }));
    }

    [Fact]
    public void ExclusionGroupKey_is_not_unique()
    {
        using var db = CreateDb();
        var et = Tg(db);
        Assert.DoesNotContain(
            et.GetIndexes().Where(i => i.IsUnique),
            i => i.Properties.Any(p => p.Name == "ExclusionGroupKey"));
    }

    [Fact]
    public void Capacity_fields_nullable_and_ResolvedStudentCount_not_persisted()
    {
        using var db = CreateDb();
        var et = Tg(db);
        Assert.True(et.FindProperty("ExpectedStudentCount")!.IsNullable);
        Assert.True(et.FindProperty("MaxTeachingCapacity")!.IsNullable);
        Assert.Null(et.FindProperty("ResolvedStudentCount"));
        Assert.Null(et.FindProperty("CurrentStrength"));
        Assert.Null(et.FindProperty("ActualStudentCount"));
        Assert.Null(et.FindProperty("PlannedCapacity"));
        Assert.Null(et.FindProperty("SectionGroupId"));
        Assert.Null(typeof(TeachingGroup).GetProperty("SectionGroupId"));
        Assert.Null(typeof(TeachingGroup).GetProperty("ResolvedStudentCount"));
    }

    [Fact]
    public void Tenant_and_soft_delete_query_filters_remain_active()
    {
        using var db = CreateDb(tenantId: 1);
        var filter = Tg(db).GetQueryFilter();
        Assert.NotNull(filter);

        // Soft-deleted TeachingGroup must not appear under normal queries.
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            TenantId = 1,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            SubjectId = 1,
            SubjectAllocationId = 1,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Draft,
            Name = "SoftDeleted",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
            IsDeleted = true,
        });
        db.SaveChanges();

        Assert.Empty(db.SchedulingTeachingGroups.Where(x => x.Name == "SoftDeleted").ToList());
        Assert.Single(db.SchedulingTeachingGroups.IgnoreQueryFilters().Where(x => x.Name == "SoftDeleted").ToList());
    }

    [Fact]
    public void Cross_tenant_TeachingGroup_is_not_visible_under_tenant_filter()
    {
        using var db = CreateDb(tenantId: 1);
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            TenantId = 99,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            SubjectId = 1,
            SubjectAllocationId = 1,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Draft,
            Name = "OtherTenant",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false,
        });
        db.SaveChanges();

        Assert.Empty(db.SchedulingTeachingGroups.Where(x => x.Name == "OtherTenant").ToList());
        Assert.Single(db.SchedulingTeachingGroups.IgnoreQueryFilters().Where(x => x.Name == "OtherTenant").ToList());
    }

    [Fact]
    public void Migration_file_is_focused_and_does_not_touch_forbidden_schema()
    {
        var root = FindRepoRoot();
        var migrationPath = Path.Combine(
            root,
            "Abhyanvaya.Infrastructure",
            "Persistence",
            "Migrations",
            "20260817153000_AI_SCHED_TG_3_TeachingGroup.cs");
        Assert.True(File.Exists(migrationPath), $"Missing migration: {migrationPath}");
        var text = File.ReadAllText(migrationPath);

        Assert.Contains("SchedulingTeachingGroup", text);
        Assert.Contains("SchedulingTeachingGroupSection", text);
        Assert.Contains("SchedulingTeachingGroupMembership", text);
        Assert.Contains("ExpectedStudentCount", text);
        Assert.Contains("MaxTeachingCapacity", text);

        Assert.DoesNotContain("SectionGroupId", text);
        Assert.DoesNotContain("ResolvedStudentCount", text);
        Assert.DoesNotContain("PlannedCapacity", text);
        Assert.DoesNotContain("SchedulingTimetableEntry", text);
        Assert.DoesNotContain("TimetableEntry", text);
        Assert.DoesNotContain("StudentSection", text);
        Assert.DoesNotContain("StudentSubject", text);
        Assert.DoesNotContain("Attendance", text);

        var upOnly = Regex.Replace(
            text,
            @"protected override void Down\(MigrationBuilder migrationBuilder\)[\s\S]*",
            string.Empty);
        Assert.DoesNotContain("DropTable", upOnly);
        Assert.DoesNotContain("DropColumn", upOnly);
        Assert.DoesNotContain("AlterColumn", upOnly);

        // SubjectAllocationId must not be unique on TeachingGroup table.
        Assert.DoesNotContain("unique: true", ExtractIndexBlock(text, "IX_SchedulingTeachingGroup_TenantId_SubjectAllocationId"));

        Assert.Contains("unique: true", ExtractIndexBlock(text, "IX_SchedulingTeachingGroupSection_TenantId_TeachingGroupId_SectionId"));
        Assert.Contains("unique: true", ExtractIndexBlock(text, "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId"));
        Assert.Contains("IsCurrent", ExtractIndexBlock(text, "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId"));
    }

    [Fact]
    public void Infrastructure_TeachingGroup_configs_do_not_introduce_IgnoreQueryFilters()
    {
        var root = FindRepoRoot();
        var configDir = Path.Combine(root, "Abhyanvaya.Infrastructure", "Persistence", "Configurations", "Scheduling");
        foreach (var file in Directory.GetFiles(configDir, "TeachingGroup*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("IgnoreQueryFilters", text);
        }
    }

    [Fact]
    public void TeachingGroup_inherits_BaseEntity_audit_and_soft_delete_fields()
    {
        using var db = CreateDb();
        var et = Tg(db);
        Assert.True(typeof(BaseEntity).IsAssignableFrom(et.ClrType));
        Assert.NotNull(et.FindProperty("TenantId"));
        Assert.NotNull(et.FindProperty("CreatedDate"));
        Assert.NotNull(et.FindProperty("CreatedBy"));
        Assert.NotNull(et.FindProperty("UpdatedDate"));
        Assert.NotNull(et.FindProperty("UpdatedBy"));
        Assert.NotNull(et.FindProperty("IsDeleted"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ExtractIndexBlock(string migrationText, string indexName)
    {
        var idx = migrationText.IndexOf(indexName, StringComparison.Ordinal);
        Assert.True(idx >= 0, $"Index {indexName} not found in migration.");
        var end = migrationText.IndexOf(");", idx, StringComparison.Ordinal);
        return migrationText.Substring(idx, end - idx + 2);
    }
}
