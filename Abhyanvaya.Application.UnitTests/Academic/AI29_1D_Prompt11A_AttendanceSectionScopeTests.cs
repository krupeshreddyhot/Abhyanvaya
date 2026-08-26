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
/// AI29.1D Prompt 11A — Attendance section scope hardening (filter / save / security contracts).
/// Uses AttendanceSectionScope + StudentSections; does not redesign AttendanceSessionResolver.
/// </summary>
public sealed class AI29_1D_Prompt11A_AttendanceSectionScopeTests
{
    private static readonly AcademicYear CurrentYear = new()
    {
        Id = 100,
        TenantId = 1,
        IsCurrent = true,
        Name = "2026-27",
        Code = "2627",
    };

    private static readonly Section SectionA = new()
    {
        Id = 11,
        TenantId = 1,
        AcademicYearId = 100,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = "A",
        SectionName = "Section A",
    };

    private static readonly Section SectionB = new()
    {
        Id = 12,
        TenantId = 1,
        AcademicYearId = 100,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = "B",
        SectionName = "Section B",
    };

    private static readonly Section OtherYearSection = new()
    {
        Id = 99,
        TenantId = 1,
        AcademicYearId = 999,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = "Z",
        SectionName = "Other year",
    };

    private static readonly Student StudentA = new()
    {
        Id = 1,
        TenantId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        StudentNumber = "A-001",
        Name = "Alice",
    };

    private static readonly Student StudentB = new()
    {
        Id = 2,
        TenantId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        StudentNumber = "B-001",
        Name = "Bob",
    };

    private static readonly Student StudentUnsectioned = new()
    {
        Id = 3,
        TenantId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        StudentNumber = "U-001",
        Name = "Una",
    };

    private static Mock<IApplicationDbContext> CreateDb()
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears).Returns(new[] { CurrentYear }.AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[] { SectionA, SectionB, OtherYearSection }.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(new[]
        {
            new StudentSection { Id = 1, TenantId = 1, StudentId = 1, SectionId = 11, IsCurrent = true },
            new StudentSection { Id = 2, TenantId = 1, StudentId = 2, SectionId = 12, IsCurrent = true },
        }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[] { StudentA, StudentB, StudentUnsectioned }.AsAsyncQueryable());
        return db;
    }

    private static IQueryable<Student> Cohort(IApplicationDbContext db) =>
        db.Students.Where(x =>
            x.TenantId == 1 && x.CourseId == 1 && x.GroupId == 2 && x.SemesterId == 3);

    [Fact]
    public async Task Section_A_Only_Returns_Only_Section_A_Students()
    {
        var db = CreateDb();
        var requested = AttendanceSectionScope.NormalizeRequestedIds(11, null);
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, requested);

        Assert.Null(error);
        Assert.Equal(new[] { 11 }, scope);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .ToList();

        Assert.Equal(new[] { "A-001" }, roster);
    }

    [Fact]
    public async Task Section_A_Plus_B_Returns_Students_In_A_Or_B()
    {
        var db = CreateDb();
        var requested = AttendanceSectionScope.NormalizeRequestedIds(null, [11, 12]);
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, requested);

        Assert.Null(error);
        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "A-001", "B-001" }, roster);
    }

    [Fact]
    public void No_Section_Filter_Returns_Full_Cohort()
    {
        var db = CreateDb();
        var requested = AttendanceSectionScope.NormalizeRequestedIds(null, null);
        Assert.Empty(requested);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, requested)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "A-001", "B-001", "U-001" }, roster);
    }

    [Fact]
    public async Task Save_Regression_Section_A_Roster_Is_Section_A_Only()
    {
        var db = CreateDb();
        var (scope, _) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11]);
        var saveNumbers = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .ToList();

        // Mark/Edit payload is the loaded roster — no save-contract change.
        Assert.Equal(new[] { "A-001" }, saveNumbers);
    }

    [Fact]
    public async Task Save_Regression_Combined_A_B_Roster()
    {
        var db = CreateDb();
        var (scope, _) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11, 12]);
        var saveNumbers = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "A-001", "B-001" }, saveNumbers);
    }

    [Fact]
    public void Save_Regression_No_Section_Preserves_Legacy_Full_Cohort()
    {
        var db = CreateDb();
        var saveNumbers = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, Array.Empty<int>())
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(3, saveNumbers.Count);
        Assert.Contains("U-001", saveNumbers);
    }

    [Fact]
    public async Task Security_Rejects_Section_Outside_Academic_Year_Scope()
    {
        var db = CreateDb();
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [99]);

        Assert.NotNull(error);
        Assert.Empty(scope);
        Assert.Contains("Academic Year", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Security_Rejects_Section_Outside_Course_Group_Semester()
    {
        var foreign = new Section
        {
            Id = 77,
            TenantId = 1,
            AcademicYearId = 100,
            CourseId = 9,
            GroupId = 9,
            SemesterId = 9,
            SectionCode = "X",
            SectionName = "Foreign",
        };
        var db = CreateDb();
        db.Setup(c => c.Sections).Returns(new[] { SectionA, SectionB, foreign }.AsAsyncQueryable());

        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [77]);

        Assert.NotNull(error);
        Assert.Empty(scope);
    }

    [Fact]
    public void AttendanceSessionResolver_Architecture_Intact()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("AttendanceSessionResolver", type.Name);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }
}
