using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.AttendanceRecovery;

public sealed class AttendanceRecoveryOptions
{
    public const string SectionName = "AttendanceRecovery";
    public int DefaultExpirationHours { get; set; } = 48;
    public int[] AllowedExpirationHours { get; set; } = [24, 48, 72];
    public bool ExpirationCleanupEnabled { get; set; } = true;
    public int CleanupScanIntervalMinutes { get; set; } = 30;
}

public interface IAttendanceRecoveryNotifier
{
    Task NotifyAsync(
        int tenantId,
        int? staffId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}

public sealed class NoOpAttendanceRecoveryNotifier : IAttendanceRecoveryNotifier
{
    public Task NotifyAsync(int tenantId, int? staffId, string eventName, object payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public interface IPendingAttendanceService
{
    Task<PendingAttendanceBucketDto> GetPendingAsync(CancellationToken cancellationToken = default);
}

public interface IAttendanceResumeService
{
    Task<AttendanceResumeCheckpointDto> GetResumeAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AttendanceResumeCheckpointDto> SaveCheckpointAsync(Guid sessionId, SaveResumeCheckpointRequest request, CancellationToken cancellationToken = default);
    Task<AutoResumePromptDto> GetAutoResumePromptAsync(CancellationToken cancellationToken = default);
    Task DecideAutoResumeAsync(AutoResumeDecisionRequest request, CancellationToken cancellationToken = default);
}

public interface IAttendanceRetryService
{
    Task<AttendanceRetryResultDto> RetryAsync(Guid sessionId, AttendanceRetryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRetryHistoryDto>> GetHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public interface IAttendanceRecoverySearchService
{
    Task<IReadOnlyList<PendingAttendanceSessionDto>> SearchAsync(AttendanceRecoverySearchRequest request, CancellationToken cancellationToken = default);
}

public interface IAttendanceRecoveryDashboardService
{
    Task<AttendanceRecoveryDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<AttendanceRecoveryAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);
    Task AdminActionAsync(Guid sessionId, AdminSessionActionRequest request, CancellationToken cancellationToken = default);
}

public interface IAttendanceExpirationService
{
    ExpirationOptionsDto GetOptions();
    Task<int> ExpireStaleSessionsAsync(CancellationToken cancellationToken = default);
}

internal static class RecoveryResumeToken
{
    public static string For(Guid sessionId) => Convert.ToBase64String(sessionId.ToByteArray());
}

public sealed class PendingAttendanceService : IPendingAttendanceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PendingAttendanceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PendingAttendanceBucketDto> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var today = DateTime.UtcNow.Date;
        var sessions = await LoadFacultySessionsAsync(cancellationToken);
        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        var mapped = AttendanceSessionPriorityEngine.SortByPriority(
            sessions.Select(s => AttendanceSessionDisplayEnricher.Map(s, names)),
            s => s.PriorityScore,
            s => s.LastActivityUtc).ToList();

        return new PendingAttendanceBucketDto
        {
            MyPendingSessions = mapped.Where(s => !IsTerminal(s.WorkflowStatus)).ToList(),
            TodaysPending = mapped.Where(s => s.AttendanceDate.Date == today && !IsTerminal(s.WorkflowStatus)).ToList(),
            ReviewPending = mapped.Where(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress).ToList(),
            RecognitionRunning = mapped.Where(s => s.WorkflowStatus == AttendanceWorkflowStatus.RecognitionRunning).ToList(),
            FailedSessions = mapped.Where(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed).ToList(),
            ReadyToFinalize = mapped.Where(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.ReadyForFinalization or AttendanceWorkflowStatus.RecognitionCompleted).ToList(),
            TotalPending = mapped.Count(s => !IsTerminal(s.WorkflowStatus))
        };
    }

    private async Task<List<AttendanceSession>> LoadFacultySessionsAsync(CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var q = _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Where(s => s.Status != AttendanceSessionStatus.Approved &&
                        s.Status != AttendanceSessionStatus.Completed &&
                        s.Status != AttendanceSessionStatus.Cancelled);

        if (_currentUser.StaffId > 0 && !isAdmin)
            q = q.Where(s => s.StaffId == _currentUser.StaffId);

        return await q
            .OrderByDescending(s => s.LastActivityUtc ?? s.StartedUtc ?? s.CreatedUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private static bool IsTerminal(AttendanceWorkflowStatus w) =>
        w is AttendanceWorkflowStatus.AttendanceFinalized or AttendanceWorkflowStatus.Cancelled or AttendanceWorkflowStatus.Expired;

    private void EnsureStaff()
    {
        // Admin may open Faculty Workspace without a StaffId; allow tenant-scoped pending.
        if (_currentUser.StaffId <= 0 &&
            !_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Staff context is required for pending attendance.");
    }
}

public sealed class AttendanceResumeService : IAttendanceResumeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspacePreferenceService _prefs;
    private readonly IPendingAttendanceService _pending;
    private readonly IAuditService _audit;

    public AttendanceResumeService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IWorkspacePreferenceService prefs,
        IPendingAttendanceService pending,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _prefs = prefs;
        _pending = pending;
        _audit = audit;
    }

    public async Task<AttendanceResumeCheckpointDto> GetResumeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await RequireOwnedSessionAsync(sessionId, track: false, cancellationToken);
        if (session.WorkflowExpiredUtc.HasValue)
            throw new DomainException("Expired sessions cannot be resumed for finalization. Ask an administrator to restore.");

        AttendanceResumeCheckpointDto? checkpoint = null;
        if (!string.IsNullOrWhiteSpace(session.ResumeCheckpointJson))
            checkpoint = JsonSerializer.Deserialize<AttendanceResumeCheckpointDto>(session.ResumeCheckpointJson, JsonOptions);

        var workflow = AttendanceWorkflowMapper.FromSession(session, hasImages: true);
        return new AttendanceResumeCheckpointDto
        {
            SessionId = session.Id,
            CurrentImageId = checkpoint?.CurrentImageId,
            Zoom = checkpoint?.Zoom,
            FiltersJson = checkpoint?.FiltersJson,
            CurrentStudentId = checkpoint?.CurrentStudentId,
            ReviewPosition = checkpoint?.ReviewPosition,
            CurrentBatchId = checkpoint?.CurrentBatchId,
            ResumePath = AttendanceWorkflowMapper.ResumePath(session.Id, workflow),
            WorkflowStatus = workflow
        };
    }

    public async Task<AttendanceResumeCheckpointDto> SaveCheckpointAsync(
        Guid sessionId,
        SaveResumeCheckpointRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireOwnedSessionAsync(sessionId, track: true, cancellationToken);
        var workflow = AttendanceWorkflowMapper.FromSession(session, hasImages: true, reviewStarted: true);
        if (workflow == AttendanceWorkflowStatus.ReviewPending)
            workflow = AttendanceWorkflowStatus.ReviewInProgress;

        var dto = new AttendanceResumeCheckpointDto
        {
            SessionId = session.Id,
            CurrentImageId = request.CurrentImageId,
            Zoom = request.Zoom,
            FiltersJson = request.FiltersJson,
            CurrentStudentId = request.CurrentStudentId,
            ReviewPosition = request.ReviewPosition,
            CurrentBatchId = request.CurrentBatchId,
            ResumePath = AttendanceWorkflowMapper.ResumePath(session.Id, workflow),
            WorkflowStatus = workflow
        };

        session.ResumeCheckpointJson = JsonSerializer.Serialize(dto, JsonOptions);
        session.LastActivityUtc = DateTime.UtcNow;
        session.WorkflowStatus = workflow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            nameof(AttendanceSession),
            session.Id.ToString(),
            AuditAction.Custom,
            null,
            JsonSerializer.Serialize(new { action = "ResumeCheckpointSaved", workflow }),
            cancellationToken);

        return dto;
    }

