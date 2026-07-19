using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Resolves validated pipeline manifests (docs/AI20_PHASE2_PIPELINE_MANIFEST.md).</summary>
public interface IPipelineManifestProvider
{
    PipelineManifest GetManifest(string pipelineName, int pipelineVersion);

    bool ManifestExists(string pipelineName, int pipelineVersion);
}
