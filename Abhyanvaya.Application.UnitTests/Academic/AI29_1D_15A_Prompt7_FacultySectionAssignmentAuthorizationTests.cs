using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A Prompt 7 — Faculty→Section assign authorization.
/// Client Staff Id is never trusted; no silent faculty substitution.
/// </summary>
public sealed class AI29_1D_15A_Prompt7_FacultySectionAssignmentAuthorizationTests
{
    private const int TenantId = 1;

    private static readonly AcademicYear Year = new()
    {
        Id = 100,
        TenantId = TenantId,
        Name = "2026-27",
        Code = "2627",
        IsCurrent = true,
    };

    private static readonly Course Course = new()
    {
        Id = 1,
        TenantId = TenantId,
        Name = "BSc",
        Code = "BSC",
    };

    private static readonly Group Group = new()
    {
        Id = 2,
        TenantId = TenantId,
        CourseId = 1,
        Name = "MPC",
        Code = "MPC",
    };

    private static readonly Semester Semester = new()
    {
        Id = 3,
        TenantId = TenantId,
        Name = "Sem 1",
    };

    private static readonly Section SectionA = new()
    {
        Id = 11,
        TenantId = TenantId,
        CollegeId = 1,
        AcademicYearId = 100,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = "A",
        SectionName = "Section A",
        Status = "Active",
    };

    private static readonly Section OtherYearSection = new()
    {
        Id = 99,
        TenantId = TenantId,
        CollegeId = 1,
        AcademicYearId = 999,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = "Z",
        SectionName = "Other year",
        Status = "Active",
    };

    private static readonly StaffTypeLookup TeachingType = new()
    {
        Id = 501,
        TenantId = TenantId,
        Name = "Teaching",
        Code = "TEACHING",
        IsActive = true,
    };

    private static readonly EmploymentStatusLookup ActiveEmployment = new()
    {
        Id = 515,
        TenantId = TenantId,
        Name = "Active",
        Code = "ACTIVE",
        IsActive = true,
    };

    private static readonly EmploymentStatusLookup InactiveEmployment = new()
    {
        Id = 516,
        TenantId = TenantId,
        Name = "Inactive",
        Code = "INACTIVE",
        IsActive = false,
    };

    private static readonly Staff ValidFaculty = new()
    {
        Id = 42,
        TenantId = TenantId,
        CollegeId = 1,
        StaffTypeId = 501,
        DesignationId = 1,
        FirstName = "Ada",
        LastName = "Lovelace",
        StaffCode = "EMP-42",
        EmploymentStatusId = 515,
    };

    private static readonly Staff OtherTenantFaculty = new()
    {
        Id = 77,
        TenantId = 99,
        CollegeId = 9,
        StaffTypeId = 501,
        DesignationId = 1,
        FirstName = "Other",
        LastName = "Tenant",
        EmploymentStatusId = 515,
    };

    private static readonly Staff InactiveFaculty = new()
    {
        Id = 88,
        TenantId = TenantId,
        CollegeId = 1,
        StaffTypeId = 501,
        DesignationId = 1,
        FirstName = "Inactive",
        LastName = "Faculty",
        EmploymentStatusId = 516,
    };

    private static readonly Staff DeletedFaculty = new()
    {
        Id = 89,
        TenantId = TenantId,
        CollegeId = 1,
        StaffTypeId = 501,
        DesignationId = 1,
        FirstName = "Deleted",
        LastName = "Faculty",
        EmploymentStatusId = 515,
        IsDeleted = true,
    };