    public async Task<AutoResumePromptDto> GetAutoResumePromptAsync(CancellationToken cancellationToken = default)
    {
        var recovery = await LoadRecoveryPrefsAsync(cancellationToken);
        if (recovery.PromptOnLogin == false)
            return new AutoResumePromptDto { ShouldPrompt = false, Message = "Auto-resume prompts disabled." };

        if (recovery.DismissAutoResumeUntilUtc is DateTime until && until > DateTime.UtcNow)
            return new AutoResumePromptDto { ShouldPrompt = false, Message = "Resume prompt dismissed." };

        var pending = await _pending.GetPendingAsync(cancellationToken);
        var session = pending.ReviewPending.FirstOrDefault()
            ?? pending.ReadyToFinalize.FirstOrDefault()
            ?? pending.FailedSessions.FirstOrDefault()
            ?? pending.TodaysPending.FirstOrDefault();

        if (session is null)
            return new AutoResumePromptDto { ShouldPrompt = false, Message = "No pending attendance." };

        return new AutoResumePromptDto
        {
            ShouldPrompt = true,
            Session = session,
            Message = "You have pending attendance. Resume, continue review, or dismiss."
        };
    }

    public async Task DecideAutoResumeAsync(AutoResumeDecisionRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Remember) return;

        var until = request.Decision.Equals("dismiss", StringComparison.OrdinalIgnoreCase)
            ? DateTime.UtcNow.AddHours(12)
            : (DateTime?)null;

