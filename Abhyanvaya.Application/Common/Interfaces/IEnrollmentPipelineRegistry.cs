namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Registers and orders enrollment pipeline stages for dynamic discovery (AI20.PHASE2.1.8).
/// </summary>
public interface IEnrollmentPipelineRegistry
{
    IReadOnlyList<IEnrollmentPipelineStage> GetOrderedStages(int pipelineVersion);
}
