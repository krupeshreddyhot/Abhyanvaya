using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.AttendanceRecovery;

public interface IDepartmentOperationsService
{
    Task<DepartmentOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface ISessionTimelineService
{
    Task<SessionTimelineDto> GetAsync(Guid sessionId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}

public interface IBulkOperationService
{
    Task<BulkOperationResultDto> ExecuteAsync(BulkOperationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BulkOperationHistoryDto>> GetHistoryAsync(int take = 50, CancellationToken cancellationToken = default);
}

public interface IEnterpriseOpsDashboardService
{
    Task<EnterpriseOpsDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>AI22.8.6.2 — Catalog Department operations summary (no Scheduling/Attendance Department master).</summary>
public sealed class DepartmentOperationsService : IDepartmentOperationsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;
    private readonly AttendanceRecoveryOptions _options;

    public DepartmentOperationsService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IMemoryCache cache,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<DepartmentOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var cacheKey = $"ai2286:dept-ops:{_currentUser.TenantId}";
        if (_cache.TryGetValue(cacheKey, out DepartmentOperationsDashboardDto? cached) && cached is not null)
            return cached;

        var since = DateTime.UtcNow.AddDays(-14);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedUtc >= since)
            .ToListAsync(cancellationToken);

        var staffIds = sessions.Where(s => s.StaffId.HasValue).Select(s => s.StaffId!.Value).Distinct().ToList();
        var staffDepts = await (
            from sd in _db.StaffDepartments.AsNoTracking()
            join d in _db.Departments.AsNoTracking() on sd.DepartmentId equals d.Id
            where staffIds.Contains(sd.StaffId) && !sd.IsDeleted
            select new { sd.StaffId, d.Id, d.Name, d.Code }).ToListAsync(cancellationToken);

        var deptByStaff = staffDepts
            .GroupBy(x => x.StaffId)
            .ToDictionary(g => g.Key, g => g.First());

        var facultyByDept = staffDepts
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StaffId).Distinct().Count());

        var groups = sessions.GroupBy(s =>
        {
            if (s.StaffId is int sid && deptByStaff.TryGetValue(sid, out var d))
                return (Id: d.Id, Name: d.Name, Code: d.Code);
            return (Id: 0, Name: "Unassigned", Code: (string?)null);
        });

        var departments = groups.Select(g =>
        {
            var mapped = g.Select(s => AttendanceSessionDisplayEnricher.Map(s, expirationHours: _options.DefaultExpirationHours)).ToList();
            var completion = g.Where(s => s.StartedUtc.HasValue && s.ApprovedUtc.HasValue)
                .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
                .Where(m => m >= 0 && m < 720).ToList();
            var recognition = g.Where(s => s.StartedUtc.HasValue && s.CompletedUtc.HasValue)
                .Select(s => (s.CompletedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
                .Where(m => m >= 0 && m < 240).ToList();

            return new DepartmentOperationsSummaryDto
            {
                DepartmentId = g.Key.Id,
                DepartmentName = g.Key.Name,
                DepartmentCode = g.Key.Code,
                PendingSessions = mapped.Count(x =>
                    x.Status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.Cancelled)),
                Completed = mapped.Count(x => x.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed),
                Failed = mapped.Count(x => x.Status == AttendanceSessionStatus.Failed || x.PriorityBand == "Failed"),
                RecognitionRunning = mapped.Count(x => x.PriorityBand == "RecognitionRunning"),
                NeedsReview = mapped.Count(x => x.PriorityBand == "NeedsReview"),
                AverageCompletionMinutes = completion.Count == 0 ? null : completion.Average(),
                AverageRecognitionMinutes = recognition.Count == 0 ? null : recognition.Average(),
                FacultyCount = g.Key.Id == 0 ? staffIds.Count(id => !deptByStaff.ContainsKey(id))
                    : facultyByDept.GetValueOrDefault(g.Key.Id)
            };
        })
        .OrderByDescending(d => d.PendingSessions)
        .ThenBy(d => d.DepartmentName)
        .ToList();

        var pendingTrend = sessions
            .GroupBy(s => s.CreatedUtc.Date)
            .OrderBy(g => g.Key)
            .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
            .ToList();
        var completionTrend = sessions
            .Where(s => s.ApprovedUtc.HasValue)
            .GroupBy(s => s.ApprovedUtc!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
            .ToList();

        var result = new DepartmentOperationsDashboardDto
        {
            Departments = departments,
            PendingTrend = pendingTrend,
            CompletionTrend = completionTrend
        };
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(45));
        return result;
    }

