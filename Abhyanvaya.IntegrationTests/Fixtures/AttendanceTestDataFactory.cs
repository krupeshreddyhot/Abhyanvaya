using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.IntegrationTests.Fixtures;

/// <summary>
/// Seeds minimal academic context and an AI attendance session for integration tests.
/// </summary>
public sealed class AttendanceTestDataFactory
{
    private readonly ApplicationDbContext _context;

    public AttendanceTestDataFactory(ApplicationDbContext context) => _context = context;

    public async Task<AttendanceTestScenario> CreateReviewReadyScenarioAsync(CancellationToken cancellationToken = default)
    {
        var gender = await EnsureGenderAsync(cancellationToken);
        var medium = await EnsureMediumAsync(cancellationToken);
        var language = await EnsureLanguageAsync(cancellationToken);
        var semester = await EnsureSemesterAsync(cancellationToken);
        var subject = await EnsureSubjectAsync(semester.Id, cancellationToken);

        var student = new Student
        {
            StudentNumber = $"IT-{Guid.NewGuid():N}"[..12],
            Name = "Integration Test Student",
            CourseId = 1,
            GroupId = 1,
            GenderId = gender.Id,
            MediumId = medium.Id,
            LanguageId = language.Id,
            FirstLanguageId = language.Id,
            SemesterId = semester.Id,
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };

        var session = AttendanceSession.CreateForPhotoAttendance(
            tenantId: 1,
            facultyId: 1,
            courseId: 1,
            groupId: 1,
            semesterId: semester.Id,
            subjectId: subject.Id,
            attendanceDate: DateTime.UtcNow.Date,
            periodNumber: 1);

        session.AttachClassroomImage(
            "integration/test.webp",
            "test.webp",
            new ClassroomImageMetadata { Width = 640, Height = 480, FileSize = 1024 },
            DateTime.UtcNow,
            1024);
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToAwaitingReview();

        await _context.AddAsync(student, cancellationToken);
        await _context.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var recognition = new AttendanceRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            AttendanceSessionId = session.Id,
            StudentId = student.Id,
            FaceNumber = 1,
            RecognitionStatus = RecognitionStatus.Recognized,
            ConfidenceScore = 92.5m,
            VerifiedByTeacher = false,
            CreatedUtc = DateTime.UtcNow
        };

        await _context.AddAsync(recognition, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AttendanceTestScenario(session, student, recognition, subject.Id);
    }

    private async Task<Gender> EnsureGenderAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Genders.FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var gender = new Gender
        {
            Name = "Test",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(gender, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return gender;
    }

    private async Task<Medium> EnsureMediumAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Mediums.FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var medium = new Medium
        {
            Name = "English",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(medium, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return medium;
    }

    private async Task<Language> EnsureLanguageAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Languages.FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var language = new Language
        {
            Name = "English",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(language, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return language;
    }

    private async Task<Semester> EnsureSemesterAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Semesters.FirstOrDefaultAsync(s => s.CourseId == 1, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var semester = new Semester
        {
            Number = 1,
            Name = "Semester 1",
            CourseId = 1,
            GroupId = 1,
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(semester, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return semester;
    }

    private async Task<Subject> EnsureSubjectAsync(int semesterId, CancellationToken cancellationToken)
    {
        var existing = await _context.Subjects.FirstOrDefaultAsync(
            s => s.CourseId == 1 && s.GroupId == 1 && s.SemesterId == semesterId,
            cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var tenantSubject = new TenantSubject
        {
            Name = "Integration Subject",
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(tenantSubject, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var subject = new Subject
        {
            TenantSubjectId = tenantSubject.Id,
            CourseId = 1,
            GroupId = 1,
            SemesterId = semesterId,
            TenantId = 1,
            CreatedDate = DateTime.UtcNow
        };
        await _context.AddAsync(subject, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return subject;
    }
}

public sealed record AttendanceTestScenario(
    AttendanceSession Session,
    Student Student,
    AttendanceRecognition Recognition,
    int SubjectId);
