using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.AttendanceRecovery;

public interface IAttendanceRecoveryPreferenceService
{
    Task<AttendanceRecoveryPreferenceDto> GetAsync(CancellationToken cancellationToken = default);
    Task<AttendanceRecoveryPreferenceDto> UpsertAsync(UpsertAttendanceRecoveryPreferenceRequest request, CancellationToken cancellationToken = default);
}

public interface IFacultyRecoveryCenterService
{
    Task<FacultyRecoveryCenterDto> GetAsync(string? query = null, CancellationToken cancellationToken = default);
}

public interface IAttendanceOperationsDashboardService
{
    Task<AttendanceOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAttendanceOperationalAnalyticsService
{
    Task<AttendanceOperationalAnalyticsDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAttendanceHealthMonitorService
{
    Task<AttendanceHealthSnapshotDto> ScanAsync(CancellationToken cancellationToken = default);
}

public interface IFacultyWorkspaceRecoverySummaryService
{
    Task<FacultyWorkspaceRecoverySummaryDto> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class AttendanceRecoveryPreferenceService : IAttendanceRecoveryPreferenceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AttendanceRecoveryPreferenceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AttendanceRecoveryPreferenceDto> GetAsync(CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var pref = await FindAsync(cancellationToken);
        return pref is null ? DefaultDto() : Map(pref);
    }

    public async Task<AttendanceRecoveryPreferenceDto> UpsertAsync(
        UpsertAttendanceRecoveryPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var pref = await FindAsync(cancellationToken);
        if (pref is null)
        {
            pref = new AttendanceRecoveryPreference
            {
                TenantId = _currentUser.TenantId,
                StaffId = _currentUser.StaffId,
                UserId = _currentUser.UserId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };
            await _db.AddAsync(pref);
        }

        if (request.AutoSaveFrequencySeconds is int secs)
            pref.AutoSaveFrequencySeconds = Math.Clamp(secs, 5, 600);
        if (request.ResumeConfirmation is bool rc) pref.ResumeConfirmation = rc;
        if (!string.IsNullOrWhiteSpace(request.DefaultLandingPage))
            pref.DefaultLandingPage = request.DefaultLandingPage.Trim();
        if (request.NotificationsEnabled is bool n) pref.NotificationsEnabled = n;
        if (request.SessionTimeoutWarning is bool tw) pref.SessionTimeoutWarning = tw;
        if (request.SessionTimeoutWarningMinutes is int wm)
            pref.SessionTimeoutWarningMinutes = Math.Clamp(wm, 5, 240);
        if (request.PromptOnLogin is bool p) pref.PromptOnLogin = p;

        pref.UpdatedDate = DateTime.UtcNow;
        pref.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(pref);
    }

    private async Task<AttendanceRecoveryPreference?> FindAsync(CancellationToken cancellationToken) =>
        await _db.AttendanceRecoveryPreferences
            .FirstOrDefaultAsync(p =>
                p.TenantId == _currentUser.TenantId &&
                p.StaffId == _currentUser.StaffId &&
                !p.IsDeleted, cancellationToken);

    private AttendanceRecoveryPreferenceDto DefaultDto() => new()
    {
        StaffId = _currentUser.StaffId
    };

    private static AttendanceRecoveryPreferenceDto Map(AttendanceRecoveryPreference p) => new()
    {
        StaffId = p.StaffId,
        AutoSaveFrequencySeconds = p.AutoSaveFrequencySeconds,
        ResumeConfirmation = p.ResumeConfirmation,
        DefaultLandingPage = p.DefaultLandingPage,
        NotificationsEnabled = p.NotificationsEnabled,
        SessionTimeoutWarning = p.SessionTimeoutWarning,
        SessionTimeoutWarningMinutes = p.SessionTimeoutWarningMinutes,
        PromptOnLogin = p.PromptOnLogin
    };

    private void EnsureStaff()
    {
        if (_currentUser.StaffId <= 0 &&
            !_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Staff context is required for recovery preferences.");
    }
}

public sealed class FacultyRecoveryCenterService : IFacultyRecoveryCenterService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AttendanceRecoveryOptions _options;

    public FacultyRecoveryCenterService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<FacultyRecoveryCenterDto> GetAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var isAdmin = _currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        if (_currentUser.StaffId <= 0 && !isAdmin)
            throw new DomainException("Staff context is required for the faculty recovery center.");

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var q = _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId);
        if (_currentUser.StaffId > 0 && !isAdmin)
            q = q.Where(s => s.StaffId == _currentUser.StaffId);

        var sessions = await q
            .OrderByDescending(s => s.LastActivityUtc ?? s.CreatedUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        var mapped = sessions
            .Select(s => AttendanceSessionDisplayEnricher.Map(s, names, expirationHours: _options.DefaultExpirationHours))
            .ToList();
        mapped = AttendanceSessionPriorityEngine.SortByPriority(mapped, s => s.PriorityScore, s => s.LastActivityUtc).ToList();

        IReadOnlyList<PendingAttendanceSessionDto> search = [];
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            search = mapped.Where(s =>
                s.DisplayTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.CourseName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.SessionId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new FacultyRecoveryCenterDto
        {
            TodaysSessions = mapped.Where(s => s.AttendanceDate.Date == today && !IsArchived(s)).ToList(),
            Yesterday = mapped.Where(s => s.AttendanceDate.Date == yesterday && !IsArchived(s)).ToList(),
            NeedsAttention = mapped.Where(s =>
                s.PriorityBand is "Failed" or "NeedsReview" or "ExpiredSoon" || s.CanRetry).ToList(),
            Completed = mapped.Where(s => s.WorkflowStatus == AttendanceWorkflowStatus.AttendanceFinalized ||
                                          s.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed).ToList(),
            Archived = mapped.Where(IsArchived).ToList(),
            SearchResults = search
        };
    }

    private static bool IsArchived(PendingAttendanceSessionDto s) =>
        s.WorkflowStatus is AttendanceWorkflowStatus.Cancelled or AttendanceWorkflowStatus.Expired;
}

public sealed class AttendanceOperationsDashboardService : IAttendanceOperationsDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AttendanceRecoveryOptions _options;

    public AttendanceOperationsDashboardService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<AttendanceOperationsDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var since = DateTime.UtcNow.AddDays(-14);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedUtc >= since)
            .ToListAsync(cancellationToken);

        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        var mapped = sessions
            .Select(s => AttendanceSessionDisplayEnricher.Map(s, names, expirationHours: _options.DefaultExpirationHours))
            .ToList();

