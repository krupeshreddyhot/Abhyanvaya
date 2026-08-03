using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class AttendanceSessionManager : IAttendanceSessionManager
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceWorkflowLifecycleService _workflow;
    private readonly ILogger<AttendanceSessionManager> _logger;

    public AttendanceSessionManager(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IAttendanceWorkflowLifecycleService workflow,
        ILogger<AttendanceSessionManager> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _workflow = workflow;
        _logger = logger;
    }

    public async Task<AttendanceSession> LoadSessionAsync(
        Guid sessionId,
        int tenantId,
        CancellationToken cancellationToken = default) =>
        await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, cancellationToken)
        ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

    public async Task BeginProcessingAsync(AttendanceSession session, CancellationToken cancellationToken = default)
    {
        if (session.Status == AttendanceSessionStatus.Draft)
        {
            session.MoveToPending();
        }

        session.MoveToProcessing();
        session.StartedUtc = DateTime.UtcNow;
        _workflow.ApplyLocal(session, hasImages: true, force: AttendanceWorkflowStatus.RecognitionRunning);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _workflow.NotifyAsync(session, "RecognitionRunning", cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Attendance session processing started. SessionId={SessionId} TenantId={TenantId}",
            session.Id,
            session.TenantId);
    }

    public async Task CompleteProcessingAsync(
        AttendanceSession session,
        AttendanceSessionStatistics statistics,
        CancellationToken cancellationToken = default)
    {
        session.DetectedFaces = statistics.DetectedFaces;
        session.RecognizedFaces = statistics.StudentsPresent;
        session.UnknownFaces = statistics.UnknownFaces;
        session.RecognizedCount = statistics.StudentsPresent;
        session.UnknownCount = statistics.UnknownFaces;
        session.CompletedUtc = DateTime.UtcNow;
        session.MoveToAwaitingReview();
        _workflow.ApplyLocal(session, hasImages: true, force: AttendanceWorkflowStatus.ReviewPending);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _workflow.NotifyAsync(session, "RecognitionCompleted", cancellationToken: cancellationToken);
        await _workflow.NotifyAsync(session, "ReviewPending", cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Attendance session processing completed. SessionId={SessionId} Present={Present} Unknown={Unknown}",
            session.Id,
            statistics.StudentsPresent,
            statistics.UnknownFaces);
    }

    public async Task FailProcessingAsync(
        AttendanceSession session,
        string error,
        CancellationToken cancellationToken = default)
    {
        session.ProcessingError = error;
        session.MoveToFailed();
        session.CompletedUtc = DateTime.UtcNow;
        _workflow.ApplyLocal(session, hasImages: true, force: AttendanceWorkflowStatus.RecognitionFailed);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _workflow.NotifyAsync(session, "RecognitionFailed", new { error }, cancellationToken);

        _logger.LogWarning(
            "Attendance session processing failed. SessionId={SessionId} Error={Error}",
            session.Id,
            error);
    }

    public AttendanceSessionMetadata CreateMetadata(AttendanceSession session, string? imageStorageKey) =>
        new()
        {
            SessionId = session.Id,
            TenantId = session.TenantId,
            CourseId = session.CourseId,
            GroupId = session.GroupId,
            SemesterId = session.SemesterId,
            SubjectId = session.SubjectId,
            AttendanceDateUtc = session.GetAttendanceDateUtc(),
            ImageStorageKey = imageStorageKey,
        };
}
