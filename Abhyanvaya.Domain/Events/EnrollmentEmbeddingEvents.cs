using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Events;

/// <summary>Raised when an enrollment embedding is durably persisted. Event-ready — not published externally in this phase.</summary>
public sealed record EmbeddingPersisted(
    Guid EnrollmentItemId,
    Guid EmbeddingId,
    int StudentId,
    Guid BatchId,
    Guid CorrelationId,
    string EmbeddingModelVersion,
    DateTime PersistedUtc) : DomainEventBase;

/// <summary>Raised when enrollment embedding persistence fails. Event-ready — not published externally in this phase.</summary>
public sealed record EmbeddingPersistenceFailed(
    Guid? EnrollmentItemId,
    int StudentId,
    Guid BatchId,
    Guid CorrelationId,
    string FailureCode,
    string FailureReason,
    DateTime FailedUtc) : DomainEventBase;
