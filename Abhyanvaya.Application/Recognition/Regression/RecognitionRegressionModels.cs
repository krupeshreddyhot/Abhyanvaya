namespace Abhyanvaya.Application.Recognition.Regression;

/// <summary>Future AI regression testing — architecture only (AI20.PHASE2.3).</summary>
public sealed record RecognitionGoldenDataset
{
    public required string DatasetId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<RecognitionGoldenSample> Samples { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record RecognitionGoldenSample
{
    public required string SampleId { get; init; }
    public required int ExpectedStudentId { get; init; }
    public required string ImagePath { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>Future regression runner — no evaluation logic yet.</summary>
public interface IRecognitionRegressionRunner
{
    string RunnerName { get; }
    Task<RecognitionAccuracyReport> RunAsync(RecognitionGoldenDataset dataset, CancellationToken cancellationToken = default);
}

/// <summary>Future accuracy report — populated by regression runner.</summary>
public sealed record RecognitionAccuracyReport
{
    public required string DatasetId { get; init; }
    public required string RunnerName { get; init; }
    public int TotalSamples { get; init; }
    public int CorrectMatches { get; init; }
    public int UnknownFaces { get; init; }
    public int FalsePositives { get; init; }
    public decimal AccuracyPercent { get; init; }
    public IReadOnlyDictionary<string, decimal>? Metrics { get; init; }
}

/// <summary>Future benchmark harness — architecture only.</summary>
public sealed record RecognitionBenchmark
{
    public required string BenchmarkId { get; init; }
    public required string Name { get; init; }
    public int IterationCount { get; init; }
    public int CandidatePoolSize { get; init; }
    public int TopK { get; init; }
}

public interface IRecognitionBenchmarkRunner
{
    Task<RecognitionBenchmarkResult> RunAsync(RecognitionBenchmark benchmark, CancellationToken cancellationToken = default);
}

public sealed record RecognitionBenchmarkResult
{
    public required string BenchmarkId { get; init; }
    public TimeSpan AverageSearchDuration { get; init; }
    public TimeSpan AverageDecisionDuration { get; init; }
    public TimeSpan P95SearchDuration { get; init; }
    public int IterationsCompleted { get; init; }
}