    private void EnsureAdmin()
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Administrator access required for department operations.");
    }
}

/// <summary>AI22.8.6.3 — session timeline from workflow timestamps + AttendanceRetryHistory (no second audit model).</summary>
public sealed class SessionTimelineService : ISessionTimelineService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SessionTimelineService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SessionTimelineDto> GetAsync(
        Guid sessionId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var session = await _db.AttendanceSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

        if (_currentUser.StaffId > 0 &&
            !_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
            session.StaffId != _currentUser.StaffId)
        {
            throw new DomainException("You can only view timeline for your own sessions.");
        }

        var events = new List<SessionTimelineEventDto>();
        void AddLifecycle(string op, DateTime? when, string? reason = null, int? userId = null)
        {
            if (!when.HasValue) return;
            events.Add(new SessionTimelineEventDto
            {
                Operation = op,
                OccurredUtc = when.Value,
                RelativeTime = ToRelative(when.Value),
                UserId = userId ?? session.CreatedBy,
                Reason = reason,
                Success = true,
                Source = "workflow"
            });
        }

        AddLifecycle("Created", session.CreatedUtc, userId: session.CreatedBy);
        if (session.StartedUtc.HasValue || session.Status != AttendanceSessionStatus.Pending)
            AddLifecycle("Images Uploaded", session.StartedUtc ?? session.CreatedUtc);
        if (session.StartedUtc.HasValue)
            AddLifecycle("Recognition Started", session.StartedUtc);
        if (session.CompletedUtc.HasValue)
            AddLifecycle("Recognition Completed", session.CompletedUtc);
        if (session.Status is AttendanceSessionStatus.AwaitingReview or AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
            AddLifecycle("Review Started", session.CompletedUtc ?? session.LastActivityUtc);
        if (session.WorkflowStatus == AttendanceWorkflowStatus.Expired)
            AddLifecycle("Paused", session.WorkflowExpiredUtc ?? session.LastActivityUtc, "Expired / paused");
        if (session.RetryCount > 0)
            AddLifecycle("Retry", session.LastActivityUtc, $"RetryCount={session.RetryCount}");
        if (session.ApprovedUtc.HasValue)
            AddLifecycle("Finalized", session.ApprovedUtc);
        if (session.WorkflowStatus is AttendanceWorkflowStatus.Cancelled or AttendanceWorkflowStatus.Expired)
            AddLifecycle("Archived", session.WorkflowExpiredUtc ?? session.LastActivityUtc);

        var history = await _db.AttendanceRetryHistories.AsNoTracking()
            .Where(h => h.TenantId == _currentUser.TenantId && h.AttendanceSessionId == sessionId)
            .OrderBy(h => h.PerformedUtc)
            .ToListAsync(cancellationToken);

        foreach (var h in history)
        {
            events.Add(new SessionTimelineEventDto
            {
                Operation = string.IsNullOrWhiteSpace(h.Action) ? h.Stage : h.Action,
                OccurredUtc = h.PerformedUtc,
                RelativeTime = ToRelative(h.PerformedUtc),
                UserId = h.PerformedBy,
                Reason = h.ErrorMessage,
                Success = h.Success,
                Source = "retry-history"
            });
        }

        var ordered = events.OrderBy(e => e.OccurredUtc).ThenBy(e => e.Operation).ToList();
        var total = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new SessionTimelineDto
        {
            SessionId = sessionId,
            Events = pageItems,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static string ToRelative(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}

/// <summary>
/// AI22.8.6.4 — administrator bulk assist tools. Never auto-finalizes attendance or retries successful sessions.
/// </summary>
public sealed class BulkOperationService : IBulkOperationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceRetryService _retry;
    private readonly IAttendanceRecoveryDashboardService _dashboard;
    private readonly IAttendanceRecoveryNotifier _notifier;
    private readonly IAuditService _audit;
    private readonly ILogger<BulkOperationService> _logger;

    public BulkOperationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAttendanceRetryService retry,
        IAttendanceRecoveryDashboardService dashboard,
        IAttendanceRecoveryNotifier notifier,
        IAuditService audit,
        ILogger<BulkOperationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _retry = retry;
        _dashboard = dashboard;
        _notifier = notifier;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BulkOperationResultDto> ExecuteAsync(BulkOperationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var ids = (request.SessionIds ?? []).Distinct().Take(200).ToList();
        if (ids.Count == 0)
            throw new DomainException("Select at least one session for bulk operation.");

        var opId = Guid.NewGuid();
        var started = DateTime.UtcNow;
        var items = new List<BulkOperationItemResultDto>();
        var sessions = await _db.AttendanceSessions
            .Where(s => s.TenantId == _currentUser.TenantId && ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            var session = sessions.FirstOrDefault(s => s.Id == id);
            if (session is null)
            {
                items.Add(new BulkOperationItemResultDto { SessionId = id, Success = false, Message = "Not found" });
                continue;
            }

            try
            {
                items.Add(await ApplyAsync(session, request, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk op {Op} failed for session {SessionId}", request.Operation, id);
                items.Add(new BulkOperationItemResultDto { SessionId = id, Success = false, Message = ex.Message });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var result = new BulkOperationResultDto
        {
            OperationId = opId,
            Operation = request.Operation.ToString(),
            RequestedCount = ids.Count,
            SucceededCount = items.Count(i => i.Success && !i.Skipped),
            SkippedCount = items.Count(i => i.Skipped),
            FailedCount = items.Count(i => !i.Success && !i.Skipped),
            Items = items
        };

        var history = new AttendanceBulkOperationHistory
        {
            Id = opId,
            TenantId = _currentUser.TenantId,
            Operation = result.Operation,
            RequestedCount = result.RequestedCount,
            SucceededCount = result.SucceededCount,
            SkippedCount = result.SkippedCount,
            FailedCount = result.FailedCount,
            SessionIdsJson = JsonSerializer.Serialize(ids),
            ResultJson = JsonSerializer.Serialize(items.Select(i => new { i.SessionId, i.Success, i.Skipped, i.Message })),
            Reason = request.Reason,
            PerformedBy = _currentUser.UserId,
            StartedUtc = started,
            CompletedUtc = DateTime.UtcNow
        };
        await _db.AddAsync(history);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            nameof(AttendanceBulkOperationHistory),
            opId.ToString(),
            AuditAction.Custom,
            null,
            JsonSerializer.Serialize(new { result.Operation, result.SucceededCount, result.SkippedCount, result.FailedCount, request.Reason }),
            cancellationToken);

        await _notifier.NotifyAsync(
            _currentUser.TenantId,
            staffId: null,
            eventName: "BulkOperationCompleted",
            payload: new
            {
                result.OperationId,
                result.Operation,
                result.SucceededCount,
                result.FailedCount,
                result.SkippedCount,
                adminOnly = true
            },
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<BulkOperationHistoryDto>> GetHistoryAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        take = Math.Clamp(take, 1, 200);
        var rows = await _db.AttendanceBulkOperationHistories.AsNoTracking()
            .Where(h => h.TenantId == _currentUser.TenantId)
            .OrderByDescending(h => h.StartedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(h => new BulkOperationHistoryDto
        {
            Id = h.Id,
            Operation = h.Operation,
            RequestedCount = h.RequestedCount,
            SucceededCount = h.SucceededCount,
            SkippedCount = h.SkippedCount,
            FailedCount = h.FailedCount,
            Reason = h.Reason,
            PerformedBy = h.PerformedBy,
            StartedUtc = h.StartedUtc,
            CompletedUtc = h.CompletedUtc
        }).ToList();
    }

    private async Task<BulkOperationItemResultDto> ApplyAsync(
        AttendanceSession session,
        BulkOperationRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Operation)
        {
            case AttendanceBulkOperationKind.NotifyFaculty:
                if (!session.StaffId.HasValue)
                    return Skip(session.Id, "No faculty assigned");
                await _notifier.NotifyAsync(
                    session.TenantId,
                    session.StaffId,
                    "FacultyReminder",
                    new { session.Id, message = request.Reason ?? "Please complete pending attendance.", sla = true },
                    cancellationToken);
                return Ok(session.Id, "Faculty notified");

            case AttendanceBulkOperationKind.ArchiveExpired:
                if (session.WorkflowStatus != AttendanceWorkflowStatus.Expired && !session.WorkflowExpiredUtc.HasValue)
                    return Skip(session.Id, "Not expired");
                await _dashboard.AdminActionAsync(session.Id, new AdminSessionActionRequest { Action = "archive", Reason = request.Reason }, cancellationToken);
                return Ok(session.Id, "Archived");

            case AttendanceBulkOperationKind.ExportSessions:
                return Ok(session.Id, "Included in export set");

            case AttendanceBulkOperationKind.RetryFailedRecognition:
                if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
                    return Skip(session.Id, "Never retry successful sessions");
                if (session.Status != AttendanceSessionStatus.Failed &&
                    session.WorkflowStatus is not (AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed))
                    return Skip(session.Id, "Not a failed recognition session");
                var retry = await _retry.RetryAsync(
                    session.Id,
                    new AttendanceRetryRequest { Kind = AttendanceRetryKind.RetryRecognition },
                    cancellationToken);
                return retry.Success
                    ? Ok(session.Id, retry.Message ?? "Retry queued")
                    : Fail(session.Id, retry.Message ?? "Retry failed");

            case AttendanceBulkOperationKind.MarkReviewed:
                if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
                    return Skip(session.Id, "Already completed");
                if (session.WorkflowStatus is AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress
                    or AttendanceWorkflowStatus.ReadyForFinalization or AttendanceWorkflowStatus.RecognitionCompleted)
                {
                    session.LastActivityUtc = DateTime.UtcNow;
                    // Assistive flag only — does not finalize attendance rows.
                    if (session.WorkflowStatus == AttendanceWorkflowStatus.RecognitionCompleted)
                        session.WorkflowStatus = AttendanceWorkflowStatus.ReviewPending;
                    return Ok(session.Id, "Marked for faculty review (not finalized)");
                }
                return Skip(session.Id, "Not in reviewable state");

            case AttendanceBulkOperationKind.CloseCompleted:
                if (session.Status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed))
                    return Skip(session.Id, "Only closes already-completed sessions");
                session.LastActivityUtc = DateTime.UtcNow;
                return Ok(session.Id, "Closed (already finalized — no attendance rewrite)");

            default:
                return Fail(session.Id, "Unsupported operation");
        }
    }

    private static BulkOperationItemResultDto Ok(Guid id, string message) =>
        new() { SessionId = id, Success = true, Message = message };

    private static BulkOperationItemResultDto Skip(Guid id, string message) =>
        new() { SessionId = id, Success = true, Skipped = true, Message = message };

    private static BulkOperationItemResultDto Fail(Guid id, string message) =>
        new() { SessionId = id, Success = false, Message = message };

    private void EnsureAdmin()
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Bulk operations are administrator-only.");
    }
}

/// <summary>AI22.8.6.5 — admin enterprise ops dashboard enhancements (aggregates existing analytics).</summary>
public sealed class EnterpriseOpsDashboardService : IEnterpriseOpsDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDepartmentOperationsService _departments;
    private readonly IMemoryCache _cache;
    private readonly AttendanceRecoveryOptions _options;

    public EnterpriseOpsDashboardService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDepartmentOperationsService departments,
        IMemoryCache cache,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _departments = departments;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<EnterpriseOpsDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Administrator access required.");

        var cacheKey = $"ai2286:enterprise-ops:{_currentUser.TenantId}";
        if (_cache.TryGetValue(cacheKey, out EnterpriseOpsDashboardDto? cached) && cached is not null)
            return cached;

        var since = DateTime.UtcNow.AddDays(-14);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedUtc >= since)
            .ToListAsync(cancellationToken);
        var mapped = sessions
            .Select(s => AttendanceSessionDisplayEnricher.Map(s, expirationHours: _options.DefaultExpirationHours))
            .ToList();
        var retries = await _db.AttendanceRetryHistories.AsNoTracking()
            .Where(r => r.TenantId == _currentUser.TenantId && r.PerformedUtc >= since)
            .ToListAsync(cancellationToken);
        var deptDash = await _departments.GetAsync(cancellationToken);

        var reviewTimes = sessions
            .Where(s => s.StartedUtc.HasValue && s.ApprovedUtc.HasValue)
            .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0 && m < 720)
            .ToList();

        var result = new EnterpriseOpsDashboardDto
        {
            SlaDistribution = mapped
                .GroupBy(s => s.SlaLevel)
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderBy(x => x.Label)
                .ToList(),
            DepartmentSummary = deptDash.Departments,
            TopDelayedSessions = mapped
                .Where(s => s.Status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.Cancelled))
                .OrderByDescending(s => s.AgeMinutes)
                .Take(15)
                .ToList(),
            FacultySla = mapped.Where(s => s.StaffId.HasValue)
                .GroupBy(s => s.StaffName ?? $"Staff {s.StaffId}")
                .Select(g => new RecoveryChartPointDto
                {
                    Label = g.Key,
                    Value = g.Count(x => x.SlaLevel is "Orange" or "Red")
                })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList(),
            AverageReviewTimeMinutes = reviewTimes.Count == 0 ? null : reviewTimes.Average(),
            TimelineTrends = retries
                .GroupBy(r => r.PerformedUtc.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
                .ToList(),
            RetrySuccessPercent = retries.Count == 0 ? 0 : 100.0 * retries.Count(r => r.Success) / retries.Count,
            FailureTrend = sessions.Where(s => s.Status == AttendanceSessionStatus.Failed)
                .GroupBy(s => s.CreatedUtc.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
                .ToList(),
            DailyHeatmap = mapped
                .GroupBy(s => (s.StartedUtc ?? s.LastActivityUtc ?? DateTime.UtcNow).ToString("HH:00"))
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderBy(x => x.Label)
                .ToList(),
            DepartmentHeatmap = deptDash.Departments
                .Select(d => new RecoveryChartPointDto { Label = d.DepartmentName, Value = d.PendingSessions + d.NeedsReview })
                .OrderByDescending(x => x.Value)
                .Take(16)
                .ToList()
        };

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(45));
        return result;
    }
}

/// <summary>AI22.8.6.7 — SLA / delay SignalR helpers (reuses IAttendanceRecoveryNotifier).</summary>
public static class AttendanceOpsNotificationCodes
{
    public const string SlaBreach = "SlaBreach";
    public const string RecognitionDelayed = "RecognitionDelayed";
    public const string LongRunningSession = "LongRunningSession";
    public const string DepartmentAlert = "DepartmentAlert";
    public const string BulkOperationCompleted = "BulkOperationCompleted";
    public const string FacultyReminder = "FacultyReminder";
    public const string AdministratorReminder = "AdministratorReminder";
}
