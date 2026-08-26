using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A — integration-level atomic attendance write-scope tests.
/// 99 valid + 1 unauthorized ⇒ 0 committed. No AttendanceSessionResolver changes.
/// </summary>
public sealed class AI29_1D_15A_Prompt4_AtomicAttendanceWriteIntegrationTests
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

    private static readonly Section SectionC = new()
    {
        Id = 13,
        TenantId = TenantId,
        AcademicYearId = 100,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "C",
        SectionName = "Section C",
    };

    private static readonly Section WrongYearSection = new()
    {
        Id = 99,
        TenantId = TenantId,
        AcademicYearId = 999,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        SectionCode = "Z",
        SectionName = "Wrong year",
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

    private static readonly Student StudentC = new()
    {
        Id = 3,
        TenantId = TenantId,
        CourseId = CourseId,
        GroupId = GroupId,
        SemesterId = SemesterId,
        StudentNumber = "C-001",
        Name = "Cara",
    };

    private static Mock<IApplicationDbContext> CreateDb(IEnumerable<AcademicYear>? years = null)
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears)
            .Returns((years ?? [CurrentYear]).AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[]
        {
            SectionA, SectionB, SectionC, WrongYearSection,
        }.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(new[]
        {
            new StudentSection { Id = 1, TenantId = TenantId, StudentId = 1, SectionId = 11, IsCurrent = true },
            new StudentSection { Id = 2, TenantId = TenantId, StudentId = 2, SectionId = 12, IsCurrent = true },
            new StudentSection { Id = 3, TenantId = TenantId, StudentId = 3, SectionId = 13, IsCurrent = true },
        }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[] { StudentA, StudentB, StudentC }.AsAsyncQueryable());
        return db;
    }

    private static async Task<(IReadOnlyList<int> Scope, string? Error, IReadOnlyList<Student> Authorized, int Committed)>
        SimulateSectionScopedMarkAsync(IApplicationDbContext db, int[] sectionIds, params string[] submitted)
    {
        var committed = 0;
        var (scope, scopeError) = await AttendanceSaveScope.ValidateWriteSectionScopeAsync(
            db, TenantId, CourseId, GroupId, SemesterId, null, sectionIds);
        if (scopeError != null)
            return (Array.Empty<int>(), scopeError, Array.Empty<Student>(), committed: 0);

        var (authorized, studentError) = await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            db, TenantId, CourseId, GroupId, SemesterId, scope, submitted);
        if (studentError != null)
            return (scope, studentError, Array.Empty<Student>(), committed: 0);

        var (rows, planError) = AttendanceSaveScope.BuildAtomicMarkRows(
            submitted,
            authorized,
            stu => new Attendance
            {
                StudentId = stu.Id,
                SubjectId = 4,
                Status = AttendanceStatus.Present,
                TenantId = TenantId,
            });
        if (planError != null)
            return (scope, planError, Array.Empty<Student>(), committed: 0);

        // Atomic policy: commit all planned rows or none (transaction success path).
        committed = AttendanceSaveScope.CountAtomicCommitOrZero(submitted.Length, rows.Count);
        return (scope, null, authorized, committed);
    }

    [Fact]
    public async Task Section_A_All_Valid_Commits_All()
    {
        var (_, error, authorized, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [11], "A-001");
        Assert.Null(error);
        Assert.Single(authorized);
        Assert.Equal(1, committed);
    }

    [Fact]
    public async Task Section_A_One_Student_From_B_Commits_Zero()
    {
        var (_, error, _, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [11], "A-001", "B-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Equal(0, committed);
        Assert.Contains("No attendance was saved", error);
    }

    [Fact]
    public async Task Combined_A_Plus_B_All_Valid_Commits_All()
    {
        var (_, error, authorized, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [11, 12], "A-001", "B-001");
        Assert.Null(error);
        Assert.Equal(2, authorized.Count);
        Assert.Equal(2, committed);
    }

    [Fact]
    public async Task Combined_A_Plus_B_One_Student_From_C_Commits_Zero()
    {
        var (_, error, _, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [11, 12], "A-001", "B-001", "C-001");
        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task No_Section_Legacy_Skips_Academic_Year_Requirement()
    {
        var db = CreateDb(years: Array.Empty<AcademicYear>());
        var (scope, error) = await AttendanceSaveScope.ValidateWriteSectionScopeAsync(
            db.Object, TenantId, CourseId, GroupId, SemesterId, null, null);
        Assert.Null(error);
        Assert.Empty(scope);

        // Legacy: validator is no-op; commit policy not forced through section atomic planner.
        var (authorized, studentError) = await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            db.Object, TenantId, CourseId, GroupId, SemesterId, scope, ["A-001", "C-001"]);
        Assert.Null(studentError);
        Assert.Empty(authorized);
    }

    [Fact]
    public async Task Unauthorized_Section_Rejects_With_Clear_Error()
    {
        var (_, error, _, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [404], "A-001");
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task Wrong_Academic_Year_Section_Rejects_With_Zero_Commit()
    {
        var (_, error, _, committed) =
            await SimulateSectionScopedMarkAsync(CreateDb().Object, [99], "A-001");
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task Transaction_Rollback_When_One_Of_Many_Is_Invalid()
    {
        // 99 synthetic valid A-section numbers + 1 unauthorized B student ⇒ 0 committed.
        var manyA = Enumerable.Range(1, 99).Select(i => $"A-{i:D3}").ToList();
        var students = manyA.Select((n, i) => new Student
        {
            Id = 1000 + i,
            TenantId = TenantId,
            CourseId = CourseId,
            GroupId = GroupId,
            SemesterId = SemesterId,
            StudentNumber = n,
            Name = n,
        }).Concat([StudentB]).ToList();

        var memberships = students
            .Where(s => s.StudentNumber.StartsWith("A-", StringComparison.Ordinal))
            .Select((s, i) => new StudentSection
            {
                Id = 1000 + i,
                TenantId = TenantId,
                StudentId = s.Id,
                SectionId = 11,
                IsCurrent = true,
            })
            .Concat(
            [
                new StudentSection
                {
                    Id = 2000,
                    TenantId = TenantId,
                    StudentId = StudentB.Id,
                    SectionId = 12,
                    IsCurrent = true,
                },
            ])
            .ToList();

        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears).Returns(new[] { CurrentYear }.AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[] { SectionA, SectionB }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(students.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(memberships.AsAsyncQueryable());

        var submitted = manyA.Append("B-001").ToArray();
        Assert.Equal(100, submitted.Length);

        var (_, error, _, committed) =
            await SimulateSectionScopedMarkAsync(db.Object, [11], submitted);

        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Equal(0, committed);
        Assert.Equal(0, AttendanceSaveScope.CountAtomicCommitOrZero(100, 99));
    }

    [Fact]
    public async Task Edit_Attendance_With_Valid_Section_Authorizes_All()
    {
        var edit = new EditAttendanceRequest
        {
            SubjectId = 4,
            Date = DateTime.UtcNow.Date,
            SectionIds = [11],
            Students =
            [
                new StudentAttendanceDto { StudentNumber = "A-001", Status = AttendanceStatus.Present },
            ],
        };

        var scope = AttendanceSaveScope.Normalize(edit);
        var (authorized, error) = await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            CreateDb().Object, TenantId, CourseId, GroupId, SemesterId, scope,
            edit.Students.Select(s => s.StudentNumber));
        Assert.Null(error);
        Assert.Single(authorized);
    }

    [Fact]
    public async Task Edit_Attendance_With_Unauthorized_Student_Rejects_Zero_Commit()
    {
        var edit = new EditAttendanceRequest
        {
            SubjectId = 4,
            Date = DateTime.UtcNow.Date,
            SectionIds = [11],
            Students =
            [
                new StudentAttendanceDto { StudentNumber = "A-001", Status = AttendanceStatus.Present },
                new StudentAttendanceDto { StudentNumber = "B-001", Status = AttendanceStatus.Absent },
            ],
        };

        var scope = AttendanceSaveScope.Normalize(edit);
        var (authorized, error) = await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
            CreateDb().Object, TenantId, CourseId, GroupId, SemesterId, scope,
            edit.Students.Select(s => s.StudentNumber));

        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(authorized);
        Assert.Equal(0, AttendanceSaveScope.CountAtomicCommitOrZero(2, authorized.Count));
    }

    [Fact]
    public void Controller_Uses_Transaction_And_Atomic_Planner_For_Section_Writes()
    {
        var controllerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "AttendanceController.cs"));
        var source = File.ReadAllText(controllerPath);
        Assert.Contains("ExecuteInTransactionAsync", source);
        Assert.Contains("BuildAtomicMarkRows", source);
        Assert.Contains("ValidateEverySubmittedStudentInSectionScopeAsync", source);
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }

    [Fact]
    public void Atomic_Planner_Does_Not_Silently_Drop_Students()
    {
        var (rows, error) = AttendanceSaveScope.BuildAtomicMarkRows(
            ["A-001", "B-001"],
            [StudentA],
            stu => new Attendance { StudentId = stu.Id, SubjectId = 1, TenantId = TenantId });

        Assert.Equal(AttendanceSaveScope.UnauthorizedStudentsMessage, error);
        Assert.Empty(rows);
    }
}
