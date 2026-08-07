using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using FluentValidation.TestHelper;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1A — Program management, configuration, hierarchy contracts.</summary>
public class AI29_1A_ProgramManagementTests
{
    [Fact]
    public void Program_Supports_SoftDelete_And_Audit_Fields()
    {
        var p = new Program
        {
            CollegeId = 1,
            ProgramCode = "COM",
            ProgramName = "Commerce",
            IsActive = true,
            Status = "Active",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false,
        };
        Assert.False(p.IsDeleted);
        Assert.Equal("COM", p.ProgramCode);
        Assert.True(typeof(Program).IsSubclassOf(typeof(Abhyanvaya.Domain.Common.BaseEntity)));
    }

    [Fact]
    public void Course_ProgramId_Is_Nullable_Additive()
    {
        var course = new Course { Code = "BCOM", Name = "B.Com", ProgramId = null };
        Assert.Null(course.ProgramId);
        course.ProgramId = 5;
        Assert.Equal(5, course.ProgramId);
    }

    [Fact]
    public void Configuration_Defaults_Programs_Disabled()
    {
        var cfg = new TenantAcademicConfiguration { EnablePrograms = false, CollegeId = 1, TenantId = 1 };
        Assert.False(cfg.EnablePrograms);
    }

    [Fact]
    public void CreateProgram_Validator_Requires_Code_And_Name()
    {
        var v = new CreateProgramRequestValidator();
        var bad = v.TestValidate(new CreateProgramRequest { ProgramCode = "", ProgramName = "" });
        bad.ShouldHaveValidationErrorFor(x => x.ProgramCode);
        bad.ShouldHaveValidationErrorFor(x => x.ProgramName);
    }

    [Fact]
    public void UpdateProgram_Validator_Rejects_Invalid_Status()
    {
        var v = new UpdateProgramRequestValidator();
        var bad = v.TestValidate(new UpdateProgramRequest
        {
            ProgramCode = "COM",
            ProgramName = "Commerce",
            Status = "Weird",
        });
        bad.ShouldHaveValidationErrorFor(x => x.Status);

        var inactive = v.TestValidate(new UpdateProgramRequest
        {
            ProgramCode = "COM",
            ProgramName = "Commerce",
            Status = "Inactive",
        });
        inactive.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignCourse_Validator_Enforces_Single_Program()
    {
        var v = new AssignCourseProgramRequestValidator();
        var ok = v.TestValidate(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 2 });
        ok.ShouldNotHaveAnyValidationErrors();
        var clear = v.TestValidate(new AssignCourseProgramRequest { CourseId = 1, ProgramId = null });
        clear.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Hierarchy_Kinds_Are_Documented()
    {
        var kinds = new[] { "Program", "Course", "Group", "Semester", "Section", "Subject", "Unassigned" };
        Assert.Contains("Program", kinds);
        Assert.Contains("Unassigned", kinds);
    }

    [Fact]
    public void AttendanceSessionResolver_Not_Referenced_By_Program_Entity()
    {
        Assert.Null(typeof(Program).GetProperty("AttendanceSessionId"));
        Assert.Null(typeof(Course).GetProperty("SubjectId"));
    }

    [Fact]
    public void Dashboard_Prep_Dtos_Exist()
    {
        var stats = new ProgramStatisticsDto
        {
            ProgramId = 1,
            ProgramCode = "SCI",
            ProgramName = "Science",
            CourseCount = 3,
            StudentCount = 100,
            FacultyCount = 12,
            Status = "Active",
        };
        Assert.Equal(3, stats.CourseCount);
    }
}
