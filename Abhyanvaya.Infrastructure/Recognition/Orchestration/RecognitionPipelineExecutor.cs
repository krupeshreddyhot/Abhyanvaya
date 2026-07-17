using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Orchestration;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition.Orchestration;

public sealed class RecognitionPipelineExecutor : IRecognitionPipelineExecutor
{
    private readonly IRecognitionPipelineRegistry _registry;
    private readonly IRecognitionPipelineMetrics _metrics;
    private readonly ILogger<RecognitionPipelineExecutor> _logger;

    public RecognitionPipelineExecutor(
        IRecognitionPipelineRegistry registry,
        IRecognitionPipelineMetrics metrics,
        ILogger<RecognitionPipelineExecutor> logger)
    {
        _registry = registry;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<RecognitionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;
        var stageDurations = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        var stageOutcomes = new List<RecognitionPipelineStageOutcome>();
        var currentContext = context;

        _metrics.RecordPipelineStarted(itemContext.CorrelationId, itemContext.PipelineVersion);

        _ = new RecognitionStarted(
            itemContext.RecognitionRequestId,
            itemContext.AttendanceSessionId,
            itemContext.TenantId,
            itemContext.CorrelationId,
            itemContext.PipelineVersion,
            DateTime.UtcNow);

        _logger.LogInformation(
            "Recognition pipeline started. RequestId={RequestId} SessionId={SessionId} FaceIndex={FaceIndex} CorrelationId={CorrelationId}",
            itemContext.RecognitionRequestId,
            itemContext.AttendanceSessionId,
            itemContext.FaceIndex,
            itemContext.CorrelationId);

        var stages = _registry.GetOrderedStages(itemContext.PipelineVersion);
        TimeSpan searchDuration = TimeSpan.Zero;
        TimeSpan rankingDuration = TimeSpan.Zero;
        TimeSpan similarityDuration = TimeSpan.Zero;
        TimeSpan decisionDuration = TimeSpan.Zero;
        TimeSpan persistenceDuration = TimeSpan.Zero;

        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentContext = currentContext with { CurrentStage = stage.ManifestStage };

            var stageStopwatch = Stopwatch.StartNew();
            var stageResult = await stage.ExecuteAsync(currentContext, cancellationToken);
            stageStopwatch.Stop();

            stageDurations[stage.Name] = stageStopwatch.Elapsed;
            stageOutcomes.Add(new RecognitionPipelineStageOutcome
            {
                ManifestStage = stage.ManifestStage,
                StageName = stage.Name,
                Success = stageResult.Success,
                Duration = stageStopwatch.Elapsed,
                FailureCode = stageResult.FailureCode,
                FailureReason = stageResult.FailureReason,
            });

            _metrics.RecordStageCompleted(
                itemContext.CorrelationId,
                stage.Name,
                stageStopwatch.Elapsed,
                stageResult.Success);

            if (stageResult.IsCancelled)
            {
                return BuildFailureResult(currentContext, stageResult.FailureCode ?? RecognitionFailureCodes.Cancelled, stageResult.FailureReason, pipelineStopwatch.Elapsed, stageDurations);
            }

            if (!stageResult.Success)
            {
                return BuildFailureResult(currentContext, stageResult.FailureCode ?? RecognitionFailureCodes.StageFailed, stageResult.FailureReason, pipelineStopwatch.Elapsed, stageDurations);
            }

            currentContext = stageResult.UpdatedContext;

            switch (stage.ManifestStage)
            {
                case Application.Recognition.Pipeline.RecognitionPipelineStage.VectorSearch:
                    searchDuration = stageStopwatch.Elapsed;
                    break;
                case Application.Recognition.Pipeline.RecognitionPipelineStage.Similarity:
                    similarityDuration = stageStopwatch.Elapsed;
                    rankingDuration = stageStopwatch.Elapsed;
                    break;
                case Application.Recognition.Pipeline.RecognitionPipelineStage.Decision:
                    decisionDuration = stageStopwatch.Elapsed;
                    break;
                case Application.Recognition.Pipeline.RecognitionPipelineStage.Persistence:
                    persistenceDuration = stageStopwatch.Elapsed;
                    break;
            }
        }

        pipelineStopwatch.Stop();

        var statistics = new RecognitionStatistics
        {
            SearchDuration = searchDuration,
            RankingDuration = rankingDuration,
            SimilarityDuration = similarityDuration,
            DecisionDuration = decisionDuration,
            PersistenceDuration = persistenceDuration,
            TotalDuration = pipelineStopwatch.Elapsed,
            TopK = currentContext.Request.TopK,
            CandidateCount = currentContext.Candidates?.Count ?? 0,
            StageDurations = stageDurations,
        };

        currentContext = currentContext with { Statistics = statistics };

        _metrics.RecordPipelineCompleted(itemContext.CorrelationId, true, pipelineStopwatch.Elapsed);

        _ = new RecognitionCompleted(
            itemContext.RecognitionRequestId,
            itemContext.CorrelationId,
            currentContext.State,
            pipelineStopwatch.Elapsed,
            DateTime.UtcNow);

        _logger.LogInformation(
            "Recognition pipeline completed. RequestId={RequestId} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            itemContext.RecognitionRequestId,
            pipelineStopwatch.ElapsedMilliseconds,
            itemContext.CorrelationId);

        return new RecognitionResult
        {
            Success = true,
            RecognitionRequestId = itemContext.RecognitionRequestId,
            AttendanceSessionId = itemContext.AttendanceSessionId,
            TenantId = itemContext.TenantId,
            FaceIndex = itemContext.FaceIndex,
            Status = currentContext.State,
            Decision = currentContext.Decision,
            PersistedRecognitionId = currentContext.PersistenceResult?.RecognitionId,
            TopCandidates = currentContext.SearchResults,
            Statistics = statistics,
            Warnings = currentContext.Warnings,
        };
    }

    private static RecognitionResult BuildFailureResult(
        RecognitionPipelineContext context,
        string failureCode,
        string? failureReason,
        TimeSpan totalDuration,
        IReadOnlyDictionary<string, TimeSpan> stageDurations) =>
        new()
        {
            Success = false,
            RecognitionRequestId = context.ItemContext.RecognitionRequestId,
            AttendanceSessionId = context.ItemContext.AttendanceSessionId,
            TenantId = context.ItemContext.TenantId,
            FaceIndex = context.ItemContext.FaceIndex,
            Status = RecognitionPipelineState.Failed,
            FailureCode = failureCode,
            FailureReason = failureReason,
            Statistics = new RecognitionStatistics
            {
                TotalDuration = totalDuration,
                StageDurations = stageDurations,
                TopK = context.Request.TopK,
                CandidateCount = context.Candidates?.Count ?? 0,
            },
        };
}