        var recovery = await LoadRecoveryPrefsAsync(cancellationToken);
        recovery.DismissAutoResumeUntilUtc = until;
        recovery.PromptOnLogin = true;

        // Store via notification prefs map extension key to avoid schema churn beyond RecoveryPreferencesJson column.
        await UpsertRecoveryJsonAsync(recovery, cancellationToken);

        if (request.SessionId.HasValue)
        {
            await _audit.RecordAsync(
                nameof(AttendanceSession),
                request.SessionId.Value.ToString(),
                AuditAction.Custom,
                null,
                JsonSerializer.Serialize(new { action = "AutoResumeDecision", request.Decision }),
                cancellationToken);
        }
    }

    private async Task UpsertRecoveryJsonAsync(RecoveryPrefs recovery, CancellationToken cancellationToken)
    {
        var entity = await _db.SchedulingWorkspacePreferences
            .FirstOrDefaultAsync(p =>
                p.TenantId == _currentUser.TenantId &&
                p.StaffId == _currentUser.StaffId, cancellationToken);

        if (entity is null)
        {
            await _prefs.UpsertAsync(new UpdateWorkspacePreferenceRequest(), cancellationToken);
            entity = await _db.SchedulingWorkspacePreferences
                .FirstAsync(p =>
                    p.TenantId == _currentUser.TenantId &&
                    p.StaffId == _currentUser.StaffId, cancellationToken);
        }

        entity.RecoveryPreferencesJson = JsonSerializer.Serialize(recovery, JsonOptions);
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<RecoveryPrefs> LoadRecoveryPrefsAsync(CancellationToken cancellationToken)
    {
        var entity = await _db.SchedulingWorkspacePreferences.AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == _currentUser.TenantId &&
                p.StaffId == _currentUser.StaffId, cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.RecoveryPreferencesJson))
            return new RecoveryPrefs { PromptOnLogin = true };
        return JsonSerializer.Deserialize<RecoveryPrefs>(entity.RecoveryPreferencesJson, JsonOptions)
               ?? new RecoveryPrefs { PromptOnLogin = true };
    }

    private async Task<AttendanceSession> RequireOwnedSessionAsync(Guid sessionId, bool track, CancellationToken cancellationToken)
    {
        if (_currentUser.StaffId <= 0)
            throw new DomainException("Staff context is required.");

        var query = track ? _db.AttendanceSessions : _db.AttendanceSessions.AsNoTracking();
        var session = await query.FirstOrDefaultAsync(
            s => s.TenantId == _currentUser.TenantId && s.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

        if (session.StaffId != _currentUser.StaffId &&
            !_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("You can only resume your own attendance sessions.");

        return session;
    }

    private sealed class RecoveryPrefs
    {
        public bool PromptOnLogin { get; set; } = true;
        public DateTime? DismissAutoResumeUntilUtc { get; set; }
    }
}

public sealed class AttendanceRetryService : IAttendanceRetryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClassroomPhotoService _photoService;
    private readonly IAttendanceSessionFinalizer _finalizer;
    private readonly IAuditService _audit;
    private readonly IAttendanceRecoveryNotifier _notifier;
    private readonly ILogger<AttendanceRetryService> _logger;

    public AttendanceRetryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClassroomPhotoService photoService,
        IAttendanceSessionFinalizer finalizer,
        IAuditService audit,
        IAttendanceRecoveryNotifier notifier,
        ILogger<AttendanceRetryService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _photoService = photoService;
        _finalizer = finalizer;
        _audit = audit;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<AttendanceRetryResultDto> RetryAsync(
        Guid sessionId,
        AttendanceRetryRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AttendanceSessions.FirstOrDefaultAsync(
            s => s.TenantId == _currentUser.TenantId && s.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

        if (session.WorkflowExpiredUtc.HasValue)
            throw new DomainException("Expired sessions cannot be retried until an administrator restores them.");

        if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
            throw new DomainException("Finalized attendance cannot be retried. Completed stages are never restarted.");

        var before = session.WorkflowStatus;
        bool ok;
        string? error = null;

        switch (request.Kind)
        {
            case AttendanceRetryKind.RetryFailedImages:
            case AttendanceRetryKind.RetryUpload:
                if (request.ImageId is Guid imageId)
                    (ok, error) = await _photoService.RequeueSessionImageAsync(sessionId, imageId, cancellationToken);
                else
                    (ok, error) = await _photoService.RequeueSessionRecognitionAsync(sessionId, cancellationToken);
                break;
            case AttendanceRetryKind.RetryRecognition:
            case AttendanceRetryKind.RetryEntireSession:
                // Stage-aware: requeue only failed/pending recognition via existing pipeline.
                (ok, error) = await _photoService.RequeueSessionRecognitionAsync(sessionId, cancellationToken);
                break;
            case AttendanceRetryKind.RetryFinalization:
                try
                {
                    await _finalizer.FinalizeAttendanceSessionAsync(sessionId, cancellationToken);
                    ok = true;
                }
                catch (Exception ex)
                {
                    ok = false;
                    error = ex.Message;
                    _logger.LogWarning(ex, "Retry finalization failed for {SessionId}", sessionId);
                }
                break;
            default:
                throw new DomainException("Unsupported retry kind.");
        }

        session = await _db.AttendanceSessions.FirstAsync(
            s => s.TenantId == _currentUser.TenantId && s.Id == sessionId, cancellationToken);
        session.RetryCount += 1;
        session.LastActivityUtc = DateTime.UtcNow;
        if (ok && request.Kind != AttendanceRetryKind.RetryFinalization)
            session.WorkflowStatus = AttendanceWorkflowStatus.RecognitionRunning;

        var history = new AttendanceRetryHistory
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            AttendanceSessionId = sessionId,
            Stage = request.Kind.ToString(),
            Action = request.Kind.ToString(),
            Success = ok,
            ErrorMessage = error,
            WorkflowStatusBefore = before,
            WorkflowStatusAfter = session.WorkflowStatus,
            PerformedBy = _currentUser.UserId,
            PerformedUtc = DateTime.UtcNow
        };
        await _db.AddAsync(history);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            nameof(AttendanceSession),
            sessionId.ToString(),
            AuditAction.Custom,
            null,
            JsonSerializer.Serialize(new { action = "Retry", request.Kind, ok, error }),
            cancellationToken);

        await _notifier.NotifyAsync(
            _currentUser.TenantId,
            session.StaffId,
            ok ? "AttendanceRetrySucceeded" : "AttendanceRetryFailed",
            new { sessionId, request.Kind, ok, error },
            cancellationToken);

        return new AttendanceRetryResultDto
        {
            SessionId = sessionId,
            Kind = request.Kind,
            Success = ok,
            Message = error ?? (ok ? "Retry queued for failed stages only." : "Retry failed."),
            WorkflowStatus = session.WorkflowStatus,
            RetryCount = session.RetryCount
        };
    }

    public async Task<IReadOnlyList<AttendanceRetryHistoryDto>> GetHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.AttendanceRetryHistories.AsNoTracking()
            .Where(h => h.TenantId == _currentUser.TenantId && h.AttendanceSessionId == sessionId)
            .OrderByDescending(h => h.PerformedUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(h => new AttendanceRetryHistoryDto
        {
            Id = h.Id,
            SessionId = h.AttendanceSessionId,
            Stage = h.Stage,
            Action = h.Action,
            Success = h.Success,
            ErrorMessage = h.ErrorMessage,
            PerformedUtc = h.PerformedUtc,
            PerformedBy = h.PerformedBy
        }).ToList();
    }
}

