using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application;

/// <summary>
/// Encapsulates recognition summary calculation for <see cref="AttendanceSession"/> aggregates.
/// </summary>
public sealed class AttendanceSessionSummaryService : IAttendanceSessionSummaryService
{
    private readonly IApplicationDbContext _context;

    public AttendanceSessionSummaryService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task SyncSessionSummaryAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{attendanceSessionId}' was not found.");

        var recognitions = await _context.AttendanceRecognitions
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        ApplySummary(session, recognitions);
    }

    internal static void ApplySummary(
        AttendanceSession session,
        IReadOnlyList<AttendanceRecognition> recognitions)
    {
        var counts = AttendanceRecognitionMetrics.CountByStatus(recognitions);

        session.RecognizedCount = counts.RecognizedCount;
        session.UnknownCount = counts.UnknownCount;
        session.RejectedCount = counts.RejectedCount;
        session.IgnoredCount = counts.IgnoredCount;
        session.DuplicateCount = counts.DuplicateCount;
        session.ManualAssignmentCount = counts.ManualAssignmentCount;
        session.LowConfidenceCount = counts.LowConfidenceCount;
        session.AverageConfidence = AttendanceRecognitionMetrics.ComputeAverageConfidence(recognitions);
        session.RecognitionCompletionPercent =
            AttendanceRecognitionMetrics.ComputeRecognitionCompletionPercent(recognitions);
    }
}
