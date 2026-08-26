using System.Reflection;
using System.Text.RegularExpressions;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4 Prompt 2 — TimetableEntry.TeachingGroupId EF/domain integrity.</summary>
public sealed class TimetableEntryTeachingGroupEfTests
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
            .UseInMemoryDatabase("tg4-entry-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options, ambient, NullLogger<ApplicationDbContext>.Instance);
    }

    private static IEntityType EntryType(ApplicationDbContext db) =>
        db.Model.FindEntityType(typeof(TimetableEntry))
        ?? throw new InvalidOperationException("TimetableEntry not mapped.");

    [Fact]
    public void TeachingGroupId_is_nullable_on_entity_and_model()
    {
        using var db = CreateDb();
        var prop = EntryType(db).FindProperty(nameof(TimetableEntry.TeachingGroupId));
        Assert.NotNull(prop);
        Assert.True(prop!.IsNullable);

        var entry = new TimetableEntry { TeachingGroupId = null };
        Assert.Null(entry.TeachingGroupId);
    }

    [Fact]
    public void TeachingGroup_FK_is_Restrict_and_optional()
    {
        using var db = CreateDb();
        var fk = EntryType(db).GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(TimetableEntry.TeachingGroupId)));
        Assert.Equal(typeof(TeachingGroup), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.False(fk.IsRequired);
        Assert.False(fk.IsUnique); // many entries per TeachingGroup
    }

    [Fact]
    public void TenantId_TeachingGroupId_index_exists()
    {
        using var db = CreateDb();
        Assert.Contains(
            EntryType(db).GetIndexes(),
            i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "TenantId", "TeachingGroupId" }));
    }

    [Fact]
    public void Same_tenant_TeachingGroup_association_is_valid()
    {
        TeachingGroupRules.EnsureTimetableEntryTeachingGroupTenant(
            timetableEntryTenantId: 1,
            teachingGroupId: 10,
            teachingGroupTenantId: 1);
    }

    [Fact]
    public void Cross_tenant_TeachingGroup_association_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.EnsureTimetableEntryTeachingGroupTenant(
                timetableEntryTenantId: 1,
                teachingGroupId: 10,
                teachingGroupTenantId: 2));
    }

    [Fact]
    public void Null_TeachingGroupId_skips_tenant_check()
    {
        TeachingGroupRules.EnsureTimetableEntryTeachingGroupTenant(
            timetableEntryTenantId: 1,
            teachingGroupId: null,
            teachingGroupTenantId: null);
    }

    [Fact]
    public void Multiple_TeachingGroups_per_SubjectAllocation_remain_allowed()
    {
        using var db = CreateDb();
        var tg = db.Model.FindEntityType(typeof(TeachingGroup))!;
        var saFk = tg.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "SubjectAllocationId"));
        Assert.False(saFk.IsUnique);
    }

    [Fact]
    public void TeachingGroupId_does_not_require_Section_or_TimetableSection()
    {
        using var db = CreateDb();
        var et = EntryType(db);
        Assert.Null(et.FindProperty("SectionId"));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Null(typeof(TimetableEntry).GetProperty("TimetableSectionId"));
        Assert.DoesNotContain(
            et.GetForeignKeys(),
            f => f.PrincipalEntityType.ClrType == typeof(TimetableSection)
                 || f.PrincipalEntityType.ClrType == typeof(Section));
    }

    [Fact]
    public void SectionGroupId_is_not_on_TimetableEntry_or_TeachingGroup()
    {
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionGroupId"));
        Assert.Null(typeof(TeachingGroup).GetProperty("SectionGroupId"));
        using var db = CreateDb();
        Assert.Null(EntryType(db).FindProperty("SectionGroupId"));
    }

    [Fact]
    public void Allocation_denormalization_does_not_set_TeachingGroupId()
    {
        var allocation = new SubjectAllocation
        {
            Id = 100,
            StaffId = 11,
            SubjectId = 22,
            CourseId = 33,
            GroupId = 44,
            SemesterId = 55,
            DepartmentId = 66,
            PreferredRoomId = 77,
        };
        var entry = new TimetableEntry { TeachingGroupId = null };
        Abhyanvaya.Application.Scheduling.TimetableService.ApplyAllocationDenormalization(entry, allocation, 88, courseDepartmentId: 66);
        Assert.Null(entry.TeachingGroupId);
        Assert.Equal(100, entry.SubjectAllocationId);
    }

    [Fact]
    public void Two_TeachingGroups_same_allocation_do_not_imply_automatic_entry_resolution()
    {
        // Model-level: entry holds only TeachingGroupId; no unique SA→TG path exists for inference.
        using var db = CreateDb();
        var entry = new TimetableEntry
        {
            SubjectAllocationId = 1,
            TeachingGroupId = null,
        };
        Assert.Null(entry.TeachingGroupId);
        Assert.Null(typeof(TimetableEntry).GetMethod("ResolveTeachingGroupFromSubjectAllocation"));
        Assert.Null(typeof(Abhyanvaya.Application.Scheduling.TimetableService)
            .GetMethod("ResolveTeachingGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    [Fact]
    public void Migration_file_is_focused_on_TimetableEntry_TeachingGroupId_only()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "Abhyanvaya.Infrastructure",
            "Persistence",
            "Migrations",
            "20260818110000_AI_SCHED_TG_4_TimetableEntryTeachingGroupId.cs");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);

        Assert.Contains("TeachingGroupId", text);
        Assert.Contains("SchedulingTimetableEntry", text);
        Assert.Contains("SchedulingTeachingGroup", text);
        Assert.Contains("ReferentialAction.Restrict", text);

        var upOnly = Regex.Replace(
            text,
            @"protected override void Down\(MigrationBuilder migrationBuilder\)[\s\S]*",
            string.Empty);
        var upCode = Regex.Replace(upOnly, @"//.*$", string.Empty, RegexOptions.Multiline);
        Assert.DoesNotContain("SchedulingTeachingGroupSection", upCode);
        Assert.DoesNotContain("TimetableSections", upCode);
        Assert.DoesNotContain("StudentSection", upCode);
        Assert.DoesNotContain("\"Attendance\"", upCode);
        Assert.DoesNotContain("SectionGroupId", upCode);
        Assert.DoesNotContain("DropTable", upCode);
        Assert.DoesNotContain("CreateTable", upCode);
        Assert.Contains("AddColumn", upCode);
    }

    [Fact]
    public void Architecture_guard_no_forbidden_patterns_in_timetable_or_tg_configs()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"),
            Path.Combine(root, "Abhyanvaya.Infrastructure", "Persistence", "Configurations", "Scheduling", "TimetableEntryConfiguration.cs"),
            Path.Combine(root, "Abhyanvaya.Domain", "Entities", "Scheduling", "TimetableEntry.cs"),
            Path.Combine(root, "Abhyanvaya.Domain", "Entities", "Scheduling", "TeachingGroupRules.cs"),
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("IgnoreQueryFilters", text);
            Assert.DoesNotContain("SectionGroupId", text);
            Assert.DoesNotContain("FindFirst", text);
            Assert.DoesNotContain("CreateTeachingGroup", text);
            Assert.DoesNotContain("ResolveTeachingGroupFromSubjectAllocation", text);
            Assert.DoesNotContain("DeleteBehavior.Cascade", text.Contains("TeachingGroup")
                ? ExtractTeachingGroupFkBlock(text)
                : string.Empty);
        }

        // Attendance resolver must remain TimetableSection-based (untouched).
        var resolver = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.Contains("TimetableSections", resolver);
        Assert.DoesNotContain("TeachingGroup", resolver);
        Assert.DoesNotContain("IgnoreQueryFilters", resolver);
    }

    [Fact]
    public void CloneEntry_preserves_TeachingGroupId_including_null()
    {
        var source = new TimetableEntry
        {
            DayOfWeek = 1,
            TimeSlotId = 2,
            SubjectAllocationId = 3,
            TeachingGroupId = 9,
            StaffId = 4,
            RoomId = 5,
            DepartmentId = 6,
            CourseId = 7,
            GroupId = 8,
            SemesterId = 9,
            SubjectId = 10,
        };
        var clone = Abhyanvaya.Application.Scheduling.TimetableService.CloneEntry(source, timetableId: 99);
        Assert.Equal(9, clone.TeachingGroupId);
        Assert.Equal(99, clone.TimetableId);

        source.TeachingGroupId = null;
        var cloneNull = Abhyanvaya.Application.Scheduling.TimetableService.CloneEntry(source, 100);
        Assert.Null(cloneNull.TeachingGroupId);
    }

    private static string ExtractTeachingGroupFkBlock(string text)
    {
        var idx = text.IndexOf("TeachingGroup", StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var end = Math.Min(text.Length, idx + 400);
        return text[idx..end];
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
}
