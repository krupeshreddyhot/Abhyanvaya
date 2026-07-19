using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Benchmarks recognition performance — independent of regression (AI20.PHASE2.5).</summary>
public interface IRecognitionBenchmarkService
{
    Task<RecognitionBenchmarkReport> RunAsync(RunBenchmarkRequest request, CancellationToken cancellationToken = default);
}