        var retries = await _db.AttendanceRetryHistories.AsNoTracking()
            .Where(r => r.TenantId == _currentUser.TenantId && r.PerformedUtc >= since)
            .ToListAsync(cancellationToken);

        var reviewTimes = sessions
            .Where(s => s.StartedUtc.HasValue && s.ApprovedUtc.HasValue)
            .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();

        var finalizedSameDay = sessions.Count(s =>
            s.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed &&
            s.ApprovedUtc.HasValue &&
            s.ApprovedUtc.Value.Date == s.AttendanceDate.Date);
        var attemptedFinalize = sessions.Count(s =>
            s.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.AwaitingReview);

        var staffIds = sessions.Where(s => s.StaffId.HasValue).Select(s => s.StaffId!.Value).Distinct().ToList();
        var staffDepts = await (
            from sd in _db.StaffDepartments.AsNoTracking()
            join d in _db.Departments.AsNoTracking() on sd.DepartmentId equals d.Id
            where staffIds.Contains(sd.StaffId) && !sd.IsDeleted
            select new { sd.StaffId, Dept = d.Name }).ToListAsync(cancellationToken);
        var deptByStaff = staffDepts
            .GroupBy(x => x.StaffId)
            .ToDictionary(g => g.Key, g => g.First().Dept);

        var depts = sessions
            .Select(s => s.StaffId is int sid && deptByStaff.TryGetValue(sid, out var dept) ? dept : "Unassigned")
            .GroupBy(x => x)
            .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(12)
            .ToList();

        var rooms = mapped
            .GroupBy(s => s.CourseName ?? $"Course {s.CourseId}")
            .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(12)
            .ToList();

