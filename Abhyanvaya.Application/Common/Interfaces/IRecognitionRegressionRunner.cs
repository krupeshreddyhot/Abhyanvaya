using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Runs recognition regression against golden datasets — never deploys models (AI20.PHASE2.5).</summary>
public interface IRecognitionRegressionRunner
{
    string RunnerName { get; }

    Task<RecognitionRegressionReport> RunAsync(RunRegressionRequest request, CancellationToken cancellationToken = default);
}
