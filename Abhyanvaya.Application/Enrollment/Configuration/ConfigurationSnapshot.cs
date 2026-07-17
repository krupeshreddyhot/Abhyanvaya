namespace Abhyanvaya.Application.Enrollment.Configuration;

public sealed record ConfigurationSnapshot
{
    public required int SchemaVersion { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required string SnapshotHash { get; init; }

    public required string EmbeddingProvider { get; init; }
    public required string EmbeddingModel { get; init; }
    public required string EmbeddingVersion { get; init; }
    public required string EngineProvider { get; init; }
    public required string NormalizationMethod { get; init; }
    public required string AiModelVersion { get; init; }
    public string? ModelChecksum { get; init; }

    public required ThresholdSnapshot Thresholds { get; init; }
    public required string PhotoProvider { get; init; }
    public required IReadOnlyDictionary<string, string> PhotoProviderSettings { get; init; }
    public required ValidationRulesSnapshot ValidationRules { get; init; }
    public required string StorageProvider { get; init; }
    public required IReadOnlyDictionary<string, string> StorageProviderSettings { get; init; }

    public required int PipelineVersion { get; init; }
    public required PipelineManifestReference PipelineManifest { get; init; }
    public required RetryPolicySnapshot RetryPolicy { get; init; }
    public required IReadOnlyDictionary<string, bool> FeatureFlags { get; init; }

    public string? FuturePromptVersion { get; init; }
}

public sealed record ThresholdSnapshot
{
    public required float RecognitionMatchDistanceThreshold { get; init; }
    public required float RecognitionLowConfidenceDistanceThreshold { get; init; }
    public float? MinimumCompositeQualityScore { get; init; }
    public required float DetectionThreshold { get; init; }
    public required float NmsThreshold { get; init; }
}

public sealed record ValidationRulesSnapshot
{
    public required bool RequireExactlyOneFace { get; init; }
    public required int MinimumSourceWidth { get; init; }
    public required int MinimumSourceHeight { get; init; }
    public required int MinimumFaceWidth { get; init; }
    public required int MinimumFaceHeight { get; init; }
    public required string BlurMethod { get; init; }
    public required double BlurThreshold { get; init; }
    public required double MaximumAbsoluteYawDegrees { get; init; }
    public required double MaximumAbsolutePitchDegrees { get; init; }
    public required double MaximumAbsoluteRollDegrees { get; init; }
    public required bool CompositeQualityIsAdvisory { get; init; }
    public required IReadOnlyDictionary<string, double> CompositeQualityWeights { get; init; }
}

public sealed record PipelineManifestReference
{
    public required string PipelineName { get; init; }
    public required int PipelineVersion { get; init; }
    public required int ManifestSchemaVersion { get; init; }
    public required string ManifestHash { get; init; }
}

public sealed record RetryPolicySnapshot
{
    public required string PolicySetVersion { get; init; }
    public required IReadOnlyDictionary<string, string> StagePolicyNames { get; init; }
    public required int MaxAutomaticRetries { get; init; }
    public required int MaximumAutomaticAttempts { get; init; }
    public required TimeSpan RetryWindow { get; init; }
    public required string BackoffStrategy { get; init; }
    public required TimeSpan BaseDelay { get; init; }
    public required TimeSpan MaxDelay { get; init; }
    public required int MaximumConsecutiveImmediateRetries { get; init; }
    public required RetryBudgetSnapshot BatchBudget { get; init; }
    public required IReadOnlyDictionary<string, int> StageBudgetCosts { get; init; }
    public required IReadOnlyList<string> AutomaticRetrySafetyFloor { get; init; }
    public required bool ScheduledRetryHonorsRetryAfter { get; init; }
    public required StageInvocationRetrySnapshot LowLevelPhotoImport { get; init; }
}

public sealed record RetryBudgetSnapshot
{
    public required int CapacityTokens { get; init; }
    public required int RefillTokens { get; init; }
    public required TimeSpan RefillInterval { get; init; }
    public required int LifetimeSpendCeilingTokens { get; init; }
}

public sealed record StageInvocationRetrySnapshot
{
    public required int MaxRetriesWithinAttempt { get; init; }
    public required IReadOnlyList<int> BackoffSeconds { get; init; }
    public required IReadOnlyList<string> RetryableConditions { get; init; }
}
