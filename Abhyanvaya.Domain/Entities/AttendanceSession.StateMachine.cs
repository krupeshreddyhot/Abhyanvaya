using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Enforces valid <see cref="AttendanceSessionStatus"/> transitions for the session aggregate.
/// </summary>
public partial class AttendanceSession
{
    /// <summary>Moves the session from <see cref="AttendanceSessionStatus.Draft"/> to pending submission.</summary>
    public void MoveToPending() => TransitionTo(AttendanceSessionStatus.Pending);

    /// <summary>Moves the session into automated AI processing.</summary>
    public void MoveToProcessing() => TransitionTo(AttendanceSessionStatus.Processing);

    /// <summary>Moves the session to teacher review after processing completes.</summary>
    public void MoveToAwaitingReview() => TransitionTo(AttendanceSessionStatus.AwaitingReview);

    /// <summary>Moves the session into a failed processing state.</summary>
    public void MoveToFailed() => TransitionTo(AttendanceSessionStatus.Failed);

    /// <summary>
    /// Marks the session approved after teacher review and attendance materialization.
    /// </summary>
    public void Approve(int? approvedBy, DateTime approvedUtc)
    {
        EnsureNotCompleted();
        EnsureNotCancelled();

        if (Status == AttendanceSessionStatus.Approved)
        {
            throw new DomainException("Attendance session is already approved.");
        }

        if (!CanApproveFromCurrentStatus())
        {
            throw new DomainException(
                $"Cannot approve attendance session from status '{Status}'.");
        }

        TransitionTo(AttendanceSessionStatus.Approved);
        ApprovedBy = approvedBy;
        ApprovedUtc = approvedUtc;
        CompletedUtc = approvedUtc;

        AddDomainEvent(new Events.AttendanceApprovedEvent(Id, TenantId, approvedUtc));
    }

    /// <summary>Moves an approved session to its terminal completed state.</summary>
    public void Complete()
    {
        EnsureNotCompleted();
        EnsureNotCancelled();
        TransitionTo(AttendanceSessionStatus.Completed);
        AddDomainEvent(new Events.AttendanceCompletedEvent(Id, TenantId, DateTime.UtcNow));
    }

    /// <summary>Voids the session. Cannot cancel a completed session.</summary>
    public void Cancel()
    {
        if (Status == AttendanceSessionStatus.Completed)
        {
            throw new DomainException("Completed attendance sessions cannot be cancelled.");
        }

        if (Status == AttendanceSessionStatus.Cancelled)
        {
            throw new DomainException("Attendance session is already cancelled.");
        }

        TransitionTo(AttendanceSessionStatus.Cancelled);
        AddDomainEvent(new Events.AttendanceCancelledEvent(Id, TenantId, DateTime.UtcNow));
    }

    private bool CanApproveFromCurrentStatus() =>
        Status is AttendanceSessionStatus.AwaitingReview
            or AttendanceSessionStatus.Draft
            or AttendanceSessionStatus.Pending
            or AttendanceSessionStatus.Processing
            or AttendanceSessionStatus.Failed;

    private void TransitionTo(AttendanceSessionStatus targetStatus)
    {
        EnsureNotCompleted();
        EnsureNotCancelled();

        if (!CanTransitionTo(targetStatus))
        {
            throw new DomainException(
                $"Invalid attendance session transition from '{Status}' to '{targetStatus}'.");
        }

        Status = targetStatus;
    }

    private bool CanTransitionTo(AttendanceSessionStatus targetStatus) =>
        (Status, targetStatus) switch
        {
            (AttendanceSessionStatus.Draft, AttendanceSessionStatus.Pending) => true,
            (AttendanceSessionStatus.Pending, AttendanceSessionStatus.Processing) => true,
            (AttendanceSessionStatus.Processing, AttendanceSessionStatus.AwaitingReview) => true,
            (AttendanceSessionStatus.AwaitingReview, AttendanceSessionStatus.Approved) => true,
            (AttendanceSessionStatus.Approved, AttendanceSessionStatus.Completed) => true,
            (AttendanceSessionStatus.Draft, AttendanceSessionStatus.Approved) => true,
            (AttendanceSessionStatus.Pending, AttendanceSessionStatus.Approved) => true,
            (AttendanceSessionStatus.Processing, AttendanceSessionStatus.Approved) => true,
            (AttendanceSessionStatus.Failed, AttendanceSessionStatus.Approved) => true,
            (AttendanceSessionStatus.Draft, AttendanceSessionStatus.Failed) => true,
            (AttendanceSessionStatus.Pending, AttendanceSessionStatus.Failed) => true,
            (AttendanceSessionStatus.Processing, AttendanceSessionStatus.Failed) => true,
            (AttendanceSessionStatus.Draft, AttendanceSessionStatus.Cancelled) => true,
            (AttendanceSessionStatus.Pending, AttendanceSessionStatus.Cancelled) => true,
            (AttendanceSessionStatus.Processing, AttendanceSessionStatus.Cancelled) => true,
            (AttendanceSessionStatus.AwaitingReview, AttendanceSessionStatus.Cancelled) => true,
            (AttendanceSessionStatus.Approved, AttendanceSessionStatus.Cancelled) => true,
            (AttendanceSessionStatus.Failed, AttendanceSessionStatus.Cancelled) => true,
            _ => false
        };

    private void EnsureNotCompleted()
    {
        if (Status == AttendanceSessionStatus.Completed)
        {
            throw new DomainException("Completed attendance sessions cannot change state.");
        }
    }

    private void EnsureNotCancelled()
    {
        if (Status == AttendanceSessionStatus.Cancelled)
        {
            throw new DomainException("Cancelled attendance sessions cannot change state.");
        }
    }
}
