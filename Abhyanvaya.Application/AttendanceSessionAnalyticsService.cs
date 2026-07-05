using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application;

/// <summary>
/// Computes read-only analytics for a single <see cref="AttendanceSession"/>.
/// </summary>
public sealed class AttendanceSessionAnalyticsService : IAttendanceSessionAnalyticsService
{
    private readonly IApplicationDbContext _context;

    public AttendanceSessionAnalyticsService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AttendanceSessionAnalyticsDto> GetSessionAnalyticsAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{attendanceSessionId}' was not found.");

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var attendanceCounts = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.AttendanceSessionId == attendanceSessionId)
            .GroupBy(a => a.Status)
            .Select(g => new AttendanceStatusCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return Build(session, recognitions, attendanceCounts);
    }

    internal static AttendanceSessionAnalyticsDto Build(
        AttendanceSession session,
        IReadOnlyList<AttendanceRecognition> recognitions,
        IReadOnlyList<AttendanceStatusCount> attendanceCounts)
    {
        var detectedFaces = session.DetectedFaces > 0 ? session.DetectedFaces : recognitions.Count;
        var identifiedFaces = session.RecognizedCount + session.ManualAssignmentCount;
        decimal? recognitionAccuracy = detectedFaces == 0
            ? null
            : decimal.Round((decimal)identifiedFaces / detectedFaces * 100m, 2, MidpointRounding.AwayFromZero);

        var presentStudents = attendanceCounts
            .Where(x => x.Status == AttendanceStatus.Present)
            .Sum(x => x.Count);
        var absentStudents = attendanceCounts
            .Where(x => x.Status == AttendanceStatus.Absent)
            .Sum(x => x.Count);

        var recognitionDuration = recognitions
            .Where(r => r.RecognitionTimeMilliseconds.HasValue)
            .Sum(r => r.RecognitionTimeMilliseconds!.Value);

        return new AttendanceSessionAnalyticsDto
        {
            AttendanceSessionId = session.Id,
            RecognizedCount = session.RecognizedCount,
            UnknownCount = session.UnknownCount,
            RejectedCount = session.RejectedCount,
            IgnoredCount = session.IgnoredCount,
            DuplicateCount = session.DuplicateCount,
            ManualAssignmentCount = session.ManualAssignmentCount,
            LowConfidenceCount = session.LowConfidenceCount,
            RecognitionAccuracy = recognitionAccuracy,
            AverageConfidence = session.AverageConfidence,
            TeacherCorrections = AttendanceRecognitionMetrics.CountTeacherCorrections(recognitions),
            RecognitionDurationMilliseconds = recognitionDuration > 0 ? recognitionDuration : null,
            ProcessingDurationMilliseconds = session.ProcessingMilliseconds,
            PresentStudents = presentStudents,
            AbsentStudents = absentStudents
        };
    }

    internal readonly record struct AttendanceStatusCount(AttendanceStatus Status, int Count);
}
