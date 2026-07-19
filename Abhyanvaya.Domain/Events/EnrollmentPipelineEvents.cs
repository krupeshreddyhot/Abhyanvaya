using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Events;

/// <summary>Raised when an enrollment pipeline run starts. Event-ready — not published externally.</summary>
public sealed record PipelineStarted(
    Guid ItemId,
    Guid BatchId,
    int StudentId,
    Guid CorrelationId,
    int PipelineVersion,
    DateTime StartedUtc) : DomainEventBase;

/// <summary>Raised when one enrollment pipeline stage completes successfully.</summary>
public sealed record PipelineStageCompleted(
    Guid ItemId,
    Guid BatchId,
    int StudentId,
    Guid CorrelationId,
    string StageName,
    long DurationMs,
    DateTime CompletedUtc) : DomainEventBase;

/// <summary>Raised when an enrollment pipeline fails.</summary>
public sealed record PipelineFailed(
    Guid ItemId,
    Guid BatchId,
    int StudentId,
    Guid CorrelationId,
    string StageName,
    string FailureCode,
    string FailureReason,
    DateTime FailedUtc) : DomainEventBase;

/// <summary>Raised when an enrollment pipeline completes successfully.</summary>
public sealed record PipelineCompleted(
    Guid ItemId,
    Guid BatchId,
    int StudentId,
    Guid CorrelationId,
    long DurationMs,
    DateTime CompletedUtc) : DomainEventBase;

/// <summary>Raised when an enrollment pipeline is cancelled.</summary>
public sealed record PipelineCancelled(
    Guid ItemId,
    Guid BatchId,
    int StudentId,
    Guid CorrelationId,
    string StageName,
    DateTime CancelledUtc) : DomainEventBase;
