namespace Abhyanvaya.Application.Enrollment.Storage;

public static class EnrollmentArtifactResolveCodes
{
    public const string ArtifactMissing = "artifact.missing";
    public const string UnsupportedArtifact = "artifact.unsupported";
    public const string ChecksumMismatch = "artifact.checksum_mismatch";
    public const string IncompatibleManifest = "manifest.incompatible";
    public const string StreamUnavailable = "artifact.stream_unavailable";
}

public sealed record EnrollmentArtifactResolveRequest
{
    public required EnrollmentStorageManifest Manifest { get; init; }
    public required string ArtifactType { get; init; }
    public Guid? CorrelationId { get; init; }
    public int? PipelineVersion { get; init; }
}

/// <summary>Resolved artifact for downstream consumers. Does not expose storage paths or object keys.</summary>
public sealed record EnrollmentArtifact : IAsyncDisposable
{
    public required string ArtifactType { get; init; }
    public required Stream Content { get; init; }
    public required EnrollmentArtifactMetadata Metadata { get; init; }
    public required string ContentType { get; init; }
    public required string Checksum { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
    }
}

public sealed record EnrollmentArtifactMetadata
{
    public required Guid ArtifactId { get; init; }
    public required Guid ManifestId { get; init; }
    public required int ManifestVersion { get; init; }
    public required int PipelineVersion { get; init; }
    public required int StorageVersion { get; init; }
    public required int ValidationVersion { get; init; }
    public string? ValidationProfile { get; init; }
    public long? FileSize { get; init; }
}

public sealed record EnrollmentArtifactResolveResult
{
    public required bool Success { get; init; }
    public EnrollmentArtifact? Artifact { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public required TimeSpan Duration { get; init; }

    public static EnrollmentArtifactResolveResult Succeeded(EnrollmentArtifact artifact, TimeSpan duration) =>
        new() { Success = true, Artifact = artifact, Duration = duration };

    public static EnrollmentArtifactResolveResult Failed(
        string code,
        string reason,
        TimeSpan duration) =>
        new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason,
            Duration = duration,
        };
}
