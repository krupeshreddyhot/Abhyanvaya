namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24A — no new API/entity/DB for Program reassignment confirmation (UI-only).</summary>
public sealed class AI29_1D_24A_ProgramReassignmentBoundaryTests
{
    [Fact]
    public void No_New_Assignment_Endpoint_Or_Entity()
    {
        var domain = typeof(Abhyanvaya.Domain.Entities.Course).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain("CourseProgram", domain);
        Assert.DoesNotContain("ProgramCourse", domain);

        var helper = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "abhyanvaya-ui", "src", "utils", "programReassignmentConfirmation.ts")));
        Assert.DoesNotContain("api.", helper);
        Assert.DoesNotContain("fetch(", helper);
        Assert.Contains("shouldConfirmProgramReassignment", helper);
    }

    [Fact]
    public void Course_And_Program_Master_Wire_Helper()
    {
        var courses = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "abhyanvaya-ui", "src", "pages", "setup", "CoursesPage.tsx")));
        var programs = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "abhyanvaya-ui", "src", "pages", "setup", "ProgramsPage.tsx")));
        Assert.Contains("shouldConfirmProgramReassignment", courses);
        Assert.Contains("shouldConfirmProgramReassignment", programs);
        Assert.Contains("AcademicConfirmDialog", courses);
        Assert.Contains("AcademicConfirmDialog", programs);
    }
}
