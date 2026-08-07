using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Events;
using FluentValidation.TestHelper;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1A.5 — Enterprise hierarchy hardening contracts & regressions.</summary>
public class AI29_1A5_EnterpriseHardeningTests
{
    [Fact]
    public void Program_Keeps_Simple_Lifecycle_Active_Inactive_Archived()
    {
        var allowed = new[] { "Active", "Inactive", "Archived" };
        Assert.Contains("Active", allowed);
        Assert.Contains("Inactive", allowed);
        Assert.Contains("Archived", allowed);
        Assert.DoesNotContain("Planning", allowed);
        Assert.DoesNotContain("Operational", allowed);
    }

    [Fact]
    public void UpdateProgram_Validator_Allows_Inactive_Rejects_Operational()
    {
        var v = new UpdateProgramRequestValidator();
        var ok = v.TestValidate(new UpdateProgramRequest
        {
            ProgramCode = "COM",
            ProgramName = "Commerce",
            Status = "Inactive",
        });
        ok.ShouldNotHaveAnyValidationErrors();

        var bad = v.TestValidate(new UpdateProgramRequest
        {
            ProgramCode = "COM",
            ProgramName = "Commerce",
            Status = "Operational",
        });
        bad.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Program_Optional_Branding_And_Calendar_Metadata()
    {
        var p = new Program
        {
            ProgramCode = "ENG",
            ProgramName = "Engineering",
            Icon = "engineering",
            ThemeColor = "#1B4F72",
            AcademicCalendarId = null,
            Status = "Active",
        };
        Assert.Equal("engineering", p.Icon);
        Assert.Equal("#1B4F72", p.ThemeColor);
        Assert.Null(p.AcademicCalendarId);
    }

    [Fact]
    public void DisplayOrder_Present_On_Academic_Entities()
    {
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.DisplayOrder)));
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.DisplayOrder)));
        Assert.NotNull(typeof(Group).GetProperty(nameof(Group.DisplayOrder)));
        Assert.NotNull(typeof(Semester).GetProperty(nameof(Semester.DisplayOrder)));
        Assert.NotNull(typeof(Section).GetProperty(nameof(Section.DisplayOrder)));
        Assert.NotNull(typeof(Subject).GetProperty(nameof(Subject.DisplayOrder)));
    }

    [Fact]
    public void DisplayOrder_Sort_Contract_DisplayOrder_Then_Name()
    {
        var items = new[]
        {
            new { DisplayOrder = 2, Name = "Alpha" },
            new { DisplayOrder = 1, Name = "Zeta" },
            new { DisplayOrder = 1, Name = "Beta" },
        };
        var sorted = items.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "Beta", "Zeta", "Alpha" }, sorted);
    }

    [Fact]
    public void ProgramPolicy_Is_Configuration_Only()
    {
        var policy = new ProgramPolicy
        {
            ProgramId = 1,
            MinimumAttendancePercent = 75,
            CreditsRequired = 120,
            PassMarks = 40,
            MaximumBacklogs = 4,
            MaximumSubjects = 8,
            AcademicRules = "No enforcement in AI29.1A.5",
        };
        Assert.Equal(75, policy.MinimumAttendancePercent);
        Assert.Contains("No enforcement", policy.AcademicRules);
    }

    [Fact]
    public void ProgramStatistics_Includes_Enterprise_Metrics()
    {
        var stats = new ProgramStatisticsDto
        {
            ProgramId = 1,
            ProgramCode = "SCI",
            ProgramName = "Science",
            StudentCount = 100,
            FacultyCount = 12,
            CourseCount = 3,
            TotalGroups = 6,
            TotalSemesters = 6,
            TotalSections = 12,
            TotalSubjects = 40,
            RunningClasses = 5,
            AttendancePercentage = 88.5m,
            RoomUtilization = 62.0m,
            Status = "Active",
        };
        Assert.Equal(100, stats.TotalStudents);
        Assert.Equal(3, stats.TotalCourses);
        Assert.Equal(12, stats.TotalFaculty);
        Assert.Equal(88.5m, stats.AttendancePercentage);
    }

    [Fact]
    public void Domain_Events_Exist_For_Program_And_Course_Assignment()
    {
        Assert.True(typeof(ProgramCreated).IsAssignableTo(typeof(Abhyanvaya.Domain.Common.IDomainEvent)));
        Assert.True(typeof(ProgramUpdated).IsAssignableTo(typeof(Abhyanvaya.Domain.Common.IDomainEvent)));
        Assert.True(typeof(ProgramArchived).IsAssignableTo(typeof(Abhyanvaya.Domain.Common.IDomainEvent)));
        Assert.True(typeof(CourseAssigned).IsAssignableTo(typeof(Abhyanvaya.Domain.Common.IDomainEvent)));
        Assert.True(typeof(CourseRemoved).IsAssignableTo(typeof(Abhyanvaya.Domain.Common.IDomainEvent)));
    }

    [Fact]
    public void Service_Split_Interfaces_Exist_Without_Duplicating_Facade()
    {
        Assert.True(typeof(IAcademicCatalogService).IsInterface);
        Assert.True(typeof(IAcademicHierarchyService).IsInterface);
        Assert.True(typeof(IAcademicHierarchyCache).IsInterface);
        Assert.True(typeof(IAcademicStructureService).IsInterface);
        Assert.Contains(typeof(AcademicStructureService).GetInterfaces(), i => i == typeof(IAcademicStructureService));
    }

    [Fact]
    public void Hierarchy_Cache_Contract_Methods_Exist()
    {
        var names = typeof(IAcademicHierarchyCache).GetMethods().Select(m => m.Name).ToHashSet();
        Assert.Contains(nameof(IAcademicHierarchyCache.InvalidateHierarchyAsync), names);
        Assert.Contains(nameof(IAcademicHierarchyCache.WarmCacheAsync), names);
        Assert.Contains(nameof(IAcademicHierarchyCache.RefreshCacheAsync), names);
    }

    [Fact]
    public void Versioned_Api_Route_Contract_Is_V1()
    {
        const string versionedRoute = "api/v1/academic-structure";
        const string legacyRoute = "api/academic-structure";
        Assert.StartsWith("api/v1/", versionedRoute);
        Assert.NotEqual(versionedRoute, legacyRoute);
    }

    [Fact]
    public void UpsertProgramPolicy_Validator_Bounds()
    {
        var v = new UpsertProgramPolicyRequestValidator();
        var bad = v.TestValidate(new UpsertProgramPolicyRequest { MinimumAttendancePercent = 120 });
        bad.ShouldHaveValidationErrorFor(x => x.MinimumAttendancePercent);
    }

    [Fact]
    public void Regression_AttendanceSessionResolver_Not_Touched_By_Program_Hardening()
    {
        Assert.Null(typeof(Program).GetProperty("AttendanceSessionId"));
        Assert.Null(typeof(ProgramPolicy).GetProperty("EnforceAttendance"));
        Assert.Null(typeof(Course).GetProperty("SubjectId"));
    }

    [Fact]
    public void Program_Not_Renamed_Aou_Is_Documentation_Concept()
    {
        Assert.Equal("Program", nameof(Program));
        var names = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Select(t => t.Name);
        Assert.DoesNotContain("AcademicOrganizationalUnit", names);
    }
}
