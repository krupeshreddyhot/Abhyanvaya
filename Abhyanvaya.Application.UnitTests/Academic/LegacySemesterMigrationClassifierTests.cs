using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class LegacySemesterMigrationClassifierTests
{
    private static LegacySemesterMigrationClassifier.Input Base(
        int? groupId = null,
        int activeGroupCount = 2,
        bool courseExists = true,
        bool courseDeleted = false,
        bool duplicate = false,
        IReadOnlyDictionary<int, int>? studentsByGroup = null,
        int studentTotal = 0)
    {
        var groups = Enumerable.Range(1, activeGroupCount)
            .Select(i => new LegacySemesterMigrationClassifier.ActiveGroupInfo(i, $"G{i}", $"Group {i}"))
            .ToList();

        return new LegacySemesterMigrationClassifier.Input(
            SemesterId: 10,
            CourseId: 1,
            CourseName: "B.Com",
            CourseExists: courseExists,
            CourseDeleted: courseDeleted,
            Number: 3,
            Name: "Semester III",
            CurrentGroupId: groupId,
            CurrentGroupName: groupId is > 0 ? "Existing" : null,
            ActiveGroupsOnCourse: groups,
            StudentReferenceCount: studentTotal,
            StudentCountByGroupId: studentsByGroup ?? new Dictionary<int, int>(),
            AttendanceReferenceCount: 0,
            SubjectAllocationReferenceCount: 0,
            TimetableEntryReferenceCount: 0,
            SubjectReferenceCount: 0,
            SectionReferenceCount: 0,
            TeachingGroupReferenceCount: 0,
            HasDuplicateLegacyNumberOnCourse: duplicate);
    }

    [Fact]
    public void Single_Group_Course_Maps_To_MapSingleGroup()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(activeGroupCount: 1));
        Assert.Equal(LegacySemesterClassification.DeterministicSingleGroup, row.Classification);
        Assert.Equal(LegacySemesterMigrationAction.MapSingleGroup, row.MigrationAction);
        Assert.Equal("MAP_SINGLE_GROUP", row.MigrationActionCode);
    }

    [Fact]
    public void Multi_Group_Without_Student_Span_Is_ManualMappingRequired()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(activeGroupCount: 2));
        Assert.Equal(LegacySemesterClassification.AmbiguousMultiGroup, row.Classification);
        Assert.Equal(LegacySemesterMigrationAction.ManualMappingRequired, row.MigrationAction);
    }

    [Fact]
    public void Multi_Group_With_Students_In_Multiple_Groups_Is_SplitRequired()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(
            activeGroupCount: 2,
            studentTotal: 10,
            studentsByGroup: new Dictionary<int, int> { [1] = 4, [2] = 6 }));
        Assert.Equal(LegacySemesterMigrationAction.SplitRequired, row.MigrationAction);
        Assert.Equal("SPLIT_REQUIRED", row.MigrationActionCode);
    }

    [Fact]
    public void Zero_Group_Course_Is_OrphanReviewRequired()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(activeGroupCount: 0));
        Assert.Equal(LegacySemesterClassification.OrphanNoGroup, row.Classification);
        Assert.Equal(LegacySemesterMigrationAction.OrphanReviewRequired, row.MigrationAction);
    }

    [Fact]
    public void Invalid_Course_Is_InvalidDataReview()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(courseExists: false));
        Assert.Equal(LegacySemesterMigrationAction.InvalidDataReview, row.MigrationAction);
    }

    [Fact]
    public void Existing_GroupId_Is_AlreadyGroupSpecific()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(groupId: 5, activeGroupCount: 2));
        Assert.Equal(LegacySemesterMigrationAction.AlreadyGroupSpecific, row.MigrationAction);
        Assert.Equal("ALREADY_GROUP_SPECIFIC", row.MigrationActionCode);
    }

    [Fact]
    public void Duplicate_Legacy_Number_Is_ManualMappingRequired()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(activeGroupCount: 1, duplicate: true));
        Assert.Equal(LegacySemesterMigrationAction.ManualMappingRequired, row.MigrationAction);
        Assert.True(row.HasDuplicateLegacyNumberOnCourse);
    }

    [Fact]
    public void Candidate_Groups_Include_Student_Counts_As_Evidence_Only()
    {
        var row = LegacySemesterMigrationClassifier.Classify(Base(
            activeGroupCount: 2,
            studentsByGroup: new Dictionary<int, int> { [1] = 3, [2] = 7 },
            studentTotal: 10));
        Assert.Equal(2, row.CandidateGroups.Count);
        Assert.Equal(3, row.CandidateGroups.Single(g => g.GroupId == 1).StudentReferenceCount);
        Assert.Equal(7, row.CandidateGroups.Single(g => g.GroupId == 2).StudentReferenceCount);
        Assert.Equal(LegacySemesterMigrationAction.SplitRequired, row.MigrationAction);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt2BLegacySemesterAuditGuardTests
{
    [Fact]
    public void Audit_Service_Is_Read_Only_No_SaveChanges()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterMigrationAuditService.cs"));
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain(".Update(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDeleted = true", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveAsync", src, StringComparison.Ordinal);
        // Assignment `GroupId = value` (not comparison `GroupId ==`)
        Assert.DoesNotMatch(@"\.GroupId\s*=\s*[^=]", src);
    }

    [Fact]
    public void Api_Exposes_Read_Only_Audit_Without_Execute_Endpoints()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-migration-audit", src, StringComparison.Ordinal);
        Assert.Contains("ILegacySemesterMigrationAuditService", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Migrate", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSplit", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignGroup", src, StringComparison.Ordinal);
    }

    [Fact]
    public void No_NOT_NULL_Or_Student_Attendance_Remap_In_Prompt_2B()
    {
        var root = FindRepoRoot();
        var migrations = Path.Combine(root, "Abhyanvaya.Infrastructure", "Persistence", "Migrations");
        Assert.Empty(Directory.GetFiles(migrations, "*P1_4*Prompt*2B*"));
        Assert.Empty(Directory.GetFiles(migrations, "*LegacySemesterSplit*"));

        var entity = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Domain", "Entities", "Semester.cs"));
        Assert.Contains("public int? GroupId", entity, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Boundaries_Intact()
    {
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
    }

    [Fact]
    public void AcademicTree_Operational_Null_Wildcard_Is_Retired()
    {
        var root = FindRepoRoot();
        var tree = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicTreeService.cs"));
        Assert.DoesNotContain("s.GroupId == null || s.GroupId == g.Id", tree, StringComparison.Ordinal);
        Assert.Contains("s.GroupId == g.Id", tree, StringComparison.Ordinal);
    }

    [Fact]
    public void Classifier_Never_Picks_First_Group_Automatically()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterMigrationClassifier.cs"));
        Assert.DoesNotContain("ActiveGroupsOnCourse[0]", src, StringComparison.Ordinal);
        Assert.DoesNotContain(".First()", src, StringComparison.Ordinal);
        Assert.Contains("never invents", src, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found.");
    }
}
