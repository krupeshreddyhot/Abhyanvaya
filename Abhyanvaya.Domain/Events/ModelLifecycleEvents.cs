using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Events;

public sealed record ModelActivated(
    Guid ModelId,
    string Version,
    AIModelState State,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ModelRetired(
    Guid ModelId,
    string Version,
    string? Reason,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record BenchmarkCompleted(
    Guid ModelId,
    string Version,
    string BenchmarkId,
    TimeSpan Duration,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RegressionCompleted(
    Guid ModelId,
    string Version,
    string DatasetId,
    decimal AccuracyPercent,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RolloutStarted(
    Guid ModelId,
    string Version,
    string RolloutId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RollbackCompleted(
    Guid ModelId,
    string FromVersion,
    string ToVersion,
    string Reason,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record DriftDetected(
    Guid ModelId,
    string Version,
    string Severity,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
