using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Events;

public sealed record AIHealthCheckCompleted(
    string ComponentName,
    AIHealthStatus Status,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record AIAlertRaised(
    string AlertId,
    string Severity,
    string Component,
    string Message,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ProductionVerificationCompleted(
    bool Passed,
    string OverallStatus,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record CapacityReportGenerated(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
