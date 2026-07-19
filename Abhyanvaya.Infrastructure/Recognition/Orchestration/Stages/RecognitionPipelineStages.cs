using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Orchestration;
using Abhyanvaya.Application.Recognition.Pipeline;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition.Orchestration.Stages;

public sealed class EmbeddingRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly ILogger<EmbeddingRecognitionPipelineStage> _logger;

    public EmbeddingRecognitionPipelineStage(
        IFaceDetectionService faceDetectionService,
        IEmbeddingEngine embeddingEngine,
        ILogger<EmbeddingRecognitionPipelineStage> logger)
    {
        _faceDetectionService = faceDetectionService;
        _embeddingEngine = embeddingEngine;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.Embedding;

    public string Name => "Embedding";

    public int Order => 100;

    public string Description => "Extracts query embedding via IEmbeddingEngine or IFaceDetectionService.";

    public string Version => "1.0";

    public async Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Request.Context.CancellationRequested)
        {
            return RecognitionPipelineStageExecutionResult.Cancelled(context);
        }

        float[]? embedding = context.QueryEmbedding ?? context.Request.QueryEmbedding;

        if (embedding == null && context.Request.ImageBytes is { Length: > 0 })
        {
            var detection = await _faceDetectionService.DetectAsync(
                new FaceDetectionRequest(context.Request.ImageBytes),
                cancellationToken);

            var face = detection.Faces.FirstOrDefault(f => f.FaceIndex == context.Request.Context.FaceIndex)
                ?? detection.Faces.FirstOrDefault();

            if (face == null)
            {
                return RecognitionPipelineStageExecutionResult.Fail(
                    context,
                    RecognitionFailureCodes.NoEmbedding,
                    "No face detected for embedding extraction.");
            }

            embedding = face.Embedding;
        }

        if (embedding == null || embedding.Length == 0)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.NoEmbedding,
                "Query embedding was not provided and could not be extracted.");
        }

        _logger.LogInformation(
            "Recognition embedding extracted. Engine={EngineName} Dimension={Dimension} CorrelationId={CorrelationId}",
            _embeddingEngine.EngineName,
            embedding.Length,
            context.ItemContext.CorrelationId);

        var updated = context with
        {
            QueryEmbedding = embedding,
            State = RecognitionPipelineState.Searching,
        };

        return RecognitionPipelineStageExecutionResult.Ok(updated);
    }
}

public sealed class CandidateRetrievalRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly IRecognitionCandidateProvider _candidateProvider;
    private readonly ILogger<CandidateRetrievalRecognitionPipelineStage> _logger;

    public CandidateRetrievalRecognitionPipelineStage(
        IRecognitionCandidateProvider candidateProvider,
        ILogger<CandidateRetrievalRecognitionPipelineStage> logger)
    {
        _candidateProvider = candidateProvider;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.CandidateRetrieval;

    public string Name => "CandidateRetrieval";

    public int Order => 200;

    public string Description => "Retrieves candidate embeddings without ranking.";

    public string Version => "1.0";

    public async Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _candidateProvider.GetCandidatesAsync(
            context.Request.CandidateFilter,
            cancellationToken);

        _logger.LogInformation(
            "Recognition candidates retrieved. Count={CandidateCount} CorrelationId={CorrelationId}",
            candidates.Count,
            context.ItemContext.CorrelationId);

        if (candidates.Count == 0)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.NoCandidates,
                "No active candidate embeddings found.");
        }

        var updated = context with
        {
            Candidates = candidates,
            State = RecognitionPipelineState.Searching,
        };

        return RecognitionPipelineStageExecutionResult.Ok(updated);
    }
}

public sealed class VectorSearchRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly IVectorSearchEngine _searchEngine;
    private readonly ILogger<VectorSearchRecognitionPipelineStage> _logger;

    public VectorSearchRecognitionPipelineStage(
        IVectorSearchEngine searchEngine,
        ILogger<VectorSearchRecognitionPipelineStage> logger)
    {
        _searchEngine = searchEngine;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.VectorSearch;

    public string Name => "VectorSearch";

    public int Order => 300;

    public string Description => "Executes Top-K vector search.";

    public string Version => "1.0";

    public async Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.QueryEmbedding == null || context.Candidates == null)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.StageFailed,
                "Embedding or candidates missing for vector search.");
        }

        var response = await _searchEngine.SearchAsync(new VectorSearchRequest
        {
            QueryEmbedding = context.QueryEmbedding,
            Candidates = context.Candidates,
            TopK = context.Request.TopK,
            Metric = context.Request.SimilarityMetric,
        }, cancellationToken);

        _logger.LogInformation(
            "Recognition vector search completed. ResultCount={ResultCount} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            response.Results.Count,
            response.Duration.TotalMilliseconds,
            context.ItemContext.CorrelationId);

        var updated = context with
        {
            SearchResults = response.Results,
            State = RecognitionPipelineState.Ranking,
        };

        return RecognitionPipelineStageExecutionResult.Ok(updated);
    }
}

