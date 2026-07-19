namespace Abhyanvaya.Domain.Events;

public sealed record ArtifactQueued(Guid ArtifactId, Guid EnrollmentId, DateTime OccurredUtc);

public sealed record ArtifactUploadStarted(Guid ArtifactId, Guid EnrollmentId, DateTime OccurredUtc);

public sealed record ArtifactUploaded(Guid ArtifactId, string StorageKey, DateTime OccurredUtc);

public sealed record ArtifactVerified(Guid ArtifactId, string Checksum, DateTime OccurredUtc);

public sealed record ArtifactVerificationFailed(Guid ArtifactId, string Reason, DateTime OccurredUtc);

public sealed record ArtifactArchived(Guid ArtifactId, DateTime OccurredUtc);

public sealed record ArtifactDeleted(Guid ArtifactId, DateTime OccurredUtc);

public sealed record ArtifactUploadFailed(Guid ArtifactId, string Reason, DateTime OccurredUtc);
