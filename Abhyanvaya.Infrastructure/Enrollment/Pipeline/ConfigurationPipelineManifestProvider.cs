using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Pipeline;

public sealed class ConfigurationPipelineManifestProvider : IPipelineManifestProvider
{
    private readonly EnrollmentPipelineOptions _options;
    private readonly IReadOnlyDictionary<int, PipelineManifest> _manifests;

    public ConfigurationPipelineManifestProvider(IOptions<EnrollmentPipelineOptions> options)
    {
        _options = options.Value;
        _manifests = BuildManifests(_options);
    }

    public PipelineManifest GetManifest(string pipelineName, int pipelineVersion)
    {
        if (!string.Equals(pipelineName, _options.PipelineName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(pipelineName, EnrollmentPipelineDefaults.PipelineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unknown pipeline name '{pipelineName}'.");
        }

        if (!_manifests.TryGetValue(pipelineVersion, out var manifest))
        {
            throw new InvalidOperationException(
                $"Pipeline manifest for {pipelineName} v{pipelineVersion} was not found.");
        }

        return manifest;
    }

    public bool ManifestExists(string pipelineName, int pipelineVersion) =>
        (string.Equals(pipelineName, _options.PipelineName, StringComparison.OrdinalIgnoreCase)
         || string.Equals(pipelineName, EnrollmentPipelineDefaults.PipelineName, StringComparison.OrdinalIgnoreCase))
        && _manifests.ContainsKey(pipelineVersion);

    internal static string ComputeManifestHash(PipelineManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static IReadOnlyDictionary<int, PipelineManifest> BuildManifests(EnrollmentPipelineOptions options)
    {
        var manifests = new Dictionary<int, PipelineManifest>();

        if (options.Versions.Count == 0)
        {
            manifests[Math.Max(1, options.ActiveVersion)] = EnrollmentPipelineDefaults.CreateV1Manifest();
            return manifests;
        }

        foreach (var (version, versionOptions) in options.Versions)
        {
            var baseManifest = EnrollmentPipelineDefaults.CreateV1Manifest();
            manifests[version] = baseManifest with
            {
                PipelineVersion = version,
                SchemaVersion = Math.Max(1, versionOptions.SchemaVersion),
                Description = versionOptions.Description ?? baseManifest.Description,
            };
        }

        if (!manifests.ContainsKey(Math.Max(1, options.ActiveVersion)))
        {
            manifests[Math.Max(1, options.ActiveVersion)] = EnrollmentPipelineDefaults.CreateV1Manifest();
        }

        return manifests;
    }
}