public sealed class SimilarityRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly ISimilarityEngine _similarityEngine;
    private readonly ILogger<SimilarityRecognitionPipelineStage> _logger;

    public SimilarityRecognitionPipelineStage(
        ISimilarityEngine similarityEngine,
        ILogger<SimilarityRecognitionPipelineStage> logger)
    {
        _similarityEngine = similarityEngine;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.Similarity;

    public string Name => "Similarity";

    public int Order => 400;

    public string Description => "Scores and ranks search results.";

    public string Version => "1.0";

    public async Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.SearchResults == null)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.StageFailed,
                "Search results missing for similarity ranking.");
        }

        var ranked = await _similarityEngine.RankAsync(
            context.SearchResults,
            context.Request.SimilarityMetric,
            cancellationToken);

        var statistics = _similarityEngine.ComputeStatistics(ranked);

        var updated = context with
        {
            RankedMatches = ranked,
            SimilarityStatistics = statistics,
            State = RecognitionPipelineState.Evaluating,
        };

        return RecognitionPipelineStageExecutionResult.Ok(updated);
    }
}

public sealed class DecisionRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly IRecognitionDecisionEngine _decisionEngine;
    private readonly IRecognitionPolicy _policy;
    private readonly ILogger<DecisionRecognitionPipelineStage> _logger;

    public DecisionRecognitionPipelineStage(
        IRecognitionDecisionEngine decisionEngine,
        IRecognitionPolicy policy,
        ILogger<DecisionRecognitionPipelineStage> logger)
    {
        _decisionEngine = decisionEngine;
        _policy = policy;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.Decision;

    public string Name => "Decision";

    public int Order => 500;

    public string Description => "Applies recognition policy to ranked matches.";

    public string Version => "1.0";

    public Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.RankedMatches == null || context.Candidates == null || context.SimilarityStatistics == null)
        {
            return Task.FromResult(RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.StageFailed,
                "Ranked matches or statistics missing for decision."));
        }

        var decisionContext = new RecognitionDecisionContext
        {
            RankedMatches = context.RankedMatches,
            Candidates = context.Candidates,
            Policy = _policy,
            Statistics = context.SimilarityStatistics,
        };

        var decision = _decisionEngine.Decide(decisionContext, context.Request.AlreadyAssignedStudentIds);

        _logger.LogInformation(
            "Recognition decision completed. DecisionType={DecisionType} Status={Status} CorrelationId={CorrelationId}",
            decision.DecisionType,
            decision.Status,
            context.ItemContext.CorrelationId);

        var state = decision.DecisionType switch
        {
            RecognitionDecisionType.Recognized => RecognitionPipelineState.Recognized,
            RecognitionDecisionType.Unknown => RecognitionPipelineState.Unknown,
            RecognitionDecisionType.ManualReview or RecognitionDecisionType.Tie or RecognitionDecisionType.LowConfidence
                => RecognitionPipelineState.ManualReview,
            _ => RecognitionPipelineState.Evaluating,
        };

        var updated = context with
        {
            Decision = decision,
            State = state,
        };

        return Task.FromResult(RecognitionPipelineStageExecutionResult.Ok(updated));
    }
}

public sealed class PersistenceRecognitionPipelineStage : IRecognitionPipelineStage
{
    private readonly IRecognitionResultWriter _resultWriter;
    private readonly ILogger<PersistenceRecognitionPipelineStage> _logger;

    public PersistenceRecognitionPipelineStage(
        IRecognitionResultWriter resultWriter,
        ILogger<PersistenceRecognitionPipelineStage> logger)
    {
        _resultWriter = resultWriter;
        _logger = logger;
    }

    public RecognitionPipelineStage? ManifestStage => RecognitionPipelineStage.Persistence;

    public string Name => "Persistence";

    public int Order => 600;

    public string Description => "Persists recognition result and audit metadata.";

    public string Version => "1.0";

    public async Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Decision == null || context.SearchResults == null || context.Statistics == null)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.StageFailed,
                "Decision or statistics missing for persistence.");
        }

        var persistence = await _resultWriter.PersistAsync(new RecognitionPersistenceRequest
        {
            Context = context.ItemContext,
            Decision = context.Decision,
            TopCandidates = context.SearchResults,
            Statistics = context.Statistics,
            BoundingBoxX = context.Request.BoundingBoxX,
            BoundingBoxY = context.Request.BoundingBoxY,
            BoundingBoxWidth = context.Request.BoundingBoxWidth,
            BoundingBoxHeight = context.Request.BoundingBoxHeight,
            FaceImageKey = context.Request.FaceImageKey,
        }, cancellationToken);

        if (!persistence.Success)
        {
            return RecognitionPipelineStageExecutionResult.Fail(
                context,
                RecognitionFailureCodes.StageFailed,
                persistence.FailureReason);
        }

        var updated = context with
        {
            PersistenceResult = persistence,
            State = RecognitionPipelineState.Completed,
        };

        return RecognitionPipelineStageExecutionResult.Ok(updated);
    }
}
