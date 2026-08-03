using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AuditAction = Abhyanvaya.Domain.Enums.AuditAction;

namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>AI22.8.5.1 — centralized pending session queue with filters/sorting.</summary>
public interface IPendingSessionQueueService
{
    Task<PendingSessionQueueDto> GetQueueAsync(PendingSessionQueueRequest? request = null, CancellationToken cancellationToken = default);
    Task CancelSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class PendingSessionQueueService : IPendingSessionQueueService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceWorkflowLifecycleService _workflow;
    private readonly IAuditService _audit;
    private readonly IAttendanceRecoveryNotifier _notifier;
    private readonly AttendanceRecoveryOptions _options;

    public PendingSessionQueueService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAttendanceWorkflowLifecycleService workflow,
        IAuditService audit,
        IAttendanceRecoveryNotifier notifier,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _workflow = workflow;
        _audit = audit;
        _notifier = notifier;
        _options = options.Value;
    }

    public async Task<PendingSessionQueueDto> GetQueueAsync(
        PendingSessionQueueRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = _currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        if (_currentUser.StaffId <= 0 && !isAdmin)
            throw new DomainException("Staff context is required for the pending session queue.");

        request ??= new PendingSessionQueueRequest();
        var q = _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Where(s => s.Status != AttendanceSessionStatus.Approved &&
                        s.Status != AttendanceSessionStatus.Completed &&
                        s.Status != AttendanceSessionStatus.Cancelled);

        // Faculty sees own sessions; Admin (often StaffId=0) sees tenant-wide pending queue.
        if (_currentUser.StaffId > 0 && !isAdmin)
            q = q.Where(s => s.StaffId == _currentUser.StaffId);

        var sessions = await q
            .OrderByDescending(s => s.LastActivityUtc ?? s.StartedUtc ?? s.CreatedUtc)
            .Take(150)
            .ToListAsync(cancellationToken);

        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        var mapped = sessions
            .Select(s => AttendanceSessionDisplayEnricher.Map(s, names, expirationHours: _options.DefaultExpirationHours))
            .ToList();

        mapped = ApplyFilters(mapped, request);
        mapped = request.SortBy?.ToLowerInvariant() switch
        {
            "time" => mapped.OrderBy(s => s.ScheduledTimeLabel).ThenByDescending(s => s.PriorityScore).ToList(),
            "subject" => mapped.OrderBy(s => s.DisplayTitle).ThenByDescending(s => s.PriorityScore).ToList(),
            "age" => mapped.OrderByDescending(s => s.AgeMinutes).ToList(),
            "activity" => mapped.OrderByDescending(s => s.LastActivityUtc).ToList(),
            _ => AttendanceSessionPriorityEngine.SortByPriority(mapped, s => s.PriorityScore, s => s.LastActivityUtc).ToList()
        };

        return new PendingSessionQueueDto
        {
            Items = mapped,
            Total = mapped.Count,
            FailedCount = mapped.Count(s => s.PriorityBand == "Failed"),
            NeedsReviewCount = mapped.Count(s => s.PriorityBand == "NeedsReview"),
            RecognitionReadyCount = mapped.Count(s => s.PriorityBand == "RecognitionReady"),
            RecognitionRunningCount = mapped.Count(s => s.PriorityBand == "RecognitionRunning"),
            SortedByPriority = string.IsNullOrWhiteSpace(request.SortBy) ||
                               request.SortBy.Equals("priority", StringComparison.OrdinalIgnoreCase)
        };
    }

    public async Task CancelSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var isAdmin = _currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        if (_currentUser.StaffId <= 0 && !isAdmin)
            throw new DomainException("Staff context is required to cancel a recovery session.");

        var sessionQuery = _db.AttendanceSessions.Where(s =>
            s.Id == sessionId && s.TenantId == _currentUser.TenantId);
        if (_currentUser.StaffId > 0 && !isAdmin)
            sessionQuery = sessionQuery.Where(s => s.StaffId == _currentUser.StaffId);

        var session = await sessionQuery.FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

        if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
            throw new DomainException("Finalized sessions cannot be cancelled from recovery.");

        session.Cancel();
        _workflow.ApplyLocal(session, force: AttendanceWorkflowStatus.Cancelled);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(
            nameof(AttendanceSession),
            session.Id.ToString(),
            AuditAction.Custom,
            null,
            "{\"action\":\"FacultyRecoveryCancel\"}",
            cancellationToken);
        await _notifier.NotifyAsync(
            session.TenantId,
            session.StaffId,
            "AttendanceSessionCancelled",
            new { sessionId = session.Id },
            cancellationToken);
    }

    private static List<PendingAttendanceSessionDto> ApplyFilters(
        List<PendingAttendanceSessionDto> items,
        PendingSessionQueueRequest request)
    {
        IEnumerable<PendingAttendanceSessionDto> q = items;
        if (!string.IsNullOrWhiteSpace(request.WorkflowStatus) &&
            Enum.TryParse<AttendanceWorkflowStatus>(request.WorkflowStatus, true, out var wf))
            q = q.Where(s => s.WorkflowStatus == wf);
        if (!string.IsNullOrWhiteSpace(request.PriorityBand))
            q = q.Where(s => s.PriorityBand.Equals(request.PriorityBand, StringComparison.OrdinalIgnoreCase));
        if (request.OnlyFailed == true)
            q = q.Where(s => s.CanRetry);
        if (request.OnlyNeedsReview == true)
            q = q.Where(s => s.PriorityBand == "NeedsReview");
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim();
            q = q.Where(s =>
                s.DisplayTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.CourseName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.SessionId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return q.ToList();
    }
}
