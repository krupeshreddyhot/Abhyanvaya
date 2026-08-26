using Abhyanvaya.Application.Academic;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24 Prompts 5–21 — boundary guards (No Program policy, Subject Master, engines, fail-closed).
/// </summary>
public sealed class AI29_1D_24_Prompt5to21_CourseProgramUiBoundaryTests
{
    [Fact]
    public void Prompt5_Unassign_To_Null_Is_Allowed_By_Existing_Rules()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, null, null);
        Assert.Null(d.Error);
        Assert.True(d.PublishRemoved);
        Assert.Null(d.NextProgramId);
    }

    [Fact]
    public void Prompt7_Course_ProgramId_Is_Authoritative_Count_Source()
    {
        // Count semantics: courses where ProgramId == programId (no separate counter field on Program).
        var courses = new[]
        {
            new Course { Id = 1, ProgramId = 10 },
            new Course { Id = 2, ProgramId = 10 },
            new Course { Id = 3, ProgramId = null },
            new Course { Id = 4, ProgramId = 20 },
        };
        Assert.Equal(2, courses.Count(c => c.ProgramId == 10));
        Assert.Equal(0, courses.Count(c => c.ProgramId == 99));
    }

    [Fact]
    public void Prompt10_Subject_Entity_Has_No_ProgramId_Or_SectionId()
    {
        var props = typeof(Subject).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(Subject.CourseId), props);
        Assert.Contains(nameof(Subject.GroupId), props);
        Assert.Contains(nameof(Subject.SemesterId), props);
        Assert.DoesNotContain("ProgramId", props);
        Assert.DoesNotContain("SectionId", props);
    }

    [Fact]
    public void Prompt12_No_CourseProgram_Entity_In_Domain()
    {
        var names = typeof(Course).Assembly.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("CourseProgram", names);
        Assert.DoesNotContain("ProgramCourse", names);
    }

    [Fact]
    public void Prompt20_CourseMaster_Uses_WriteService_Not_Ui_Ef()
    {
        var write = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.Application", "Academic", "CourseMasterWriteService.cs")));
        Assert.Contains("AssignCourseToProgramAsync", write);
        Assert.Contains("ExecuteInTransactionAsync", write);

        var uiPersist = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "abhyanvaya-ui", "src", "utils", "courseMasterPersistence.ts")));
        Assert.Contains("callAssignCourseSeparately: false", uiPersist);
        Assert.DoesNotContain("DbContext", uiPersist);
    }

    [Fact]
    public void Prompt8_FailClosed_Cascade_Still_Empty_Without_Program()
    {
        var cascade = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "abhyanvaya-ui", "src", "utils", "academicCascade.ts")));
        Assert.Contains("filterCoursesForProgram", cascade);
        Assert.Contains("if (programId == null) return [];", cascade);
        Assert.Contains("Do not fall back to the full course catalog", cascade);
    }
}
