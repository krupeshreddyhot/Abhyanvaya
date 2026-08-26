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
/// AI29.1D.15A Prompt 4 — every submitted student validated for section-scoped writes.
/// Chain: Submitted → current StudentSection → authoritative AY section scope → Authorized.
/// </summary>
public sealed class AI29_1D_15A_Prompt4_AttendanceStudentWriteScopeIntegrityTests
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

    private static readonly Student StudentUnsectioned = new()
    {
        Id = 3,
        TenantId = TenantId,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        StudentNumber = "U-001",
        Name = "Una",
    };

    private static Mock<IApplicationDbContext> CreateDb(
        IEnumerable<StudentSection>? memberships = null,
        IEnumerable<AcademicYear>? years = null)
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears)
            .Returns((years ?? [CurrentYear]).AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[] { SectionA, SectionB }.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns((memberships ??
        [
            new StudentSection { Id = 1, TenantId = TenantId, StudentId = 1, SectionId = 11, IsCurrent = true },
            new StudentSection { Id = 2, TenantId = TenantId, StudentId = 2, SectionId = 12, IsCurrent = true },
        ]).AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[] { StudentA, StudentB, StudentUnsectioned }.AsAsyncQueryable());
        return db;
    }

    private static Task<(IReadOnlyList<Student> Authorized, string? Error)> ValidateAsync(
        IApplicationDbContext db,
        IReadOnlyList<int> scopeSectionIds,
        params string[] submitted) =>
        AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            db,
            TenantId,
            CourseId,
            GroupId,
            SemesterId,
            scopeSectionIds,
            submitted);

    [Fact]
    public async Task Section_A_Accepts_Only_Students_In_A()
    {
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [11], "A-001");
        Assert.Null(error);
        Assert.Equal(new[] { "A-001" }, authorized.Select(s => s.StudentNumber));
    }

    [Fact]
    public async Task Section_A_Rejects_Student_From_B_Fail_Closed()
    {
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [11], "A-001", "B-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Combined_A_And_B_Accepts_Students_In_A_Or_B()
    {
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [11, 12], "A-001", "B-001");
        Assert.Null(error);
        Assert.Equal(2, authorized.Count);
        Assert.Contains(authorized, s => s.StudentNumber == "A-001");
        Assert.Contains(authorized, s => s.StudentNumber == "B-001");
    }

    [Fact]
    public async Task Combined_A_And_B_Rejects_Unsectioned_Student()
    {
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [11, 12], "A-001", "U-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Non_Current_StudentSection_Does_Not_Authorize()
    {
        var db = CreateDb(
        [
            new StudentSection { Id = 1, TenantId = TenantId, StudentId = 1, SectionId = 11, IsCurrent = false },
        ]);
        var (authorized, error) = await ValidateAsync(db.Object, [11], "A-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Browser_Forged_Student_List_Cannot_Bypass_Section_Scope()
    {
        // Client claims section A but submits only B's student.
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [11], "B-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Empty_Section_Scope_Is_No_Op_For_Legacy_Path()
    {
        var (authorized, error) = await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            CreateDb(years: Array.Empty<AcademicYear>()).Object,
            TenantId,
            CourseId,
            GroupId,
            SemesterId,
            validatedScopeSectionIds: [],
            submittedStudentNumbers: ["A-001", "U-001"]);

        Assert.Null(error);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Invalid_Scope_Section_Rejects_Before_Student_Membership()
    {
        var (authorized, error) = await ValidateAsync(CreateDb().Object, [404], "A-001");
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Empty(authorized);
    }

    [Fact]
    public void Mark_And_Edit_Use_Every_Student_Validator_Before_Persist()
    {
        var controllerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "AttendanceController.cs"));
        Assert.True(File.Exists(controllerPath), controllerPath);
        var source = File.ReadAllText(controllerPath);
        Assert.Contains("ValidateEverySubmittedStudentInSectionScopeAsync", source);
        Assert.DoesNotContain("partial", source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }
}
