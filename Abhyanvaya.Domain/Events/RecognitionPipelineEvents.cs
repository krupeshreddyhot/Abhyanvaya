using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Events;

public sealed record RecognitionStarted(
    Guid RecognitionRequestId,
    Guid AttendanceSessionId,
    int TenantId,
    Guid CorrelationId,
    int PipelineVersion,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record CandidatesRetrieved(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    int CandidateCount,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record SearchCompleted(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    int TopK,
    int ResultCount,
    TimeSpan SearchDuration,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RecognitionSucceeded(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    int? StudentId,
    decimal Confidence,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RecognitionUnknown(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    decimal? BestConfidence,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RecognitionFailed(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    string FailureCode,
    string? FailureReason,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record RecognitionCompleted(
    Guid RecognitionRequestId,
    Guid CorrelationId,
    RecognitionPipelineState FinalState,
    TimeSpan TotalDuration,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
