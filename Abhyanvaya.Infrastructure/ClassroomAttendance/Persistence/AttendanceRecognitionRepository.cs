using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance.Persistence;

public sealed class AttendanceRecognitionRepository : IAttendanceRecognitionRepository
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AttendanceRecognitionRepository> _logger;

    public AttendanceRecognitionRepository(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<AttendanceRecognitionRepository> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ReplaceSessionRecognitionsAsync(
        Guid sessionId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AttendanceRecognitions
            .Where(r => r.AttendanceSessionId == sessionId && r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
        {
            _context.Remove(row);
        }

        if (existing.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> ApplyAttendanceDecisionsAsync(
        IReadOnlyList<AttendanceDecision> decisions,
        CancellationToken cancellationToken = default)
    {
        var updated = 0;

        foreach (var decision in decisions)
        {
            if (!decision.RecognitionId.HasValue)
            {
                continue;
            }

            var entity = await _context.AttendanceRecognitions
                .FirstOrDefaultAsync(r => r.Id == decision.RecognitionId.Value, cancellationToken);

            if (entity == null)
            {
                continue;
            }

            entity.RecognitionStatus = decision.RecognitionStatus;
            entity.StudentId = decision.StudentId;
            entity.ConfidenceScore = decision.Confidence;
            updated++;
        }

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Attendance decisions applied. Count={Count}", updated);
        return updated;
    }

    public async Task UpdateSessionCountersAsync(
        AttendanceSession session,
        AttendanceSessionStatistics statistics,
        CancellationToken cancellationToken = default)
    {
        session.DetectedFaces = statistics.DetectedFaces;
        session.RecognizedFaces = statistics.StudentsPresent;
        session.UnknownFaces = statistics.UnknownFaces;
        session.RecognizedCount = statistics.StudentsPresent;
        session.UnknownCount = statistics.UnknownFaces;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
