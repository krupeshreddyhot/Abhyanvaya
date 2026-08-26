using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A Prompt 3 — server-side section authorization on attendance writes.
/// Reuses AttendanceSectionScope; does not redesign AttendanceSessionResolver.
/// </summary>
public sealed class AI29_1D_15A_Prompt3_AttendanceSaveSectionAuthorizationTests
{
    private const int TenantId = 1;
    private const int CourseId = 1;
    private const int GroupId = 2;
    private const int SemesterId = 3;

    private static readonly AcademicYear CurrentYear = new()
    {
        Id = 100,
        TenantId = TenantId,
        IsCurrent = true,
        Name = "2026-27",
        Code = "2627",
    };

    private static readonly Section SectionA = new()
    {
        Id = 11,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "A",
        SectionName = "Section A",
    };

    private static readonly Section SectionB = new()
    {
        Id = 12,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "B",
        SectionName = "Section B",
    };

    private static readonly Section OtherTenantSection = new()
    {
        Id = 21,
        TenantId = 99,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "X",
        SectionName = "Other tenant",
    };

    private static readonly Section OtherYearSection = new()
    {
        Id = 99,
        TenantId = TenantId,
        AcademicYearId = 999,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "Z",
        SectionName = "Other year",
    };

    private static readonly Section WrongCourseSection = new()
    {
        Id = 31,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = 9,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "C9",
        SectionName = "Wrong course",
    };

    private static readonly Section WrongGroupSection = new()
    {
        Id = 32,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = 8,
        SemesterId = SemesterId,
        SectionCode = "G8",
        SectionName = "Wrong group",
    };

    private static readonly Section WrongSemesterSection = new()
    {
        Id = 33,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = 7,
        SectionCode = "S7",
        SectionName = "Wrong semester",
    };

    private static readonly Student StudentA = new()
    {
        Id = 1,
        TenantId = TenantId,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        StudentNumber = "A-001",
        Name = "Alice",
    };

    private static readonly Student StudentB = new()
    {
        Id = 2,
        TenantId = TenantId,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        StudentNumber = "B-001",
        Name = "Bob",
    };

