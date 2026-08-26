using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.IntegrationTests.Fixtures;

/// <summary>AI-SCHED-TG.5 Prompt 5A — Minimal PostgreSQL seed for Teaching Group membership integrity.</summary>
public sealed class TeachingGroupMembershipPgSeed
{
    private readonly ApplicationDbContext _db;

    public TeachingGroupMembershipPgSeed(ApplicationDbContext db) => _db = db;

    public async Task<(TeachingGroup Tg, Student Student)> SeedExplicitTeachingGroupWithStudentAsync(
        CancellationToken cancellationToken = default)
    {
        var gender = await EnsureAsync(
            () => _db.Genders.FirstOrDefaultAsync(cancellationToken),
            () => new Gender { Name = "TG-IT", TenantId = 1, CreatedDate = DateTime.UtcNow },
            cancellationToken);
        var medium = await EnsureAsync(
            () => _db.Mediums.FirstOrDefaultAsync(cancellationToken),
            () => new Medium { Name = "English", TenantId = 1, CreatedDate = DateTime.UtcNow },
            cancellationToken);
        var language = await EnsureAsync(
            () => _db.Languages.FirstOrDefaultAsync(cancellationToken),
            () => new Language { Name = "English", TenantId = 1, CreatedDate = DateTime.UtcNow },
            cancellationToken);

        var semester = await EnsureAsync(
            () => _db.Semesters.FirstOrDefaultAsync(s => s.CourseId == 1, cancellationToken),
            () => new Semester
            {
                Number = 1,
                Name = "Semester 1",
                CourseId = 1,
                GroupId = 1,
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            cancellationToken);

        var subject = await _db.Subjects.FirstOrDefaultAsync(
            s => s.CourseId == 1 && s.GroupId == 1 && s.SemesterId == semester.Id,
            cancellationToken);
        // Always create a dedicated Subject for this seed so SubjectAllocation uniqueness is not violated across tests.
        {
            var tenantSubject = new TenantSubject
            {
                Name = $"TG-Sub-{Guid.NewGuid():N}"[..20],
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            };
            await _db.AddAsync(tenantSubject, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            subject = new Subject
            {
                TenantSubjectId = tenantSubject.Id,
                CourseId = 1,
                GroupId = 1,
                SemesterId = semester.Id,
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            };
            await _db.AddAsync(subject, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var university = await EnsureAsync(
            () => _db.Universities.FirstOrDefaultAsync(cancellationToken),
            () => new University
            {
                Name = "TG University",
                Code = $"U{Guid.NewGuid():N}"[..8],
                CreatedDate = DateTime.UtcNow,
            },
            cancellationToken);

        var college = await EnsureAsync(
            () => _db.Colleges.FirstOrDefaultAsync(c => c.UniversityId == university.Id, cancellationToken),
            () => new College
            {
                Name = "TG College",
                Code = $"C{Guid.NewGuid():N}"[..8],
                UniversityId = university.Id,
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            cancellationToken);

        var year = await EnsureAsync(
            () => _db.SchedulingAcademicYears.FirstOrDefaultAsync(cancellationToken),
            () => new AcademicYear
            {
                Name = "2026-27",
                Code = $"AY{Guid.NewGuid():N}"[..8],
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2027, 5, 31),
                IsCurrent = true,
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            cancellationToken);

        var department = await EnsureAsync(
            () => _db.Departments.FirstOrDefaultAsync(d => d.CollegeId == college.Id, cancellationToken),
            () => new Department
            {
                CollegeId = college.Id,
                Name = "TG Dept",
                Code = "TGD",
                TenantId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            cancellationToken);

        var staffTypeId = await _db.StaffTypeLookups.Select(x => x.Id).FirstAsync(cancellationToken);
        var designationId = await _db.DesignationLookups.Select(x => x.Id).FirstAsync(cancellationToken);

        var staff = new Staff
        {
            CollegeId = college.Id,
            StaffTypeId = staffTypeId,
            DesignationId = designationId,
            FirstName = "TG",
            LastName = "Faculty",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(staff, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var sa = new SubjectAllocation
        {
            AcademicYearId = year.Id,
            SubjectId = subject.Id,
            StaffId = staff.Id,
            CourseId = 1,
            GroupId = 1,
            SemesterId = semester.Id,
            DepartmentId = department.Id,
            WeeklyHours = 3,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            TenantId = 1,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(sa, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var tg = new TeachingGroup
        {
            SubjectAllocationId = sa.Id,
            AcademicYearId = year.Id,
            CourseId = 1,
            GroupId = 1,
            SemesterId = semester.Id,
            SubjectId = subject.Id,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Active,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Name = $"TG-{Guid.NewGuid():N}"[..16],
            Code = $"C{Guid.NewGuid():N}"[..8],
            EffectiveFrom = new DateOnly(2026, 1, 1),
            TenantId = 1,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(tg, cancellationToken);

        var student = new Student
        {
            StudentNumber = $"TG-{Guid.NewGuid():N}"[..12],
            Name = "TG Membership Student",
            CourseId = 1,
            GroupId = 1,
            GenderId = gender.Id,
            MediumId = medium.Id,
            LanguageId = language.Id,
            FirstLanguageId = language.Id,
            SemesterId = semester.Id,
            TenantId = 1,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(student, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (tg, student);
    }

    private async Task<T> EnsureAsync<T>(
        Func<Task<T?>> find,
        Func<T> factory,
        CancellationToken cancellationToken) where T : class
    {
        var existing = await find();
        if (existing is not null)
            return existing;

        var entity = factory();
        await _db.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
