using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Events;
using FluentValidation.TestHelper;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24 Prompt 4A — Course→Program persistence consistency.</summary>
public sealed class AI29_1D_24_Prompt4A_CourseProgramPersistenceTests
{
    private static CourseProgramAssignmentRules.ProgramSnapshot Active(int id = 10) =>
        new(id, TenantId: 1, IsActive: true, Status: "Active");

    private static CourseProgramAssignmentRules.ProgramSnapshot Inactive(int id = 10) =>
        new(id, TenantId: 1, IsActive: false, Status: "Inactive");

    private static CourseProgramAssignmentRules.ProgramSnapshot Archived(int id = 10) =>
        new(id, TenantId: 1, IsActive: false, Status: "Archived");

    [Fact]
    public void Case01_Active_New_Assignment_Accepted()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(null, 10, Active(10));
        Assert.False(d.IsNoOp);
        Assert.Equal(10, d.NextProgramId);
        Assert.True(d.PublishAssigned);
        Assert.False(d.PublishRemoved);
        Assert.True(d.InvalidateCaches);
        Assert.Null(d.Error);
    }

    [Fact]
    public void Case02_Archived_New_Assignment_Rejected()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(null, 10, Archived(10));
        Assert.NotNull(d.Error);
        Assert.Contains("Archived", d.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(d.InvalidateCaches);
    }

    [Fact]
    public void Case03_Inactive_New_Assignment_Rejected()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(null, 10, Inactive(10));
        Assert.NotNull(d.Error);
        Assert.Contains("inactive", d.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case04_Existing_Inactive_Assignment_Retained()
    {
        // Commerce (10) became Inactive — keeping B.Com → Commerce is a no-op, not a "new" assign.
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, 10, Inactive(10));
        Assert.True(d.IsNoOp);
        Assert.Equal(10, d.NextProgramId);
        Assert.False(d.PublishAssigned);
        Assert.False(d.PublishRemoved);
        Assert.False(d.InvalidateCaches);
        Assert.Null(d.Error);
    }

    [Fact]
    public void Case05_Same_Program_Is_Idempotent()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, 10, Active(10));
        Assert.True(d.IsNoOp);
        Assert.False(d.PublishAssigned);
        Assert.False(d.InvalidateCaches);
    }

    [Fact]
    public void Case06_Program_Change_Publishes_Exactly_One_Assigned_Event()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, 20, Active(20));
        Assert.False(d.IsNoOp);
        Assert.True(d.PublishAssigned);
        Assert.False(d.PublishRemoved);
        Assert.Equal(20, d.NextProgramId);
        Assert.True(d.InvalidateCaches);
    }

    [Fact]
    public void Case07_Program_Removal_Publishes_CourseRemoved()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, null, null);
        Assert.True(d.PublishRemoved);
        Assert.False(d.PublishAssigned);
        Assert.Null(d.NextProgramId);
        Assert.True(d.InvalidateCaches);
    }

    [Fact]
    public void Case08_Cross_Tenant_Program_Treated_As_Invalid()
    {
        // Caller must not pass a foreign program snapshot — null target ⇒ Invalid Program.
        var d = CourseProgramAssignmentRules.EvaluateEnabled(null, 99, targetProgram: null);
        Assert.Equal("Invalid Program.", d.Error);
    }

    [Fact]
    public void Case09_Assign_Validator_Rejects_Invalid_CourseId()
    {
        var v = new AssignCourseProgramRequestValidator();
        var bad = v.TestValidate(new AssignCourseProgramRequest { CourseId = 0, ProgramId = 1 });
        bad.ShouldHaveValidationErrorFor(x => x.CourseId);
    }

    [Fact]
    public void Case10_Cache_Invalidation_Only_On_Actual_Change()
    {
        Assert.False(CourseProgramAssignmentRules.EvaluateEnabled(5, 5, Active(5)).InvalidateCaches);
        Assert.True(CourseProgramAssignmentRules.EvaluateEnabled(5, 6, Active(6)).InvalidateCaches);
        Assert.False(CourseProgramAssignmentRules.EvaluateDisabled(null).InvalidateCaches);
        Assert.True(CourseProgramAssignmentRules.EvaluateDisabled(5).InvalidateCaches);
    }

    [Fact]
    public void Case11_Course_Edit_Keeps_Inactive_Program_Without_Error()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(
            previousProgramId: 10,
            requestedProgramId: 10,
            targetProgram: Inactive(10));
        Assert.True(d.IsNoOp);
        Assert.Null(d.Error);
    }

    [Fact]
    public void Case12_Programs_Disabled_Legacy_Unlink_When_Previously_Linked()
    {
        var clear = CourseProgramAssignmentRules.EvaluateDisabled(10);
        Assert.False(clear.IsNoOp);
        Assert.True(clear.PublishRemoved);
        Assert.Null(clear.NextProgramId);

        var alreadyClear = CourseProgramAssignmentRules.EvaluateDisabled(null);
        Assert.True(alreadyClear.IsNoOp);
        Assert.False(alreadyClear.InvalidateCaches);
    }

    [Fact]
    public void Case13_Changing_To_Inactive_Different_Program_Rejected()
    {
        var d = CourseProgramAssignmentRules.EvaluateEnabled(10, 20, Inactive(20));
        Assert.NotNull(d.Error);
    }

    [Fact]
    public void Case14_Domain_Event_Types_Exist_For_Assign_And_Remove()
    {
        Assert.Equal(typeof(CourseAssigned), typeof(CourseAssigned));
        Assert.Equal(typeof(CourseRemoved), typeof(CourseRemoved));
        var assigned = new CourseAssigned(1, 2, 3, DateTime.UtcNow);
        var removed = new CourseRemoved(1, 2, 3, DateTime.UtcNow);
        Assert.Equal(2, assigned.ProgramId);
        Assert.Equal(2, removed.PreviousProgramId);
    }

    [Fact]
    public void Case15_Normalize_Treats_Zero_As_Unlink()
    {
        Assert.Null(CourseProgramAssignmentRules.NormalizeProgramId(0));
        Assert.Null(CourseProgramAssignmentRules.NormalizeProgramId(null));
        Assert.Equal(7, CourseProgramAssignmentRules.NormalizeProgramId(7));
    }
}
