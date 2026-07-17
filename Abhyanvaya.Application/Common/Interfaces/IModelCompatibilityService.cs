using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Validates embedding, recognition, and pipeline version compatibility (AI20.PHASE2.5).</summary>
public interface IModelCompatibilityService
{
    ModelCompatibilityResult Validate(AIModelDescriptor model, int pipelineVersion);
}
