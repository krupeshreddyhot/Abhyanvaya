using Abhyanvaya.Application.Recognition.Regression;

namespace Abhyanvaya.Infrastructure.Recognition.Regression;

/// <summary>Architecture-only regression runner stub (AI20.PHASE2.3).</summary>
public sealed class RecognitionRegressionRunnerStub : IRecognitionRegressionRunner
{
    public string RunnerName => "Stub";

    public Task<RecognitionAccuracyReport> RunAsync(
        RecognitionGoldenDataset dataset,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Recognition regression evaluation is not implemented in PHASE2.3.");
    }
}

public sealed class RecognitionBenchmarkRunnerStub : IRecognitionBenchmarkRunner
{
    public Task<RecognitionBenchmarkResult> RunAsync(
        RecognitionBenchmark benchmark,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Recognition benchmark execution is not implemented in PHASE2.3.");
    }
}
