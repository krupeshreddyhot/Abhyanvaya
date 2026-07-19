using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Events;

public sealed record EnrollmentQueued(
    Guid EnrollmentId,
    Guid BatchId,
    int StudentId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record EnrollmentStarted(
    Guid EnrollmentId,
    Guid BatchId,
    int StudentId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record FaceDetected(
    Guid EnrollmentId,
    int FaceCount,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record FaceAligned(
    Guid EnrollmentId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record EmbeddingGenerated(
    Guid EnrollmentId,
    int Dimension,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record QualityValidated(
    Guid EnrollmentId,
    decimal QualityScore,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record DuplicateDetected(
    Guid EnrollmentId,
    string DuplicateType,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record ArtifactBuilt(
    Guid EnrollmentId,
    Guid ManifestId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record EnrollmentCompleted(
    Guid EnrollmentId,
    Guid BatchId,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);

public sealed record EnrollmentFailed(
    Guid EnrollmentId,
    string Reason,
    EnrollmentState State,
    DateTime OccurredUtc) : DomainEventBase(OccurredUtc);
