using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Configuration;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Captures an immutable configuration snapshot for a new batch (docs/AI20_PHASE2_CONFIGURATION_SNAPSHOT.md).
/// </summary>
public interface IEnrollmentConfigurationSnapshotCapture
{
    Task<ConfigurationSnapshotCaptureResult> CaptureAsync(
        EnrollmentBatchRequest request,
        int pipelineVersion,
        PipelineManifest manifest,
        string photoProviderName,
        CancellationToken cancellationToken = default);
}

public sealed record ConfigurationSnapshotCaptureResult
{
    public required bool Succeeded { get; init; }
    public ConfigurationSnapshot? Snapshot { get; init; }
    public string? SerializedJson { get; init; }
    public string? FailureMessage { get; init; }

    public static ConfigurationSnapshotCaptureResult Ok(ConfigurationSnapshot snapshot, string serializedJson) =>
        new() { Succeeded = true, Snapshot = snapshot, SerializedJson = serializedJson };

    public static ConfigurationSnapshotCaptureResult Fail(string message) =>
        new() { Succeeded = false, FailureMessage = message };
}