        return new AttendanceOperationsDashboardDto
        {
            SessionsByStatus = mapped.GroupBy(s => s.FriendlyWorkflowLabel)
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .ToList(),
            LongestRunningSessions = mapped
                .Where(s => s.Status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.Cancelled))
                .OrderByDescending(s => s.ElapsedMinutes)
                .Take(15)
                .ToList(),
            FacultyProductivity = mapped.Where(s => s.StaffId.HasValue)
                .GroupBy(s => s.StaffName ?? $"Staff {s.StaffId}")
                .Select(g => new RecoveryChartPointDto
                {
                    Label = g.Key,
                    Value = g.Count(x => x.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
                })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList(),
            AverageReviewTimeMinutes = reviewTimes.Count == 0 ? null : reviewTimes.Average(),
            RecognitionFailureRatePercent = sessions.Count == 0 ? 0 : 100.0 * sessions.Count(s => s.Status == AttendanceSessionStatus.Failed) / sessions.Count,
            RetrySuccessRatePercent = retries.Count == 0 ? 0 : 100.0 * retries.Count(r => r.Success) / retries.Count,
            FinalizationSlaPercent = attemptedFinalize == 0 ? 100 : 100.0 * finalizedSameDay / attemptedFinalize,
            DepartmentDistribution = depts,
            RoomDistribution = rooms,
            TopBusyFaculty = mapped.Where(s => s.StaffId.HasValue)
                .GroupBy(s => s.StaffName ?? $"Staff {s.StaffId}")
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList()
        };
    }

    private void EnsureAdmin()
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Administrator access required for operations dashboard.");
    }
}

public sealed class AttendanceOperationalAnalyticsService : IAttendanceOperationalAnalyticsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AttendanceOperationalAnalyticsService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AttendanceOperationalAnalyticsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Administrator access required for operational analytics.");

        var since = DateTime.UtcNow.AddDays(-30);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedUtc >= since)
            .ToListAsync(cancellationToken);

        var recognitionMinutes = sessions
            .Where(s => s.StartedUtc.HasValue && s.CompletedUtc.HasValue)
            .Select(s => (s.CompletedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0 && m < 240)
            .ToList();
        var reviewMinutes = sessions
            .Where(s => s.CompletedUtc.HasValue && s.ApprovedUtc.HasValue)
            .Select(s => (s.ApprovedUtc!.Value - s.CompletedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0 && m < 480)
            .ToList();
        var finalizeMinutes = sessions
            .Where(s => s.StartedUtc.HasValue && s.ApprovedUtc.HasValue)
            .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0 && m < 720)
            .ToList();

        var peak = sessions.GroupBy(s => s.CreatedUtc.Hour)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key:00}:00")
            .FirstOrDefault();

        var checkpointResumes = sessions.Count(s => !string.IsNullOrWhiteSpace(s.ResumeCheckpointJson));

        var staffIds = sessions.Where(s => s.StaffId.HasValue).Select(s => s.StaffId!.Value).Distinct().ToList();
        var staffDepts = await (
            from sd in _db.StaffDepartments.AsNoTracking()
            join d in _db.Departments.AsNoTracking() on sd.DepartmentId equals d.Id
            where staffIds.Contains(sd.StaffId) && !sd.IsDeleted
            select new { sd.StaffId, Dept = d.Name }).ToListAsync(cancellationToken);
        var deptByStaff = staffDepts.GroupBy(x => x.StaffId).ToDictionary(g => g.Key, g => g.First().Dept);
        var departmentTrends = sessions
            .Select(s => s.StaffId is int sid && deptByStaff.TryGetValue(sid, out var dept) ? dept : "Unassigned")
            .GroupBy(x => x)
            .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(12)
            .ToList();

        return new AttendanceOperationalAnalyticsDto
        {
            AverageRecognitionMinutes = recognitionMinutes.Count == 0 ? null : recognitionMinutes.Average(),
            AverageReviewMinutes = reviewMinutes.Count == 0 ? null : reviewMinutes.Average(),
            AverageFinalizationMinutes = finalizeMinutes.Count == 0 ? null : finalizeMinutes.Average(),
            SessionsStarted = sessions.Count,
            SessionsCompleted = sessions.Count(s => s.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed),
            RetryPercent = sessions.Count == 0 ? 0 : 100.0 * sessions.Count(s => s.RetryCount > 0) / sessions.Count,
            FailurePercent = sessions.Count == 0 ? 0 : 100.0 * sessions.Count(s => s.Status == AttendanceSessionStatus.Failed) / sessions.Count,
            ResumePercent = sessions.Count == 0 ? 0 : 100.0 * checkpointResumes / sessions.Count,
            PeakUsageLabel = peak,
            DailyTrends = sessions.GroupBy(s => s.CreatedUtc.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
                .ToList(),
            DepartmentTrends = departmentTrends,
            FacultyTrends = sessions.Where(s => s.StaffId.HasValue)
                .GroupBy(s => $"Staff {s.StaffId}")
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .Take(12)
                .ToList()
        };
    }
}

