namespace Abhyanvaya.Domain.Events;

public sealed record DeploymentVerified(Guid DeploymentId, DateTime OccurredUtc);

public sealed record SmokeTestsCompleted(Guid DeploymentId, bool Passed, DateTime OccurredUtc);

public sealed record PerformanceValidated(Guid DeploymentId, bool Passed, DateTime OccurredUtc);

public sealed record SecurityValidated(Guid DeploymentId, bool Passed, DateTime OccurredUtc);

public sealed record BackupValidated(Guid DeploymentId, bool Passed, DateTime OccurredUtc);

public sealed record RecoveryValidated(Guid DeploymentId, bool Passed, DateTime OccurredUtc);

public sealed record GoLiveCertified(Guid DeploymentId, string Decision, DateTime OccurredUtc);
