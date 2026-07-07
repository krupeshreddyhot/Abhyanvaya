using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application;

/// <summary>
/// Materializes official <see cref="Attendance"/> rows from teacher-reviewed
/// <see cref="AttendanceRecognition"/> results for an <see cref="AttendanceSession"/> aggregate.
/// </summary>
public sealed class AttendanceBuilder : IAttendanceBuilder
{
    private readonly IApplicationDbContext _context;
    private readonly IAttendanceSessionSummaryService _sessionSummaryService;
    private readonly IAttendanceCalendar _attendanceCalendar;

    public AttendanceBuilder(
        IApplicationDbContext context,
        IAttendanceSessionSummaryService sessionSummaryService,
        IAttendanceCalendar attendanceCalendar)
    {
        _context = context;
        _sessionSummaryService = sessionSummaryService;
        _attendanceCalendar = attendanceCalendar;
    }

    /// <inheritdoc />
    public async Task<AttendanceBuildSummaryDto> BuildAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        await _sessionSummaryService.SyncSessionSummaryAsync(attendanceSessionId, cancellationToken);

        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{attendanceSessionId}' was not found.");

        session.ValidateCanBuildAttendance();

        // Anchor Attendance.Date to the reporting-day start (same as the manual marking endpoint) so photo/AI
        // attendance and manual attendance never diverge by the reporting-zone offset for the same calendar day.
        var attendanceDay = _attendanceCalendar.GetAttendanceDay(session.GetAttendanceDateUtc());
        var attendanceDate = attendanceDay.UtcStart;

        var isLocked = await _context.Attendances
            .AnyAsync(a =>
                    a.TenantId == session.TenantId
                    && a.SubjectId == session.SubjectId
                    && a.Date >= attendanceDay.UtcStart
                    && a.Date < attendanceDay.UtcEnd
                    && a.IsLocked,
                cancellationToken);

        session.ValidateAttendanceNotLocked(isLocked);

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var rosterStudentIds = await GetRosterStudentIdsAsync(session, cancellationToken);
        var existingStudentIds = await GetExistingAttendanceStudentIdsAsync(
            session,
            attendanceDay,
            rosterStudentIds,
            cancellationToken);

        var generation = AttendanceGenerationBuilder.Build(new AttendanceGenerationBuilder.BuildInput(
            attendanceSessionId,
            recognitions,
            rosterStudentIds,
            existingStudentIds));

        var studentsById = await LoadStudentsForRecognitionsAsync(generation.PresentRecognitions, cancellationToken);
        var attendancesToStage = new List<Attendance>();

        foreach (var recognition in generation.PresentRecognitions)
        {
            var studentId = recognition.StudentId!.Value;
            var attendance = CreateAttendance(session, attendanceDate, studentId, AttendanceStatus.Present);
            attendance.Detail = CreateAttendanceDetail(
                session,
                recognition,
                recognition.StudentId.HasValue && studentsById.TryGetValue(recognition.StudentId.Value, out var student)
                    ? student
                    : null);
            attendancesToStage.Add(attendance);
        }

        foreach (var studentId in generation.AbsentStudentIds)
        {
            attendancesToStage.Add(CreateAttendance(session, attendanceDate, studentId, AttendanceStatus.Absent));
        }

        StageAttendances(attendancesToStage);

        return generation.Summary;
    }

    private async Task<IReadOnlyDictionary<int, Student>> LoadStudentsForRecognitionsAsync(
        IReadOnlyList<AttendanceRecognition> recognitions,
        CancellationToken cancellationToken)
    {
        var studentIds = recognitions
            .Where(r => r.StudentId.HasValue)
            .Select(r => r.StudentId!.Value)
            .Distinct()
            .ToList();

        if (studentIds.Count == 0)
        {
            return new Dictionary<int, Student>();
        }

        return await _context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
    }

    private async Task<HashSet<int>> GetRosterStudentIdsAsync(
        AttendanceSession session,
        CancellationToken cancellationToken)
    {
        var classStudentIds = await _context.Students
            .AsNoTracking()
            .Where(s =>
                s.TenantId == session.TenantId
                && !s.IsDeleted
                && s.CourseId == session.CourseId
                && s.GroupId == session.GroupId
                && s.SemesterId == session.SemesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var subjectEnrolledIds = await _context.StudentSubjects
            .AsNoTracking()
            .Where(ss => ss.SubjectId == session.SubjectId)
            .Select(ss => ss.StudentId)
            .ToListAsync(cancellationToken);

        return classStudentIds.Concat(subjectEnrolledIds).ToHashSet();
    }

    private async Task<HashSet<int>> GetExistingAttendanceStudentIdsAsync(
        AttendanceSession session,
        AttendanceDay attendanceDay,
        HashSet<int> rosterStudentIds,
        CancellationToken cancellationToken)
    {
        // Match on the reporting-day range (not exact instant) so manual rows anchored at reporting-zone midnight
        // are recognized and never duplicated by the AI pipeline.
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

    private static Attendance CreateAttendance(
        AttendanceSession session,
        DateTime attendanceDate,
        int studentId,
        AttendanceStatus status) =>
        new()
        {
            StudentId = studentId,
            SubjectId = session.SubjectId,
            Date = attendanceDate,
            Status = status,
            AttendanceSessionId = session.Id,
            TenantId = session.TenantId
        };

    private static AttendanceDetail CreateAttendanceDetail(
        AttendanceSession session,
        AttendanceRecognition recognition,
        Student? student) =>
        new()
        {
            AttendanceId = 0,
            AttendanceRecognitionId = recognition.Id,
            CaptureMethod = session.AttendanceMethod,
            ConfidenceScore = recognition.ConfidenceScore,
            TeacherOverride = recognition.TeacherOverride,
            FaceNumber = recognition.FaceNumber,
            RecognitionSnapshotJson = RecognitionSnapshotSerializer.Serialize(session, recognition, student),
            TenantId = session.TenantId
        };

    private void StageAttendances(IReadOnlyList<Attendance> attendances)
    {
        if (attendances.Count == 0)
        {
            return;
        }

        _context.AddAttendances(attendances);
    }
}
