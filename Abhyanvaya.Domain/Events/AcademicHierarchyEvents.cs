using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Events;

/// <summary>AI29.1A.5 — Program / hierarchy domain events (logging seam; no SignalR).</summary>
public sealed record ProgramCreated(
    int ProgramId,
    int TenantId,
    string ProgramCode,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ProgramUpdated(
    int ProgramId,
    int TenantId,
    string ProgramCode,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ProgramArchived(
    int ProgramId,
    int TenantId,
    string ProgramCode,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record CourseAssigned(
    int CourseId,
    int? ProgramId,
    int TenantId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record CourseRemoved(
    int CourseId,
    int? PreviousProgramId,
    int TenantId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