public sealed class AttendanceRecoverySearchService : IAttendanceRecoverySearchService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AttendanceRecoverySearchService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PendingAttendanceSessionDto>> SearchAsync(
        AttendanceRecoverySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(request.Take <= 0 ? 50 : request.Take, 1, 200);
        var q = _db.AttendanceSessions.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);

        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && _currentUser.StaffId > 0)
            q = q.Where(s => s.StaffId == _currentUser.StaffId);

        if (request.SessionId.HasValue) q = q.Where(s => s.Id == request.SessionId);
        if (request.StaffId.HasValue) q = q.Where(s => s.StaffId == request.StaffId);
        if (request.CourseId.HasValue) q = q.Where(s => s.CourseId == request.CourseId);
        if (request.GroupId.HasValue) q = q.Where(s => s.GroupId == request.GroupId);
        if (request.SemesterId.HasValue) q = q.Where(s => s.SemesterId == request.SemesterId);
        if (request.SubjectId.HasValue) q = q.Where(s => s.SubjectId == request.SubjectId);
        if (request.AttendanceDate.HasValue) q = q.Where(s => s.AttendanceDate.Date == request.AttendanceDate.Value.Date);
        if (request.Status.HasValue) q = q.Where(s => s.Status == request.Status);
        if (request.WorkflowStatus.HasValue) q = q.Where(s => s.WorkflowStatus == request.WorkflowStatus);

        if (request.StudentId.HasValue)
        {
            var sid = request.StudentId.Value;
            q = q.Where(s => _db.AttendanceRecognitions.Any(r =>
                r.AttendanceSessionId == s.Id && r.StudentId == sid));
        }

        if (!string.IsNullOrWhiteSpace(request.Query) && Guid.TryParse(request.Query.Trim(), out var gid))
            q = q.Where(s => s.Id == gid);

        var sessions = await q.OrderByDescending(s => s.LastActivityUtc ?? s.CreatedUtc).Take(take).ToListAsync(cancellationToken);
        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        return AttendanceSessionPriorityEngine.SortByPriority(
            sessions.Select(s => AttendanceSessionDisplayEnricher.Map(s, names)),
            s => s.PriorityScore,
            s => s.LastActivityUtc);
    }
}

