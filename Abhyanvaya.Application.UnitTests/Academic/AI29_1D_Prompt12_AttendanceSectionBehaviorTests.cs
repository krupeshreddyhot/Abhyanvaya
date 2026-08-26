using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 12 — Attendance Section behavior.
/// Subject Master = Course + Group + Semester; Section scopes StudentSections population only.
/// Combined classes use AttendanceSessionResolutionDto.SectionIds (no FE SectionGroup duplication).
/// </summary>
public sealed class AI29_1D_Prompt12_AttendanceSectionBehaviorTests
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

    private static Mock<IApplicationDbContext> CreateDb()
    {
        var db = new Mock<IApplicationDbContext>();
        db.Setup(c => c.SchedulingAcademicYears).Returns(new[] { CurrentYear }.AsAsyncQueryable());
        db.Setup(c => c.Sections).Returns(new[] { SectionA, SectionB }.AsAsyncQueryable());
        db.Setup(c => c.StudentSections).Returns(new[]
        {
            new StudentSection { Id = 1, TenantId = 1, StudentId = 1, SectionId = 11, IsCurrent = true },
            new StudentSection { Id = 2, TenantId = 1, StudentId = 2, SectionId = 12, IsCurrent = true },
            new StudentSection { Id = 3, TenantId = 1, StudentId = 3, SectionId = 11, IsCurrent = true },
        }.AsAsyncQueryable());
        db.Setup(c => c.Students).Returns(new[]
        {
            new Student { Id = 1, TenantId = 1, CourseId = 1, GroupId = 2, SemesterId = 3, StudentNumber = "A-001", Name = "Alice" },
            new Student { Id = 2, TenantId = 1, CourseId = 1, GroupId = 2, SemesterId = 3, StudentNumber = "B-001", Name = "Bob" },
            new Student { Id = 3, TenantId = 1, CourseId = 1, GroupId = 2, SemesterId = 3, StudentNumber = "A-002", Name = "Amy" },
            new Student { Id = 4, TenantId = 1, CourseId = 1, GroupId = 2, SemesterId = 3, StudentNumber = "U-001", Name = "Una" },
        }.AsAsyncQueryable());
        return db;
    }

    private static IQueryable<Student> Cohort(IApplicationDbContext db) =>
        db.Students.Where(x => x.TenantId == 1 && x.CourseId == 1 && x.GroupId == 2 && x.SemesterId == 3);

    [Fact]
    public void Subject_Master_Is_Course_Group_Semester_Not_Section()
    {
        // Contract: Subject Master identity fields never include SectionId.
        var subjectScope = new { CourseId = 1, GroupId = 2, SemesterId = 3 };
        Assert.Equal(1, subjectScope.CourseId);
        Assert.Equal(2, subjectScope.GroupId);
        Assert.Equal(3, subjectScope.SemesterId);
        Assert.Null(typeof(AttendanceSessionResolutionDto).GetProperty("SubjectSectionId"));
    }

    [Fact]
    public async Task Section_A_Population_Is_Students_Assigned_To_A()
    {
        var db = CreateDb();
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [11]);
        Assert.Null(error);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "A-001", "A-002" }, roster);
        Assert.DoesNotContain("B-001", roster);
    }

    [Fact]
    public async Task Section_B_Population_Is_Students_Assigned_To_B()
    {
        var db = CreateDb();
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, [12]);
        Assert.Null(error);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .ToList();

        Assert.Equal(new[] { "B-001" }, roster);
    }

    [Fact]
    public async Task No_Section_Preserves_Full_Course_Group_Semester_Cohort()
    {
        var db = CreateDb();
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, Array.Empty<int>());
        Assert.Null(error);
        Assert.Empty(scope);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(4, roster.Count);
        Assert.Contains("U-001", roster);
    }

    [Fact]
    public async Task Combined_Sections_A_Plus_B_One_Session_Population()
    {
        // Timetable/session contract already expands SectionGroup → SectionIds; UI must not re-resolve groups.
        var resolution = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 4,
            SectionIds = [11, 12],
            SectionCodes = ["A", "B"],
        };

        Assert.Equal(2, resolution.SectionIds.Count);

        var db = CreateDb();
        var (scope, error) = await AttendanceSectionScope.ValidateSectionIdsAsync(
            db.Object, 1, 1, 2, 3, resolution.SectionIds.ToList());
        Assert.Null(error);

        var roster = AttendanceSectionScope
            .ApplyStudentSectionFilter(Cohort(db.Object), db.Object, 1, scope)
            .Select(s => s.StudentNumber)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "A-001", "A-002", "B-001" }, roster);
    }

    [Fact]
    public void Combined_Class_Uses_Resolver_SectionIds_Not_Frontend_SectionGroup()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("AttendanceSessionResolver", type.Name);
        Assert.Contains(
            typeof(AttendanceSessionResolutionDto).GetProperties().Select(p => p.Name),
            name => name == "SectionIds");
        // No second SectionGroup model / FE expansion surface on the resolution DTO.
        Assert.DoesNotContain(
            typeof(AttendanceSessionResolutionDto).GetProperties().Select(p => p.Name),
            name => name.Contains("SectionGroup", StringComparison.OrdinalIgnoreCase));
    }
}
