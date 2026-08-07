using System.Diagnostics;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.DomainEvents.Handlers;

/// <summary>AI29.1A.5/7 — Logging + domain event metrics (no SignalR).</summary>
public sealed class ProgramCreatedEventHandler : IDomainEventHandler<ProgramCreated>
{
    private readonly ILogger<ProgramCreatedEventHandler> _logger;
    private readonly IAcademicDomainEventMetrics _metrics;

    public ProgramCreatedEventHandler(ILogger<ProgramCreatedEventHandler> logger, IAcademicDomainEventMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(ProgramCreated domainEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordPublished("ProgramCreated");
        try
        {
            _logger.LogInformation(
                "ProgramCreated: ProgramId={ProgramId} TenantId={TenantId} Code={ProgramCode} OccurredUtc={OccurredUtc}",
                domainEvent.ProgramId, domainEvent.TenantId, domainEvent.ProgramCode, domainEvent.OccurredUtc);
            sw.Stop();
            _metrics.RecordSucceeded("ProgramCreated", sw.Elapsed);
        }
        catch
        {
            sw.Stop();
            _metrics.RecordFailed("ProgramCreated", sw.Elapsed);
            throw;
        }
        return Task.CompletedTask;
    }
}

public sealed class ProgramUpdatedEventHandler : IDomainEventHandler<ProgramUpdated>
{
    private readonly ILogger<ProgramUpdatedEventHandler> _logger;
    private readonly IAcademicDomainEventMetrics _metrics;

    public ProgramUpdatedEventHandler(ILogger<ProgramUpdatedEventHandler> logger, IAcademicDomainEventMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(ProgramUpdated domainEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordPublished("ProgramUpdated");
        try
        {
            _logger.LogInformation(
                "ProgramUpdated: ProgramId={ProgramId} TenantId={TenantId} Code={ProgramCode} OccurredUtc={OccurredUtc}",
                domainEvent.ProgramId, domainEvent.TenantId, domainEvent.ProgramCode, domainEvent.OccurredUtc);
            sw.Stop();
            _metrics.RecordSucceeded("ProgramUpdated", sw.Elapsed);
        }
        catch
        {
            sw.Stop();
            _metrics.RecordFailed("ProgramUpdated", sw.Elapsed);
            throw;
        }
        return Task.CompletedTask;
    }
}

public sealed class ProgramArchivedEventHandler : IDomainEventHandler<ProgramArchived>
{
    private readonly ILogger<ProgramArchivedEventHandler> _logger;
    private readonly IAcademicDomainEventMetrics _metrics;

    public ProgramArchivedEventHandler(ILogger<ProgramArchivedEventHandler> logger, IAcademicDomainEventMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(ProgramArchived domainEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordPublished("ProgramArchived");
        try
        {
            _logger.LogInformation(
                "ProgramArchived: ProgramId={ProgramId} TenantId={TenantId} Code={ProgramCode} OccurredUtc={OccurredUtc}",
                domainEvent.ProgramId, domainEvent.TenantId, domainEvent.ProgramCode, domainEvent.OccurredUtc);
            sw.Stop();
            _metrics.RecordSucceeded("ProgramArchived", sw.Elapsed);
        }
        catch
        {
            sw.Stop();
            _metrics.RecordFailed("ProgramArchived", sw.Elapsed);
            throw;
        }
        return Task.CompletedTask;
    }
}

public sealed class CourseAssignedEventHandler : IDomainEventHandler<CourseAssigned>
{
    private readonly ILogger<CourseAssignedEventHandler> _logger;
    private readonly IAcademicDomainEventMetrics _metrics;

    public CourseAssignedEventHandler(ILogger<CourseAssignedEventHandler> logger, IAcademicDomainEventMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(CourseAssigned domainEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordPublished("CourseAssigned");
        try
        {
            _logger.LogInformation(
                "CourseAssigned: CourseId={CourseId} ProgramId={ProgramId} TenantId={TenantId} OccurredUtc={OccurredUtc}",
                domainEvent.CourseId, domainEvent.ProgramId, domainEvent.TenantId, domainEvent.OccurredUtc);
            sw.Stop();
            _metrics.RecordSucceeded("CourseAssigned", sw.Elapsed);
        }
        catch
        {
            sw.Stop();
            _metrics.RecordFailed("CourseAssigned", sw.Elapsed);
            throw;
        }
        return Task.CompletedTask;
    }
}

public sealed class CourseRemovedEventHandler : IDomainEventHandler<CourseRemoved>
{
    private readonly ILogger<CourseRemovedEventHandler> _logger;
    private readonly IAcademicDomainEventMetrics _metrics;

    public CourseRemovedEventHandler(ILogger<CourseRemovedEventHandler> logger, IAcademicDomainEventMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(CourseRemoved domainEvent, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordPublished("CourseRemoved");
        try
        {
            _logger.LogInformation(
                "CourseRemoved: CourseId={CourseId} PreviousProgramId={PreviousProgramId} TenantId={TenantId} OccurredUtc={OccurredUtc}",
                domainEvent.CourseId, domainEvent.PreviousProgramId, domainEvent.TenantId, domainEvent.OccurredUtc);
            sw.Stop();
            _metrics.RecordSucceeded("CourseRemoved", sw.Elapsed);
        }
        catch
        {
            sw.Stop();
            _metrics.RecordFailed("CourseRemoved", sw.Elapsed);
            throw;
        }
        return Task.CompletedTask;
    }
}