public sealed class AttendanceRecoveryDashboardService : IAttendanceRecoveryDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public AttendanceRecoveryDashboardService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<AttendanceRecoveryDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Where(s => s.Status != AttendanceSessionStatus.Approved && s.Status != AttendanceSessionStatus.Completed)
            .OrderByDescending(s => s.LastActivityUtc ?? s.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await AttendanceSessionDisplayEnricher.LoadAsync(_db, sessions, cancellationToken);
        var mapped = AttendanceSessionPriorityEngine.SortByPriority(
            sessions.Select(s => AttendanceSessionDisplayEnricher.Map(s, names)),
            s => s.PriorityScore,
            s => s.LastActivityUtc).ToList();
        return new AttendanceRecoveryDashboardDto
        {
            TodayCount = mapped.Count(s => s.AttendanceDate.Date == today),
            YesterdayCount = mapped.Count(s => s.AttendanceDate.Date == yesterday),
            ProcessingCount = mapped.Count(s => s.WorkflowStatus == AttendanceWorkflowStatus.RecognitionRunning),
            FailedCount = mapped.Count(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed),
            ReviewPendingCount = mapped.Count(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress),
            FinalizationPendingCount = mapped.Count(s =>
                s.WorkflowStatus is AttendanceWorkflowStatus.ReadyForFinalization or AttendanceWorkflowStatus.RecognitionCompleted),
            ExpiredCount = mapped.Count(s => s.IsExpired),
            Sessions = mapped,
            ByStatus = mapped.GroupBy(s => s.FriendlyWorkflowLabel)
                .Select(g => new RecoveryChartPointDto { Label = g.Key, Value = g.Count() })
                .ToList()
        };
    }

    public async Task<AttendanceRecoveryAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var since = DateTime.UtcNow.AddDays(-14);
        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedUtc >= since)
            .ToListAsync(cancellationToken);

        var pending = sessions.Count(s =>
            s.Status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed or AttendanceSessionStatus.Cancelled));
        var failed = sessions.Count(s => s.Status == AttendanceSessionStatus.Failed);
        var reviewed = sessions.Count(s => s.Status is AttendanceSessionStatus.AwaitingReview or AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed);
        var finalized = sessions.Count(s => s.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed);

        var reviewTimes = sessions
            .Where(s => s.StartedUtc.HasValue && s.ApprovedUtc.HasValue)
            .Select(s => (s.ApprovedUtc!.Value - s.StartedUtc!.Value).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();

        return new AttendanceRecoveryAnalyticsDto
        {
            PendingSessions = pending,
            AverageReviewMinutes = reviewTimes.Count == 0 ? null : reviewTimes.Average(),
            AverageFinalizationMinutes = reviewTimes.Count == 0 ? null : reviewTimes.Average(),
            AverageRetryCount = sessions.Count == 0 ? 0 : sessions.Average(s => (double)s.RetryCount),
            FailureRatePercent = sessions.Count == 0 ? 0 : 100.0 * failed / sessions.Count,
            RecognitionSuccessPercent = sessions.Count == 0 ? 0 : 100.0 * reviewed / sessions.Count,
            ReviewCompletionPercent = sessions.Count == 0 ? 0 : 100.0 * finalized / sessions.Count,
            PendingTrend = sessions.GroupBy(s => s.CreatedUtc.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RecoveryChartPointDto { Label = g.Key.ToString("MM-dd"), Value = g.Count() })
                .ToList(),
            FacultyProductivity = sessions.Where(s => s.StaffId.HasValue)
                .GroupBy(s => s.StaffId!.Value)
                .Select(g => new RecoveryChartPointDto { Label = $"Staff {g.Key}", Value = g.Count(x => x.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed) })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList()
        };
    }

    public async Task AdminActionAsync(Guid sessionId, AdminSessionActionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var session = await _db.AttendanceSessions.FirstOrDefaultAsync(
            s => s.TenantId == _currentUser.TenantId && s.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{sessionId}' was not found.");

        switch (request.Action.Trim().ToLowerInvariant())
        {
            case "restore":
                session.WorkflowExpiredUtc = null;
                if (session.WorkflowStatus == AttendanceWorkflowStatus.Expired)
                    session.WorkflowStatus = AttendanceWorkflowStatus.ReviewPending;
                session.LastActivityUtc = DateTime.UtcNow;
                break;
            case "archive":
            case "delete":
                // Soft cancel — never hard-delete attendance audit trail.
                session.WorkflowStatus = AttendanceWorkflowStatus.Cancelled;
                session.WorkflowExpiredUtc ??= DateTime.UtcNow;
                break;
            default:
                throw new DomainException("Unsupported admin action. Use restore, archive, or delete.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(
            nameof(AttendanceSession),
            sessionId.ToString(),
            AuditAction.Custom,
            null,
            JsonSerializer.Serialize(new { action = "AdminRecoveryAction", request.Action, request.Reason }),
            cancellationToken);
    }

    private void EnsureAdmin()
    {
        if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Administrator access required for recovery dashboard.");
    }

}

public sealed class AttendanceExpirationService : IAttendanceExpirationService
{
    private readonly IApplicationDbContext _db;
    private readonly IOptions<AttendanceRecoveryOptions> _options;
    private readonly IAuditService _audit;
    private readonly IAttendanceRecoveryNotifier _notifier;
    private readonly ILogger<AttendanceExpirationService> _logger;

    public AttendanceExpirationService(
        IApplicationDbContext db,
        IOptions<AttendanceRecoveryOptions> options,
        IAuditService audit,
        IAttendanceRecoveryNotifier notifier,
        ILogger<AttendanceExpirationService> logger)
    {
        _db = db;
        _options = options;
        _audit = audit;
        _notifier = notifier;
        _logger = logger;
    }

    public ExpirationOptionsDto GetOptions() => new()
    {
        DefaultExpirationHours = _options.Value.DefaultExpirationHours,
        AllowedHours = _options.Value.AllowedExpirationHours
    };

    public async Task<int> ExpireStaleSessionsAsync(CancellationToken cancellationToken = default)
    {
        var hours = _options.Value.DefaultExpirationHours;
        if (!_options.Value.AllowedExpirationHours.Contains(hours))
            hours = 48;

        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var candidates = await _db.AttendanceSessions
            .Where(s => s.WorkflowExpiredUtc == null)
            .Where(s => s.Status != AttendanceSessionStatus.Approved &&
                        s.Status != AttendanceSessionStatus.Completed &&
                        s.Status != AttendanceSessionStatus.Cancelled)
            .Where(s => (s.LastActivityUtc ?? s.StartedUtc ?? s.CreatedUtc) < cutoff)
            .OrderBy(s => s.CreatedUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var session in candidates)
        {
            session.WorkflowExpiredUtc = DateTime.UtcNow;
            session.WorkflowStatus = AttendanceWorkflowStatus.Expired;
            session.LastActivityUtc = DateTime.UtcNow;
            await _audit.RecordAsync(
                nameof(AttendanceSession),
                session.Id.ToString(),
                AuditAction.Custom,
                null,
                JsonSerializer.Serialize(new { action = "Expired", hours }),
                cancellationToken);
            await _notifier.NotifyAsync(
                session.TenantId,
                session.StaffId,
                "AttendanceSessionExpired",
                new { session.Id, hours },
                cancellationToken);
        }

        if (candidates.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("AI22.8 expired {Count} stale attendance sessions (>{Hours}h).", candidates.Count, hours);
        return candidates.Count;
    }
}
