using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.Hosting;

namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// Read-only presence/size snapshot for a single InsightFace ONNX model file.
/// Populated using <see cref="File.Exists"/> and <see cref="FileInfo.Length"/> only — the model is
/// never opened or loaded.
/// </summary>
public sealed record ModelFileStatus(string FileName, string FullPath, bool Found, long SizeBytes)
{
    public double SizeMegabytes => Math.Round(SizeBytes / (1024d * 1024d), 1);
}

/// <summary>
/// Aggregate InsightFace model deployment status, resolved from <see cref="InsightFaceOptions"/>.
/// <see cref="ConfiguredModelDirectory"/> is the raw value from configuration (may be relative);
/// <see cref="ResolvedModelDirectory"/> is the absolute path actually checked on disk (AI12.OBS.10).
/// </summary>
public sealed record ModelAvailabilityReport(
    string ConfiguredModelDirectory,
    string ResolvedModelDirectory,
    bool ModelDirectoryExists,
    ModelFileStatus Detection,
    ModelFileStatus Embedding,
    string PipelineVersion)
{
    public bool AllModelsPresent => ModelDirectoryExists && Detection.Found && Embedding.Found;
}

/// <summary>
/// Resolves the configured model directory via <see cref="ModelPathResolver"/> (the single shared
/// helper also usable anywhere else path resolution is needed — AI12.OBS.10) before checking for
/// the detection/embedding ONNX files. Shared by the startup summary (AI11.HARDENING.3 /
/// AI12.OBS.5) and the <c>/health</c> and <c>/health/ready</c> endpoints (AI12.OBS.6) to avoid
/// duplicating the lookup/verification logic.
/// </summary>
public static class ModelAvailabilityChecker
{
    public static ModelAvailabilityReport Check(InsightFaceOptions options, IHostEnvironment environment)
    {
        var resolvedDirectory = ModelPathResolver.Resolve(options.ModelDirectory, environment);
        var directoryExists = !string.IsNullOrWhiteSpace(resolvedDirectory) && Directory.Exists(resolvedDirectory);

        var detection = BuildStatus(resolvedDirectory, options.DetectionModelFile, directoryExists);
        var embedding = BuildStatus(resolvedDirectory, options.RecognitionModelFile, directoryExists);

        return new ModelAvailabilityReport(
            options.ModelDirectory,
            resolvedDirectory,
            directoryExists,
            detection,
            embedding,
            options.PipelineVersion);
    }

    private static ModelFileStatus BuildStatus(string modelDirectory, string fileName, bool directoryExists)
    {
        var fullPath = Path.Combine(modelDirectory, fileName);
        if (!directoryExists || !File.Exists(fullPath))
        {
            return new ModelFileStatus(fileName, fullPath, Found: false, SizeBytes: 0);
        }

        var sizeBytes = new FileInfo(fullPath).Length;
        return new ModelFileStatus(fileName, fullPath, Found: true, SizeBytes: sizeBytes);
    }
}