public sealed class AttendanceHealthMonitorService : IAttendanceHealthMonitorService
{
    private readonly IApplicationDbContext _db;
    private readonly IAttendanceRecoveryNotifier _notifier;
    private readonly ILogger<AttendanceHealthMonitorService> _logger;
    private readonly AttendanceRecoveryOptions _options;

    public AttendanceHealthMonitorService(
        IApplicationDbContext db,
        IAttendanceRecoveryNotifier notifier,
        ILogger<AttendanceHealthMonitorService> logger,
        IOptions<AttendanceRecoveryOptions> options)
    {
        _db = db;
        _notifier = notifier;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AttendanceHealthSnapshotDto> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.Status != AttendanceSessionStatus.Approved &&
                        s.Status != AttendanceSessionStatus.Completed &&
                        s.Status != AttendanceSessionStatus.Cancelled)
            .Where(s => s.CreatedUtc >= now.AddDays(-7))
            .Take(500)
            .ToListAsync(cancellationToken);

        var alerts = new List<AttendanceHealthAlertDto>();

        foreach (var s in sessions)
        {
            var w = AttendanceWorkflowMapper.FromSession(s, hasImages: true);
            var last = s.LastActivityUtc ?? s.StartedUtc ?? s.CreatedUtc;
            var idle = (now - last).TotalMinutes;

            if (w == AttendanceWorkflowStatus.RecognitionRunning && idle >= 25)
            {
                alerts.Add(Alert("RecognitionStalled", "critical", s,
                    $"Recognition stalled for {idle:F0} minutes."));
            }

            if (w is AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress && idle >= 120)
            {
                alerts.Add(Alert("ReviewStalled", "warning", s,
                    $"Review stalled for {idle:F0} minutes."));
            }

            if (idle >= _options.DefaultExpirationHours * 60 * 0.75 &&
                w is not (AttendanceWorkflowStatus.Expired or AttendanceWorkflowStatus.Cancelled))
            {
                alerts.Add(Alert("SessionAbandoned", "warning", s,
                    "Session appears abandoned (long idle)."));
            }

            if (s.RetryCount >= 3 ||
                (w is AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed && s.RetryCount >= 2))
            {
                alerts.Add(Alert("RepeatedFailures", "critical", s,
                    $"Repeated failures (retries={s.RetryCount})."));
            }

            if (idle >= 180 && w == AttendanceWorkflowStatus.RecognitionRunning)
            {
                alerts.Add(Alert("LongRunning", "warning", s,
                    $"Long-running recognition ({idle:F0} min)."));
                await _notifier.NotifyAsync(
                    s.TenantId,
                    s.StaffId,
                    AttendanceOpsNotificationCodes.LongRunningSession,
                    new { sessionId = s.Id, idleMinutes = idle },
                    cancellationToken);
            }

            // AI22.8.6.7 — SLA / recognition delay (SignalR, no polling)
            var ageMinutes = Math.Max(0, (now - s.CreatedUtc).TotalMinutes);
            var sla = AttendanceSlaCalculator.Calculate(ageMinutes, expectedRemainingMinutes: 15);
            if (sla.Level == AttendanceSlaLevel.Red)
            {
                alerts.Add(Alert(AttendanceOpsNotificationCodes.SlaBreach, "critical", s,
                    $"SLA breach — session age {ageMinutes:F0} minutes."));
                await _notifier.NotifyAsync(
                    s.TenantId,
                    s.StaffId,
                    AttendanceOpsNotificationCodes.SlaBreach,
                    new { sessionId = s.Id, ageMinutes, slaStatus = sla.SlaStatus },
                    cancellationToken);
                await _notifier.NotifyAsync(
                    s.TenantId,
                    staffId: null,
                    AttendanceOpsNotificationCodes.AdministratorReminder,
                    new { sessionId = s.Id, ageMinutes, adminOnly = true },
                    cancellationToken);
            }
            else if (w == AttendanceWorkflowStatus.RecognitionRunning && ageMinutes >= AttendanceSlaCalculator.YellowMaxMinutes)
            {
                await _notifier.NotifyAsync(
                    s.TenantId,
                    s.StaffId,
                    AttendanceOpsNotificationCodes.RecognitionDelayed,
                    new { sessionId = s.Id, ageMinutes },
                    cancellationToken);
            }
        }

        var pendingByStaff = sessions.GroupBy(s => s.StaffId ?? 0)
            .Where(g => g.Count() >= 8)
            .ToList();
        foreach (var g in pendingByStaff)
        {
            alerts.Add(new AttendanceHealthAlertDto
            {
                Code = "LargePendingQueue",
                Severity = "warning",
                Message = $"Staff {g.Key} has {g.Count()} pending sessions.",
                StaffId = g.Key == 0 ? null : g.Key,
                DetectedUtc = now
            });
        }

