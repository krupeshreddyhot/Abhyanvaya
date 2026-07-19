using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Pipeline;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Recognition.Orchestration;

public sealed record RecognitionPipelineContext
{
    public required RecognitionPipelineRequest Request { get; init; }
    public RecognitionPipelineState State { get; init; } = RecognitionPipelineState.Pending;
    public RecognitionPipelineStage? CurrentStage { get; init; }
    public float[]? QueryEmbedding { get; init; }
    public IReadOnlyList<RecognitionCandidate>? Candidates { get; init; }
    public IReadOnlyList<RecognitionSearchResult>? SearchResults { get; init; }
    public IReadOnlyList<SimilarityMatch>? RankedMatches { get; init; }
    public SimilarityStatistics? SimilarityStatistics { get; init; }
    public RecognitionDecision? Decision { get; init; }
    public RecognitionPersistenceResult? PersistenceResult { get; init; }
    public RecognitionStatistics? Statistics { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }

    public RecognitionRequestContext ItemContext => Request.Context;

    public static RecognitionPipelineContext Create(RecognitionPipelineRequest request) =>
        new()
        {
            Request = request,
            QueryEmbedding = request.QueryEmbedding,
        };
}

public sealed record RecognitionPipelineStageExecutionResult
{
    public required bool Success { get; init; }
    public required RecognitionPipelineContext UpdatedContext { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public bool IsCancelled { get; init; }

    public static RecognitionPipelineStageExecutionResult Ok(RecognitionPipelineContext context) =>
        new() { Success = true, UpdatedContext = context };

    public static RecognitionPipelineStageExecutionResult Fail(
        RecognitionPipelineContext context,
        string failureCode,
        string? reason = null) =>
        new()
        {
            Success = false,
            UpdatedContext = context,
            FailureCode = failureCode,
            FailureReason = reason,
        };

    public static RecognitionPipelineStageExecutionResult Cancelled(RecognitionPipelineContext context) =>
        new()
        {
            Success = false,
            UpdatedContext = context,
            IsCancelled = true,
            FailureCode = RecognitionFailureCodes.Cancelled,
        };
}
