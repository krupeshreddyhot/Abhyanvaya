using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application;

/// <summary>
/// Read-only attendance session queries for the AI recognition review workflow.
/// </summary>
public sealed class AttendanceSessionQueryService : IAttendanceSessionQueryService
{
    private readonly IApplicationDbContext _context;
    private readonly IAttendanceCalendar _attendanceCalendar;

    public AttendanceSessionQueryService(
        IApplicationDbContext context,
        IAttendanceCalendar attendanceCalendar)
    {
        _context = context;
        _attendanceCalendar = attendanceCalendar;
    }

    public async Task<AttendanceSessionReviewDto?> GetSessionForReviewAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken);

        return session == null ? null : MapToReviewDto(session);
    }

    public async Task<AttendanceSessionStatusDto?> GetSessionStatusAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var recognitionStats = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Reviewed = g.Count(r => r.VerifiedByTeacher),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var rowCount = recognitionStats?.Count ?? 0;
        var reviewedCount = recognitionStats?.Reviewed ?? 0;

        var imageStats = await _context.AttendanceSessionImages
            .AsNoTracking()
            .Where(i => i.AttendanceSessionId == attendanceSessionId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Processed = g.Count(i => i.Status == AttendanceSessionImageStatus.Processed),
                CurrentSequence = g
                    .Where(i => i.Status == AttendanceSessionImageStatus.Processing)
                    .Select(i => (short?)i.ImageSequence)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return AttendanceSessionStatusMapper.Map(
            session,
            rowCount,
            reviewedCount,
            DateTime.UtcNow,
            imageStats?.Total ?? 0,
            imageStats?.Processed ?? 0,
            imageStats?.CurrentSequence);
    }

    public async Task<FinalizationStatusDto?> GetFinalizationStatusAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var attendanceAlreadyGenerated = await _context.Attendances
            .AsNoTracking()
            .AnyAsync(a => a.AttendanceSessionId == attendanceSessionId, cancellationToken);

        var rosterStudentIds = await GetRosterStudentIdsAsync(session, cancellationToken);
        var existingStudentIds = await GetExistingAttendanceStudentIdsAsync(session, rosterStudentIds, cancellationToken);
        var generation = AttendanceGenerationBuilder.Build(new AttendanceGenerationBuilder.BuildInput(
            attendanceSessionId,
            recognitions,
            rosterStudentIds,
            existingStudentIds));

        var blockers = FinalizationValidator.BuildBlockingReasons(
            session,
            recognitions,
            attendanceAlreadyGenerated);

        var facultyName = await _context.StaffMembers
            .AsNoTracking()
            .Where(s => s.Id == session.StaffId)
            .Select(s => (s.FirstName + " " + s.LastName).Trim())
            .FirstOrDefaultAsync(cancellationToken);

        var subjectName = await (
            from subject in _context.Subjects.AsNoTracking()
            join tenantSubject in _context.TenantSubjects.AsNoTracking()
                on subject.TenantSubjectId equals tenantSubject.Id
            where subject.Id == session.SubjectId
            select tenantSubject.Name).FirstOrDefaultAsync(cancellationToken);

        return new FinalizationStatusDto
        {
            AttendanceSessionId = attendanceSessionId,
            CanFinalize = blockers.Count == 0,
            BlockingReasons = blockers,
            PendingRecognitions = recognitions.Count(FinalizationValidator.IsPendingRecognition),
            ReviewedRecognitions = recognitions.Count(r => r.VerifiedByTeacher),
            ManualOverrides = recognitions.Count(r => r.TeacherOverride),
            RejectedRecognitions = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Rejected),
            UnknownFaces = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Unknown),
            AttendanceAlreadyGenerated = attendanceAlreadyGenerated
                || session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed,
            StudentsPresent = generation.Summary.Present,
            StudentsAbsent = generation.Summary.Absent,
            TotalStudents = rosterStudentIds.Count,
            AttendanceDate = session.AttendanceDate,
            FacultyName = facultyName,
            SubjectName = subjectName
        };
    }

    public async Task<AttendanceSessionReportDto?> GetSessionReportAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var attendances = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var recognitionIds = recognitions.Select(r => r.Id).ToList();
        DateTime firstReview = default;
        DateTime lastReview = default;

        if (recognitionIds.Count > 0)
        {
            firstReview = await _context.AttendanceRecognitionReviewHistories
                .AsNoTracking()
                .Where(h => recognitionIds.Contains(h.RecognitionId))
                .OrderBy(h => h.ReviewedUtc)
                .Select(h => h.ReviewedUtc)
                .FirstOrDefaultAsync(cancellationToken);

            lastReview = await _context.AttendanceRecognitionReviewHistories
                .AsNoTracking()
                .Where(h => recognitionIds.Contains(h.RecognitionId))
                .OrderByDescending(h => h.ReviewedUtc)
                .Select(h => h.ReviewedUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        int? reviewTimeMs = firstReview != default && lastReview != default
            ? (int?)(lastReview - firstReview).TotalMilliseconds
            : null;

        return new AttendanceSessionReportDto
        {
            AttendanceSessionId = attendanceSessionId,
            Present = attendances.Count(a => a.Status == AttendanceStatus.Present),
            Absent = attendances.Count(a => a.Status == AttendanceStatus.Absent),
            RecognitionAccuracy = session.RecognitionCompletionPercent,
            ManualCorrections = AttendanceRecognitionMetrics.CountTeacherCorrections(recognitions),
            ReviewTimeMilliseconds = reviewTimeMs,
            FinalizationTime = session.ApprovedUtc
        };
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetSessionAuditEntriesAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionExists = await _context.AttendanceSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == attendanceSessionId, cancellationToken);

        if (!sessionExists)
        {
            return [];
        }

        var entityId = attendanceSessionId.ToString();
        var entries = await _context.AuditEntries
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(AttendanceSession) && a.EntityId == entityId)
            .OrderByDescending(a => a.PerformedUtc)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return [];
        }

        var userIds = entries
            .Where(e => e.PerformedBy.HasValue)
            .Select(e => e.PerformedBy!.Value)
            .Distinct()
            .ToList();

        var usernames = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        return entries
            .Select(entry => new AuditEntryDto
            {
                Id = entry.Id,
                EntityName = entry.EntityName,
                Action = entry.Action,
                OldValues = entry.OldValues,
                NewValues = entry.NewValues,
                PerformedBy = entry.PerformedBy,
                PerformedByUsername = entry.PerformedBy.HasValue && usernames.TryGetValue(entry.PerformedBy.Value, out var username)
                    ? username
                    : null,
                PerformedUtc = entry.PerformedUtc
            })
            .ToList();
    }

    internal static AttendanceSessionReviewDto MapToReviewDto(AttendanceSession session) =>
        new()
        {
            Id = session.Id,
            Status = (int)session.Status,
            AttendanceDate = session.AttendanceDate,
            AnnotatedImageUrl = AttendanceSessionMediaPaths.BuildImageUrl(
                session.AnnotatedImageKey ?? session.ImageMetadata.ImageKey,
                session.CreatedUtc,
                "annotated"),
            OriginalImageUrl = AttendanceSessionMediaPaths.BuildMediaUrl(
                session.ImageMetadata.ImageKey,
                session.ImageMetadata.UploadedUtc ?? session.CreatedUtc),
            ImageWidth = session.ImageMetadata.Width,
            ImageHeight = session.ImageMetadata.Height
        };

    private static async Task<HashSet<int>> GetRosterStudentIdsAsync(
        AttendanceSession session,
        CancellationToken cancellationToken,
        IApplicationDbContext context)
    {
        var classStudentIds = await context.Students
            .AsNoTracking()
            .Where(s =>
                s.TenantId == session.TenantId
                && !s.IsDeleted
                && s.CourseId == session.CourseId
                && s.GroupId == session.GroupId
                && s.SemesterId == session.SemesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var subjectEnrolledIds = await context.StudentSubjects
            .AsNoTracking()
            .Where(ss => ss.SubjectId == session.SubjectId)
            .Select(ss => ss.StudentId)
            .ToListAsync(cancellationToken);

        return classStudentIds.Concat(subjectEnrolledIds).ToHashSet();
    }

    private Task<HashSet<int>> GetRosterStudentIdsAsync(
        AttendanceSession session,
        CancellationToken cancellationToken) =>
        GetRosterStudentIdsAsync(session, cancellationToken, _context);

    private async Task<HashSet<int>> GetExistingAttendanceStudentIdsAsync(
        AttendanceSession session,
        HashSet<int> rosterStudentIds,
        CancellationToken cancellationToken)
    {
        var attendanceDay = _attendanceCalendar.GetAttendanceDay(session.GetAttendanceDateUtc());

        // Match on the reporting-day range (not exact instant) so manual rows anchored at reporting-zone midnight
        // are recognized consistently with the finalization builder.
        var existingAttendances = await _context.Attendances
            .AsNoTracking()
            .Where(a =>
                a.TenantId == session.TenantId
                && a.SubjectId == session.SubjectId
                && a.Date >= attendanceDay.UtcStart
                && a.Date < attendanceDay.UtcEnd
                && rosterStudentIds.Contains(a.StudentId))
            .Select(a => a.StudentId)
            .ToListAsync(cancellationToken);

        return existingAttendances.ToHashSet();
    }
}
