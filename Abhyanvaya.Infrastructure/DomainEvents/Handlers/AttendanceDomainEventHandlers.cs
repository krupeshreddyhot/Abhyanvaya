using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.DomainEvents.Handlers;

/// <summary>
/// Logging-only handlers for the Attendance aggregate's domain events (AI13.DOMAIN.4).
/// Each handler performs structured logging only — no business logic, no persistence,
/// no side effects beyond the log entry. They are the seam future features (notifications,
/// audit trail, analytics) will plug into without touching the code that raises the events.
/// </summary>
public sealed class AttendanceMarkedEventHandler : IDomainEventHandler<AttendanceMarkedEvent>
{
    private readonly ILogger<AttendanceMarkedEventHandler> _logger;

    public AttendanceMarkedEventHandler(ILogger<AttendanceMarkedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(AttendanceMarkedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AttendanceMarked: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} StudentCount={StudentCount} CaptureMethod={CaptureMethod} MarkedByUserId={MarkedByUserId} OccurredUtc={OccurredUtc}",
            domainEvent.TenantId,
            domainEvent.SubjectId,
            domainEvent.AttendanceDay,
            domainEvent.StudentCount,
            domainEvent.CaptureMethod,
            domainEvent.MarkedByUserId,
            domainEvent.OccurredUtc);

        return Task.CompletedTask;
    }
}

public sealed class AttendanceGeneratedFromAIEventHandler : IDomainEventHandler<AttendanceGeneratedFromAIEvent>
{
    private readonly ILogger<AttendanceGeneratedFromAIEventHandler> _logger;

    public AttendanceGeneratedFromAIEventHandler(ILogger<AttendanceGeneratedFromAIEventHandler> logger) => _logger = logger;

    public Task HandleAsync(AttendanceGeneratedFromAIEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AttendanceGeneratedFromAI: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} StudentCount={StudentCount} Present={PresentCount} Absent={AbsentCount} CaptureMethod={CaptureMethod} AttendanceSessionId={AttendanceSessionId} OccurredUtc={OccurredUtc}",
            domainEvent.TenantId,
            domainEvent.SubjectId,
            domainEvent.AttendanceDay,
            domainEvent.StudentCount,
            domainEvent.PresentCount,
            domainEvent.AbsentCount,
            domainEvent.CaptureMethod,
            domainEvent.AttendanceSessionId,
            domainEvent.OccurredUtc);

        return Task.CompletedTask;
    }
}

public sealed class AttendanceFinalizedEventHandler : IDomainEventHandler<AttendanceFinalizedEvent>
{
    private readonly ILogger<AttendanceFinalizedEventHandler> _logger;

    public AttendanceFinalizedEventHandler(ILogger<AttendanceFinalizedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(AttendanceFinalizedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AttendanceFinalized: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} StudentCount={StudentCount} Present={PresentCount} Absent={AbsentCount} CaptureMethod={CaptureMethod} AttendanceSessionId={AttendanceSessionId} OccurredUtc={OccurredUtc}",
            domainEvent.TenantId,
            domainEvent.SubjectId,
            domainEvent.AttendanceDay,
            domainEvent.StudentCount,
            domainEvent.PresentCount,
            domainEvent.AbsentCount,
            domainEvent.CaptureMethod,
            domainEvent.AttendanceSessionId,
            domainEvent.OccurredUtc);

        return Task.CompletedTask;
    }
}

public sealed class AttendanceLockedEventHandler : IDomainEventHandler<AttendanceLockedEvent>
{
    private readonly ILogger<AttendanceLockedEventHandler> _logger;

    public AttendanceLockedEventHandler(ILogger<AttendanceLockedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(AttendanceLockedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AttendanceLocked: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} StudentCount={StudentCount} LockedByUserId={LockedByUserId} OccurredUtc={OccurredUtc}",
            domainEvent.TenantId,
            domainEvent.SubjectId,
            domainEvent.AttendanceDay,
            domainEvent.StudentCount,
            domainEvent.LockedByUserId,
            domainEvent.OccurredUtc);

        return Task.CompletedTask;
    }
}

public sealed class AttendanceUnlockedEventHandler : IDomainEventHandler<AttendanceUnlockedEvent>
{
    private readonly ILogger<AttendanceUnlockedEventHandler> _logger;

    public AttendanceUnlockedEventHandler(ILogger<AttendanceUnlockedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(AttendanceUnlockedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AttendanceUnlocked: Tenant={TenantId} Subject={SubjectId} AttendanceDay={AttendanceDay} StudentCount={StudentCount} UnlockedByUserId={UnlockedByUserId} OccurredUtc={OccurredUtc}",
            domainEvent.TenantId,
            domainEvent.SubjectId,
            domainEvent.AttendanceDay,
            domainEvent.StudentCount,
            domainEvent.UnlockedByUserId,
            domainEvent.OccurredUtc);

        return Task.CompletedTask;
    }
}
