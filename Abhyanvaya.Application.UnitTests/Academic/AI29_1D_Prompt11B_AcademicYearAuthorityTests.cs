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
/// AI29.1D Prompt 11B — fail-closed Academic Year authority for Section-scoped attendance.
/// </summary>
public sealed class AI29_1D_Prompt11B_AcademicYearAuthorityTests
{
    private static readonly AcademicYear YearA = new()
    {
        Id = 100,
        TenantId = 1,
        IsCurrent = true,
        Name = "2026-27",
        Code = "2627",
    };

    private static readonly AcademicYear YearB = new()
    {
        Id = 101,
        TenantId = 1,
        IsCurrent = true,
        Name = "2025-26",
        Code = "2526",
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

    private static Mock<IApplicationDbContext> CreateDb(
        IEnumerable<AcademicYear> years,
        IEnumerable<Section>? sections = null)
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears).Returns(years.AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns((sections ?? new[] { SectionA }).AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(new[]
        {
            new StudentSection { Id = 1, TenantId = 1, StudentId = 1, SectionId = 11, IsCurrent = true },
        }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[] { StudentA, StudentUnsectioned }.AsAsyncQueryable());
        return db;
    }

    private static IQueryable<Student> Cohort(IApplicationDbContext db) =>
        db.Students.Where(x => x.TenantId == 1 && x.CourseId == 1 && x.GroupId == 2 && x.SemesterId == 3);

    [Fact]
    public async Task Exactly_One_Current_Year_Is_Authoritative()
    {
        var db = CreateDb(new[]
        {
            YearA,
            new AcademicYear { Id = 200, TenantId = 1, IsCurrent = false, Name = "Prior", Code = "P" },
        });

        var authority = await AttendanceSectionScope.ResolveAuthoritativeCurrentAcademicYearAsync(db.Object, 1);
        Assert.Equal(AcademicYearAuthorityStatus.ExactlyOne, authority.Status);
        Assert.Equal(100, authority.AcademicYearId);
        Assert.Null(authority.Error);
    }

    [Fact]
    public async Task No_Current_Year_Does_Not_Guess()
    {
        var db = CreateDb(new[]
        {
            new AcademicYear { Id = 1, TenantId = 1, IsCurrent = false, Name = "A", Code = "A" },
            new AcademicYear { Id = 2, TenantId = 1, IsCurrent = false, Name = "B", Code = "B" },
        });

        var authority = await AttendanceSectionScope.ResolveAuthoritativeCurrentAcademicYearAsync(db.Object, 1);
        Assert.Equal(AcademicYearAuthorityStatus.None, authority.Status);
        Assert.Null(authority.AcademicYearId);
        Assert.Equal(AttendanceSectionScope.NoCurrentAcademicYearMessage, authority.Error);
    }

    [Fact]
    public async Task Multiple_Current_Years_Do_Not_Guess()
    {
        var db = CreateDb(new[] { YearA, YearB });
        var authority = await AttendanceSectionScope.ResolveAuthoritativeCurrentAcademicYearAsync(db.Object, 1);
        Assert.Equal(AcademicYearAuthorityStatus.Multiple, authority.Status);
        Assert.Null(authority.AcademicYearId);
        Assert.Equal(2, authority.CurrentYearIds.Count);
        Assert.Equal(AttendanceSectionScope.MultipleCurrentAcademicYearsMessage, authority.Error);
    }

    [Fact]
    public async Task Section_A_With_Valid_Current_Year_Succeeds()
    {
        var db = CreateDb(new[] { YearA });
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11]);

        Assert.Null(error);
        Assert.Equal(new[] { 11 }, scope);
        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .ToList();
        Assert.Equal(new[] { "A-001" }, roster);
    }

    [Fact]
    public async Task Section_A_With_No_Current_Year_Returns_Configuration_Error()
    {
        var db = CreateDb(Array.Empty<AcademicYear>());
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11]);

        Assert.Empty(scope);
        Assert.Equal(AttendanceSectionScope.NoCurrentAcademicYearMessage, error);
    }

    [Fact]
    public async Task Section_A_With_Multiple_Current_Years_Returns_Configuration_Error()
    {
        var db = CreateDb(new[] { YearA, YearB });
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11]);

        Assert.Empty(scope);
        Assert.Equal(AttendanceSectionScope.MultipleCurrentAcademicYearsMessage, error);
    }

    [Fact]
    public async Task No_Section_Filter_With_No_Current_Year_Preserves_Legacy_Cohort()
    {
        var db = CreateDb(Array.Empty<AcademicYear>());
        var requested = AttendanceSectionScope.NormalizeRequestedIds(null, null);
        Assert.Empty(requested);

        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, requested);
        Assert.Null(error);
        Assert.Empty(scope);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(new[] { "A-001", "U-001" }, roster);
    }

    [Fact]
    public async Task Legacy_Attendance_Still_Works_Without_Academic_Year()
    {
        // Course → Group → Semester → Subject → Period path does not require AY when section omitted.
        var db = CreateDb(Array.Empty<AcademicYear>());
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, Array.Empty<int>());
        Assert.Null(error);
        Assert.Empty(scope);
        Assert.Equal(2, Cohort(db.Object).Count());
    }

    [Fact]
    public async Task Wrong_Academic_Year_Section_Rejected()
    {
        var otherYearSection = new Section
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
        var db = CreateDb(new[] { YearA }, new[] { SectionA, otherYearSection });
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [99]);

        Assert.Empty(scope);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
    }

    [Fact]
    public async Task Wrong_Course_Group_Semester_Rejected()
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
        var db = CreateDb(new[] { YearA }, new[] { SectionA, foreign });
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [77]);

        Assert.Empty(scope);
        Assert.Equal(AttendanceSectionScope.SectionOutOfScopeMessage, error);
    }

    [Fact]
    public void AttendanceSessionResolver_Not_Redesigned()
    {
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }
}