    private static Mock<IApplicationDbContext> CreateDb(IEnumerable<AcademicYear>? years = null)
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears)
            .Returns((years ?? [CurrentYear]).AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[]
        {
            SectionA,
            SectionB,
            OtherTenantSection,
            OtherYearSection,
            WrongCourseSection,
            WrongGroupSection,
            WrongSemesterSection,
        }.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(new[]
        {
            new StudentSection { Id = 1, TenantId = TenantId, StudentId = 1, SectionId = 11, IsCurrent = true },
            new StudentSection { Id = 2, TenantId = TenantId, StudentId = 2, SectionId = 12, IsCurrent = true },
        }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[] { StudentA, StudentB }.AsAsyncQueryable());
        db.Setup(c => c.StaffSubjectAssignments).Returns(Array.Empty<StaffSubjectAssignment>().AsAsyncQueryable());
        return db;
    }

    private static Task<(IReadOnlyList<int> ScopeIds, string? Error)> ValidateAsync(
        IApplicationDbContext db,
        params int[] sectionIds) =>
        AttendanceSaveScope.ValidateWriteSectionScopeAsync(
            db,
            TenantId,
            CourseId,
            GroupId,
            SemesterId,
            sectionId: null,
            sectionIds: sectionIds);

    [Fact]
    public async Task Valid_Section_Is_Accepted()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 11);
        Assert.Null(error);
        Assert.Equal(new[] { 11 }, scope);
    }

    [Fact]
    public async Task Wrong_Tenant_Section_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 21);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Wrong_Academic_Year_Section_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 99);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Wrong_Course_Section_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 31);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Wrong_Group_Section_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 32);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Wrong_Semester_Section_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 33);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Multiple_Valid_Sections_Are_Accepted()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 11, 12);
        Assert.Null(error);
        Assert.Equal(2, scope.Count);
        Assert.Contains(11, scope);
        Assert.Contains(12, scope);
    }

    [Fact]
    public async Task Invalid_Section_Id_Is_Rejected()
    {
        var (scope, error) = await ValidateAsync(CreateDb().Object, 404);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task No_Section_Legacy_Request_Skips_Academic_Year()
    {
        var db = CreateDb(years: Array.Empty<AcademicYear>());
        var (scope, error) = await AttendanceSaveScope.ValidateWriteSectionScopeAsync(
            db.Object,
            TenantId,
            CourseId,
            GroupId,
            SemesterId,
            sectionId: null,
            sectionIds: null);

        Assert.Null(error);
        Assert.Empty(scope);
        Assert.False(AttendanceSaveScope.HasSectionScope(scope));
    }

    [Fact]
    public async Task Section_Scoped_Write_Requires_Exactly_One_Current_Academic_Year()
    {
        var none = CreateDb(years: Array.Empty<AcademicYear>());
        var (_, noneError) = await ValidateAsync(none.Object, 11);
        Assert.Equal(AttendanceSectionScope.NoCurrentAcademicYearMessage, noneError);

        var multiple = CreateDb(years:
        [
            CurrentYear,
            new AcademicYear { Id = 101, TenantId = TenantId, IsCurrent = true, Name = "Extra", Code = "X" },
        ]);
        var (_, multiError) = await ValidateAsync(multiple.Object, 11);
        Assert.Equal(AttendanceSectionScope.MultipleCurrentAcademicYearsMessage, multiError);
    }

    [Fact]
    public void Malicious_Out_Of_Section_Students_Are_Rejected_Fail_Closed()
    {
        // Valid section A scope authorizes A-001 only; client also submits B-001.
        var error = AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(
            ["A-001", "B-001"],
            ["A-001"]);
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
    }

    [Fact]
    public void Authorized_Section_Students_Pass_Fail_Closed_Check()
    {
        Assert.Null(
            AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(
                ["A-001", "B-001"],
                ["A-001", "B-001"]));
    }

    [Fact]
    public async Task StudentSections_Filter_Blocks_Out_Of_Section_Roster()
    {
        var db = CreateDb();
        var (scope, error) = await ValidateAsync(db.Object, 11);
        Assert.Null(error);

        var authorized = AttendanceSaveScope
            .ApplyAuthorizedSectionFilter(
                db.Object.Students.Where(s => s.TenantId == TenantId),
                db.Object,
                TenantId,
                scope)
            .Select(s => s.StudentNumber)
            .ToList();

        Assert.Equal(new[] { "A-001" }, authorized);
        Assert.Equal(
            AttendanceSaveScope.UnauthorizedStudentsMessage,
            AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(["A-001", "B-001"], authorized));
    }

    [Fact]
    public void Unauthorized_Faculty_Gate_Remains_FacultySubjectAccess_On_Controller()
    {
        // Application tests cannot reference the API assembly; enforce the wiring contract in source.
        var controllerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "AttendanceController.cs"));
        Assert.True(File.Exists(controllerPath), $"Missing controller at {controllerPath}");
        var source = File.ReadAllText(controllerPath);
        Assert.Contains("FacultyMayAccessSubjectAsync", source);
        Assert.Contains("ValidateWriteSectionScopeAsync", source);
        Assert.Contains("EnsureAllSubmittedStudentsAuthorized", source);
        // Faculty denial must occur before section validation on both write verbs.
        var markIdx = source.IndexOf("MarkAttendance", StringComparison.Ordinal);
        var editIdx = source.IndexOf("EditAttendance", StringComparison.Ordinal);
        Assert.True(markIdx >= 0 && editIdx > markIdx);
        var markFaculty = source.IndexOf("FacultyMayAccessSubjectAsync", markIdx, StringComparison.Ordinal);
        var markSection = source.IndexOf("ValidateWriteSectionScopeAsync", markIdx, StringComparison.Ordinal);
        Assert.True(markFaculty >= 0 && markSection > markFaculty);
        var editFaculty = source.IndexOf("FacultyMayAccessSubjectAsync", editIdx, StringComparison.Ordinal);
        var editSection = source.IndexOf("ValidateWriteSectionScopeAsync", editIdx, StringComparison.Ordinal);
        Assert.True(editFaculty >= 0 && editSection > editFaculty);
    }

    [Fact]
    public void AttendanceSessionResolver_Not_Modified_By_Write_Scope()
    {
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
        Assert.DoesNotContain(
            typeof(AttendanceSaveScope).GetMethods().Select(m => m.Name),
            n => n.Contains("ResolveAsync", StringComparison.OrdinalIgnoreCase));
    }
}
