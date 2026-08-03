using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>
/// AI22.8 — keeps additive <see cref="AttendanceWorkflowStatus"/> in sync with Status transitions
/// and emits recovery notifications. Does not create sessions or replace Status.
/// </summary>
public interface IAttendanceWorkflowLifecycleService
{
    void ApplyLocal(AttendanceSession session, bool hasImages = true, bool reviewStarted = false, AttendanceWorkflowStatus? force = null);
    void EnsureNotExpiredForFinalization(AttendanceSession session);
    Task NotifyAsync(AttendanceSession session, string eventName, object? extra = null, CancellationToken cancellationToken = default);
}

public sealed class AttendanceWorkflowLifecycleService : IAttendanceWorkflowLifecycleService
{
    private readonly IAttendanceRecoveryNotifier _notifier;

    public AttendanceWorkflowLifecycleService(IAttendanceRecoveryNotifier notifier) => _notifier = notifier;

    public void ApplyLocal(
        AttendanceSession session,
        bool hasImages = true,
        bool reviewStarted = false,
        AttendanceWorkflowStatus? force = null)
    {
        session.LastActivityUtc = DateTime.UtcNow;
        session.WorkflowStatus = force ?? AttendanceWorkflowMapper.FromSession(session, hasImages, reviewStarted: reviewStarted);
    }

    public void EnsureNotExpiredForFinalization(AttendanceSession session)
    {
        if (session.WorkflowExpiredUtc.HasValue || session.WorkflowStatus == AttendanceWorkflowStatus.Expired)
            throw new DomainException(
                "This attendance session has expired and cannot be finalized. An administrator must restore it first.");
    }

    public Task NotifyAsync(
        AttendanceSession session,
        string eventName,
        object? extra = null,
        CancellationToken cancellationToken = default) =>
        _notifier.NotifyAsync(
            session.TenantId,
            session.StaffId,
            eventName,
            new
            {
                sessionId = session.Id,
                workflowStatus = session.WorkflowStatus.ToString(),
                status = session.Status.ToString(),
                resumePath = AttendanceWorkflowMapper.ResumePath(session.Id, session.WorkflowStatus),
                extra
            },
            cancellationToken);
}
