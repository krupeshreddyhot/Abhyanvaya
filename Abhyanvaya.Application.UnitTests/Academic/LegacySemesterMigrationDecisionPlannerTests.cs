using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class LegacySemesterMigrationDecisionPlannerTests
{
    private static LegacySemesterMigrationDecisionPlanner.Input Input(
        int? currentGroupId = null,
        int groupCount = 2,
        bool duplicate = false,
        IReadOnlyDictionary<int, int>? students = null,
        IReadOnlyDictionary<int, int>? attendance = null,
        IReadOnlyDictionary<int, int>? subjects = null)
    {
        var groups = Enumerable.Range(1, groupCount)
            .Select(i => new LegacySemesterMigrationDecisionPlanner.GroupInfo(i, i == 1 ? "FINANCE" : "COMPUTER APPLICATIONS"))
            .ToList();

        return new LegacySemesterMigrationDecisionPlanner.Input(
            SemesterId: 3,
            CourseId: 1,
            CourseName: "B.Com",
            CourseExists: true,
            CourseDeleted: false,
            Number: 3,
            Name: "Semester III",
            CurrentGroupId: currentGroupId,
            CurrentGroupName: currentGroupId is 2 ? "COMPUTER APPLICATIONS" : null,
            ActiveGroupsOnCourse: groups,
            Downstream: new LegacySemesterMigrationDecisionPlanner.DownstreamCounts(
                students ?? new Dictionary<int, int>(),
                attendance ?? new Dictionary<int, int>(),
                subjects ?? new Dictionary<int, int>(),
                new Dictionary<int, int>(),
                new Dictionary<int, int>(),
                new Dictionary<int, int>(),
                new Dictionary<int, int>()),
            HasDuplicateLegacyNumberOnCourse: duplicate);
    }

    [Fact]
    public void Semester_III_Style_Split_Produces_Two_Target_Groups()
    {
        var row = LegacySemesterMigrationDecisionPlanner.Plan(Input(
            students: new Dictionary<int, int> { [1] = 60, [2] = 236 }));
        Assert.Equal(LegacySemesterMigrationDecision.Split, row.Decision);
        Assert.Equal("SPLIT", row.DecisionCode);
        Assert.Equal(2, row.TargetGroupIds.Count);
        Assert.Contains(1, row.TargetGroupIds);
        Assert.Contains(2, row.TargetGroupIds);
        Assert.Equal(60, row.StudentCountsByTargetGroup[1]);
        Assert.Equal(236, row.StudentCountsByTargetGroup[2]);
        Assert.True(row.RequiresManualApproval);
        Assert.False(row.MustNotModify);
    }

    [Fact]
    public void Duplicate_Number_Is_DuplicateReview_And_MustNotModify()
    {
        var row = LegacySemesterMigrationDecisionPlanner.Plan(Input(duplicate: true, groupCount: 2));
        Assert.Equal(LegacySemesterMigrationDecision.DuplicateReview, row.Decision);
        Assert.True(row.MustNotModify);
    }

    [Fact]
    public void Semester_9_Style_Already_Group_Specific_Must_Not_Modify()
    {
        var row = LegacySemesterMigrationDecisionPlanner.Plan(Input(currentGroupId: 2, groupCount: 2));
        Assert.Equal(LegacySemesterMigrationDecision.AlreadyGroupSpecific, row.Decision);
        Assert.True(row.MustNotModify);
        Assert.False(row.RequiresManualApproval);
    }

    [Fact]
    public void Single_Group_Maps_To_MapToSingleGroup()
    {
        var row = LegacySemesterMigrationDecisionPlanner.Plan(Input(groupCount: 1));
        Assert.Equal(LegacySemesterMigrationDecision.MapToSingleGroup, row.Decision);
    }

    [Fact]
    public void Multi_Group_Without_Student_Span_Is_RetainPending()
    {
        var row = LegacySemesterMigrationDecisionPlanner.Plan(Input(groupCount: 2));
        Assert.Equal(LegacySemesterMigrationDecision.RetainLegacyPendingDecision, row.Decision);
        Assert.True(row.MustNotModify);
    }

    [Fact]
    public void Downstream_TeachingGroup_Is_IdentifyOnly()
    {
        var input = Input(students: new Dictionary<int, int> { [1] = 1, [2] = 1 });
        input = input with
        {
            Downstream = input.Downstream with
            {
                TeachingGroupsByGroup = new Dictionary<int, int> { [2] = 2 },
            },
        };
        var ds = LegacySemesterMigrationDecisionPlanner.ClassifyDownstream(input);
        var tg = ds.Single(x => x.EntityType == "TeachingGroup");
        Assert.Equal(DownstreamReferenceDeterminism.IdentifyOnlyDoNotMutate, tg.Determinism);
    }

    [Fact]
    public void Attendance_With_GroupId_Is_Deterministic_By_Entity_Group()
    {
        var input = Input(
            students: new Dictionary<int, int> { [1] = 1, [2] = 1 },
            attendance: new Dictionary<int, int> { [1] = 12, [2] = 55 });
        var att = LegacySemesterMigrationDecisionPlanner.ClassifyDownstream(input)
            .Single(x => x.EntityType == "AttendanceSession");
        Assert.Equal(DownstreamReferenceDeterminism.DeterministicByEntityGroupId, att.Determinism);
        Assert.Equal(67, att.ReferenceCount);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3AMigrationDecisionGuardTests
{
    [Fact]
    public void Prompt_2B_And_3A_Services_Remain_Read_Only()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Academic", "LegacySemesterMigrationAuditService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Academic", "LegacySemesterMigrationDecisionPlanService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Academic", "LegacySemesterMigrationDecisionPlanner.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(root, relative));
            Assert.DoesNotContain("SaveChanges", src, StringComparison.Ordinal);
            Assert.DoesNotContain("AddAsync", src, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\.GroupId\s*=\s*[^=]", src);
            Assert.DoesNotMatch(@"\.SemesterId\s*=\s*[^=]", src);
        }
    }

    [Fact]
    public void Api_Exposes_Decision_Plan_Without_Execution()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-migration-decision-plan", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSplit", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyMigration", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Decision_Codes_Are_Closed_Set()
    {
        var codes = Enum.GetValues<LegacySemesterMigrationDecision>()
            .Select(LegacySemesterMigrationDecisionPlanner.ToDecisionCode)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SPLIT", "MAP_TO_SINGLE_GROUP", "RETAIN_LEGACY_PENDING_DECISION",
                "DUPLICATE_REVIEW", "ALREADY_GROUP_SPECIFIC", "INVALID_DATA",
            },
            codes);
    }

    [Fact]
    public void Frozen_Boundaries_Intact()
    {
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
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
    public void Decision_Plan_Doc_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3A_MIGRATION_DECISION_PLAN.md")));
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