    private static Mock<IApplicationDbContext> CreateDb(
        IEnumerable<Section>? sections = null,
        IEnumerable<Staff>? staff = null,
        IEnumerable<AcademicYear>? years = null)
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears).Returns((years ?? [Year]).AsAsyncQueryable());
        db.Setup(c => c.Courses).Returns(new[] { Course }.AsAsyncQueryable());
        db.Setup(c => c.Groups).Returns(new[] { Group }.AsAsyncQueryable());
        db.Setup(c => c.Semesters).Returns(new[] { Semester }.AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns((sections ?? [SectionA, OtherYearSection]).AsAsyncQueryable());
        db.Setup(c => c.StaffMembers).Returns((staff ??
        [
            ValidFaculty, OtherTenantFaculty, InactiveFaculty, DeletedFaculty,
        ]).AsAsyncQueryable());
        db.Setup(c => c.StaffTypeLookups).Returns(new[] { TeachingType }.AsAsyncQueryable());
        db.Setup(c => c.EmploymentStatusLookups).Returns(new[]
        {
            ActiveEmployment, InactiveEmployment,
        }.AsAsyncQueryable());
        return db;
    }

    private static AssignFacultySectionRequest Req(
        int facultyId = 42,
        int sectionId = 11,
        int academicYearId = 100) =>
        new()
        {
            FacultyId = facultyId,
            SectionId = sectionId,
            AcademicYearId = academicYearId,
            Role = "Primary",
        };

    [Fact]
    public async Task Valid_Faculty_And_Section_Is_Allowed()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req());

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(42, result.FacultyId);
        Assert.Equal(11, result.Section!.Id);
        Assert.Equal(100, result.Section.AcademicYearId);
    }

    [Fact]
    public async Task Faculty_Outside_Tenant_Is_Rejected_Without_Substitution()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(facultyId: 77));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.UnauthorizedFacultyMessage, result.Error);
        Assert.Equal(77, result.FacultyId); // never substituted with a valid faculty
        Assert.Null(result.Section);
    }

    [Fact]
    public async Task Unauthorized_Missing_Faculty_Is_Rejected()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(facultyId: 404));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.UnauthorizedFacultyMessage, result.Error);
        Assert.Equal(404, result.FacultyId);
    }

    [Fact]
    public async Task Soft_Deleted_Faculty_Is_Rejected_As_Unauthorized()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(facultyId: 89));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.UnauthorizedFacultyMessage, result.Error);
        Assert.Equal(89, result.FacultyId);
    }

    [Fact]
    public async Task Inactive_Faculty_Is_Rejected()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(facultyId: 88));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.InactiveFacultyMessage, result.Error);
        Assert.Equal(88, result.FacultyId);
    }

    [Fact]
    public async Task Section_Outside_Academic_Scope_Is_Rejected()
    {
        // Client sends current AY but section belongs to another year.
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(sectionId: 99, academicYearId: 100));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.SectionOutOfAcademicScopeMessage, result.Error);
        Assert.Equal(42, result.FacultyId);
    }

    [Fact]
    public async Task Wrong_Academic_Year_On_Request_Is_Rejected()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(academicYearId: 999));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.SectionOutOfAcademicScopeMessage, result.Error);
    }

    [Fact]
    public async Task Missing_Section_Is_NotFound()
    {
        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb().Object, TenantId, Req(sectionId: 404));

        Assert.False(result.Ok);
        Assert.True(result.SectionNotFound);
        Assert.Equal(42, result.FacultyId);
    }

    [Fact]
    public async Task Invalid_Course_In_Section_Scope_Is_Rejected()
    {
        var badSection = new Section
        {
            Id = 55,
            TenantId = TenantId,
            CollegeId = 1,
            AcademicYearId = 100,
            CourseId = 999,
            GroupId = 2,
            SemesterId = 3,
            SectionCode = "X",
            SectionName = "Bad course",
            Status = "Active",
        };

        var result = await FacultySectionAssignmentAuthorization.ValidateAssignAsync(
            CreateDb(sections: [badSection]).Object, TenantId, Req(sectionId: 55));

        Assert.False(result.Ok);
        Assert.Equal(FacultySectionAssignmentAuthorization.InvalidCourseMessage, result.Error);
    }

    [Fact]
    public void AssignFacultyAsync_Uses_Authorization_Helper_And_Keeps_Api_Contract()
    {
        var requestProps = typeof(AssignFacultySectionRequest).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("FacultyId", requestProps);
        Assert.Contains("SectionId", requestProps);
        Assert.Contains("AcademicYearId", requestProps);

        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Abhyanvaya.Application", "Academic", "SectionManagementService.cs"));
        var source = File.ReadAllText(servicePath);
        Assert.Contains("FacultySectionAssignmentAuthorization.ValidateAssignAsync", source);
        Assert.Contains("validation.FacultyId", source);
        Assert.DoesNotContain("Invalid faculty.", source);
    }
}
