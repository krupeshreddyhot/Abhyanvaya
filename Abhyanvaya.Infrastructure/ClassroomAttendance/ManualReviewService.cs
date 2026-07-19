using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class ManualReviewService : IManualReviewService
{
    public ManualReviewResult Evaluate(ManualReviewRequest request)
    {
        if (request.Reason.Contains("low", StringComparison.OrdinalIgnoreCase)
            || request.Reason.Contains("tie", StringComparison.OrdinalIgnoreCase)
            || request.Reason.Contains("manual", StringComparison.OrdinalIgnoreCase))
        {
            return new ManualReviewResult
            {
                RequiresReview = true,
                ReviewReason = request.Reason,
            };
        }

        return new ManualReviewResult { RequiresReview = false };
    }
}

public sealed class AttendanceAnalyticsService : IAttendanceAnalyticsService
{
    private readonly IApplicationDbContext _context;

    public AttendanceAnalyticsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AttendanceAnalyticsSnapshot> BuildSnapshotAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var total = recognitions.Count;
        var recognized = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Recognized);
        var unknown = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Unknown);
        var teacherOverrides = recognitions.Count(r => r.TeacherOverride);

        return new AttendanceAnalyticsSnapshot
        {
            SessionId = sessionId,
            RecognitionAccuracyPercent = total == 0 ? 0 : Math.Round((decimal)recognized / total * 100, 2),
            AttendanceAccuracyPercent = total == 0 ? 0 : Math.Round((decimal)(recognized - teacherOverrides) / total * 100, 2),
            TeacherCorrections = teacherOverrides,
            FalsePositives = 0,
            FalseNegatives = 0,
            UnknownRatePercent = total == 0 ? 0 : Math.Round((decimal)unknown / total * 100, 2),
        };
    }
}
