using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Embedding/recognition version compatibility — no migration logic (AI20.PHASE2.5).</summary>
public interface IEmbeddingCompatibilityService
{
    ModelCompatibilityResult CheckCompatibility(
        string embeddingVersion,
        string recognitionVersion,
        int pipelineVersion);
}
