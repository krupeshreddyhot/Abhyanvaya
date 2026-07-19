using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Events;

public sealed record WorkerStarted(
    string WorkerId,
    string NodeId,
    DateTime StartedUtc) : DomainEventBase;

public sealed record LeaseAcquired(
    Guid LeaseId,
    Guid ItemId,
    string WorkerId,
    DateTime ExpiresUtc) : DomainEventBase;

public sealed record LeaseRenewed(
    Guid LeaseId,
    Guid ItemId,
    string WorkerId,
    DateTime ExpiresUtc,
    int RenewalCount) : DomainEventBase;

public sealed record WorkerCompleted(
    Guid ItemId,
    string WorkerId,
    long DurationMs,
    bool Success) : DomainEventBase;

public sealed record WorkerFailed(
    Guid ItemId,
    string WorkerId,
    string FailureCode,
    string FailureReason) : DomainEventBase;

public sealed record LeaseExpired(
    Guid LeaseId,
    Guid ItemId,
    string WorkerId) : DomainEventBase;

public sealed record RecoveryExecuted(
    int ExpiredLeases,
    int StuckItems,
    int RequeuedItems,
    long DurationMs) : DomainEventBase;