        // Administrator alerts only — publish to tenant group with staffId null.
        foreach (var group in alerts.GroupBy(a => a.Code))
        {
            var tenants = sessions.Select(s => s.TenantId).Distinct();
            foreach (var tenantId in tenants)
            {
                await _notifier.NotifyAsync(
                    tenantId,
                    staffId: null,
                    eventName: "AttendanceHealthAlert",
                    payload: new
                    {
                        code = group.Key,
                        count = group.Count(),
                        severity = group.First().Severity,
                        adminOnly = true,
                        neverAutoCancels = true
                    },
                    cancellationToken);
            }
        }

        _logger.LogInformation("AI22.8.5 health scan produced {Count} alerts (never auto-cancels).", alerts.Count);

        return new AttendanceHealthSnapshotDto
        {
            Alerts = alerts,
            RecognitionStalled = alerts.Count(a => a.Code == "RecognitionStalled"),
            ReviewStalled = alerts.Count(a => a.Code == "ReviewStalled"),
            Abandoned = alerts.Count(a => a.Code == "SessionAbandoned"),
            RepeatedFailures = alerts.Count(a => a.Code == "RepeatedFailures"),
            LargePendingQueues = alerts.Count(a => a.Code == "LargePendingQueue"),
            LongRunning = alerts.Count(a => a.Code == "LongRunning")
        };
    }

    private static AttendanceHealthAlertDto Alert(string code, string severity, AttendanceSession s, string message) => new()
    {
        Code = code,
        Severity = severity,
        Message = message,
        SessionId = s.Id,
        StaffId = s.StaffId,
        DetectedUtc = DateTime.UtcNow
    };
}

public sealed class FacultyWorkspaceRecoverySummaryService : IFacultyWorkspaceRecoverySummaryService
{
    private readonly IPendingSessionQueueService _queue;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FacultyWorkspaceRecoverySummaryService(
        IPendingSessionQueueService queue,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _queue = queue;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FacultyWorkspaceRecoverySummaryDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var queue = await _queue.GetQueueAsync(new PendingSessionQueueRequest { SortBy = "priority" }, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var isAdmin = _currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var completedQ = _db.AttendanceSessions.AsNoTracking()
            .Where(s =>
                s.TenantId == _currentUser.TenantId &&
                s.AttendanceDate.Date == today &&
                (s.Status == AttendanceSessionStatus.Approved || s.Status == AttendanceSessionStatus.Completed));
        var classesQ = _db.AttendanceSessions.AsNoTracking()
            .Where(s =>
                s.TenantId == _currentUser.TenantId &&
                s.AttendanceDate.Date == today);
        if (_currentUser.StaffId > 0 && !isAdmin)
        {
            completedQ = completedQ.Where(s => s.StaffId == _currentUser.StaffId);
            classesQ = classesQ.Where(s => s.StaffId == _currentUser.StaffId);
        }

        var completed = await completedQ.CountAsync(cancellationToken);
        var classes = await classesQ.CountAsync(cancellationToken);

        var reviewSessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s =>
                s.TenantId == _currentUser.TenantId &&
                s.AttendanceDate.Date == today &&
                s.StartedUtc.HasValue &&
                s.ApprovedUtc.HasValue &&
                (_currentUser.StaffId <= 0 || isAdmin || s.StaffId == _currentUser.StaffId))
            .Select(s => new { s.StartedUtc, s.ApprovedUtc })
            .ToListAsync(cancellationToken);
        var reviewMinutes = reviewSessions
            .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0 && m < 720)
            .ToList();

        return new FacultyWorkspaceRecoverySummaryDto
        {
            TodaysClasses = classes,
            PendingAttendance = queue.Total,
            NeedsReview = queue.NeedsReviewCount,
            RecognitionRunning = queue.RecognitionRunningCount,
            Completed = completed,
            CompletedToday = completed,
            AverageReviewTimeMinutes = reviewMinutes.Count == 0 ? null : reviewMinutes.Average(),
            PendingByPriority = queue.Items
                .GroupBy(i => i.PriorityBand)
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .ToList(),
            SlaDistribution = queue.Items
                .GroupBy(i => i.SlaLevel)
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .ToList(),
            TopPending = queue.Items.Take(5).ToList()
        };
    }
}
