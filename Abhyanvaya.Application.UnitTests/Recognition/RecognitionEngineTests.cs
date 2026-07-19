using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Recognition.Engine;
using Abhyanvaya.Infrastructure.Recognition.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Recognition;

public sealed class RecognitionEngineTests
{
    private readonly SimilarityEngine _similarityEngine;
    private readonly PostgreSqlVectorDatabaseProvider _vectorProvider;
    private readonly VectorSearchEngine _searchEngine;
    private readonly RecognitionDecisionEngine _decisionEngine;
    private readonly ConfigurableRecognitionPolicy _policy;

    public RecognitionEngineTests()
    {
        var providers = new ISimilarityProvider[]
        {
            new CosineSimilarityProvider(),
            new EuclideanSimilarityProvider(),
            new InnerProductSimilarityProvider(),
        };
        _similarityEngine = new SimilarityEngine(providers);
        _vectorProvider = new PostgreSqlVectorDatabaseProvider(
            _similarityEngine,
            NullLogger<PostgreSqlVectorDatabaseProvider>.Instance);
        _searchEngine = new VectorSearchEngine(_vectorProvider);
        _decisionEngine = new RecognitionDecisionEngine();
        _policy = new ConfigurableRecognitionPolicy(Options.Create(new RecognitionEngineOptions
        {
            MatchDistanceThreshold = 0.45f,
            LowConfidenceDistanceThreshold = 0.55f,
            MinimumConfidence = 55f,
            TieThreshold = 0.02f,
            ManualReviewEnabled = true,
        }));
    }

    [Fact]
    public async Task VectorSearch_ReturnsTopK_OrderedBySimilarity()
    {
        var query = Normalize(new float[] { 1f, 0f, 0f });
        var candidates = new[]
        {
            CreateCandidate(1, new float[] { 1f, 0f, 0f }),
            CreateCandidate(2, new float[] { 0.9f, 0.1f, 0f }),
            CreateCandidate(3, new float[] { 0f, 1f, 0f }),
        };

        var response = await _searchEngine.SearchAsync(new VectorSearchRequest
        {
            QueryEmbedding = query,
            Candidates = candidates,
            TopK = 2,
            Metric = SimilarityMetric.Cosine,
        });

        Assert.Equal(2, response.Results.Count);
        Assert.Equal(1, response.Results[0].StudentId);
        Assert.Equal(2, response.Results[1].StudentId);
        Assert.True(response.Results[0].SimilarityScore >= response.Results[1].SimilarityScore);
    }

    [Fact]
    public async Task SimilarityEngine_RanksMatchesDescending()
    {
        var searchResults = new[]
        {
            new RecognitionSearchResult { StudentId = 1, EmbeddingId = Guid.NewGuid(), SimilarityScore = 0.7f, Rank = 1, Distance = 0.3f },
            new RecognitionSearchResult { StudentId = 2, EmbeddingId = Guid.NewGuid(), SimilarityScore = 0.9f, Rank = 2, Distance = 0.1f },
        };

        var ranked = await _similarityEngine.RankAsync(searchResults, SimilarityMetric.Cosine);

        Assert.Equal(2, ranked[0].StudentId);
        Assert.Equal(1, ranked[1].StudentId);
    }

    [Fact]
    public void DecisionEngine_Recognizes_WhenWithinThreshold()
    {
        var decision = Decide(new SimilarityMatch
        {
            StudentId = 42,
            EmbeddingId = Guid.NewGuid(),
            NormalizedScore = 0.8f,
            RawDistance = 0.2f,
            Rank = 1,
        });

        Assert.Equal(RecognitionDecisionType.Recognized, decision.DecisionType);
        Assert.Equal(RecognitionStatus.Recognized, decision.Status);
        Assert.Equal(42, decision.StudentId);
    }

    [Fact]
    public void DecisionEngine_ReturnsUnknown_WhenNoMatches()
    {
        var decision = _decisionEngine.Decide(new RecognitionDecisionContext
        {
            RankedMatches = Array.Empty<SimilarityMatch>(),
            Candidates = Array.Empty<RecognitionCandidate>(),
            Policy = _policy,
            Statistics = new SimilarityStatistics(),
        });

        Assert.Equal(RecognitionDecisionType.Unknown, decision.DecisionType);
        Assert.Equal(RecognitionStatus.Unknown, decision.Status);
    }

    [Fact]
    public void DecisionEngine_ReturnsDuplicate_WhenStudentAlreadyAssigned()
    {
        var decision = Decide(
            new SimilarityMatch
            {
                StudentId = 7,
                EmbeddingId = Guid.NewGuid(),
                NormalizedScore = 0.85f,
                RawDistance = 0.15f,
                Rank = 1,
            },
            alreadyAssigned: new HashSet<int> { 7 });

        Assert.Equal(RecognitionDecisionType.Duplicate, decision.DecisionType);
        Assert.Equal(RecognitionStatus.Duplicate, decision.Status);
    }

    [Fact]
    public void DecisionEngine_ReturnsTie_WhenTopCandidatesTooClose()
    {
        var decision = _decisionEngine.Decide(new RecognitionDecisionContext
        {
            RankedMatches = new[]
            {
                new SimilarityMatch { StudentId = 1, EmbeddingId = Guid.NewGuid(), NormalizedScore = 0.80f, RawDistance = 0.20f, Rank = 1 },
                new SimilarityMatch { StudentId = 2, EmbeddingId = Guid.NewGuid(), NormalizedScore = 0.79f, RawDistance = 0.21f, Rank = 2 },
            },
            Candidates = Array.Empty<RecognitionCandidate>(),
            Policy = _policy,
            Statistics = new SimilarityStatistics { BestScore = 0.80f, MatchCount = 2 },
        });

        Assert.Equal(RecognitionDecisionType.Tie, decision.DecisionType);
        Assert.True(decision.RequiresManualReview);
    }

    [Fact]
    public void DecisionEngine_ReturnsLowConfidence_InMiddleBand()
    {
        var decision = Decide(new SimilarityMatch
        {
            StudentId = 5,
            EmbeddingId = Guid.NewGuid(),
            NormalizedScore = 0.5f,
            RawDistance = 0.5f,
            Rank = 1,
        });

        Assert.Equal(RecognitionDecisionType.LowConfidence, decision.DecisionType);
        Assert.Equal(RecognitionStatus.LowConfidence, decision.Status);
    }

    [Fact]
    public async Task CandidateProvider_UsesTenantStrategy_AsFallback()
    {
        var repository = new Mock<IRecognitionRepository>();
        repository.Setup(r => r.GetActiveEmbeddingsAsync(It.IsAny<RecognitionCandidateFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateCandidate(1, new float[] { 1f, 0f }) });

        var provider = new RecognitionCandidateProvider(
            new IRecognitionCandidateStrategy[] { new TenantCandidateStrategy() },
            repository.Object);

        var filter = new RecognitionCandidateFilter { TenantId = 1 };
        var candidates = await provider.GetCandidatesAsync(filter);

        Assert.Single(candidates);
        repository.Verify(r => r.GetActiveEmbeddingsAsync(filter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Orchestrator_DelegatesToExecutor()
    {
        var executor = new Mock<IRecognitionPipelineExecutor>();
        var expected = new RecognitionResult
        {
            Success = true,
            RecognitionRequestId = Guid.NewGuid(),
            AttendanceSessionId = Guid.NewGuid(),
            TenantId = 1,
            FaceIndex = 1,
            Status = RecognitionPipelineState.Completed,
        };

        executor.Setup(e => e.ExecuteAsync(It.IsAny<Application.Recognition.Orchestration.RecognitionPipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var orchestrator = new RecognitionOrchestrator(executor.Object, NullLogger<RecognitionOrchestrator>.Instance);
        var request = CreateRequest(queryEmbedding: new float[] { 1f, 0f });

        var result = await orchestrator.RecognizeAsync(request);

        Assert.True(result.Success);
        executor.Verify(e => e.ExecuteAsync(It.IsAny<Application.Recognition.Orchestration.RecognitionPipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VectorSearch_SupportsConcurrentRequests()
    {
        var query = Normalize(new float[] { 1f, 0f, 0f });
        var candidates = Enumerable.Range(1, 50)
            .Select(i => CreateCandidate(i, Normalize(new float[] { 1f, i * 0.001f, 0f })))
            .ToList();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _searchEngine.SearchAsync(new VectorSearchRequest
            {
                QueryEmbedding = query,
                Candidates = candidates,
                TopK = 5,
                Metric = SimilarityMetric.Cosine,
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(5, r.Results.Count));
    }

    [Fact]
    public async Task VectorSearch_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _searchEngine.SearchAsync(new VectorSearchRequest
            {
                QueryEmbedding = new float[] { 1f, 0f },
                Candidates = new[] { CreateCandidate(1, new float[] { 1f, 0f }) },
                TopK = 1,
            }, cts.Token));
    }

    private RecognitionDecision Decide(SimilarityMatch best, IReadOnlySet<int>? alreadyAssigned = null) =>
        _decisionEngine.Decide(new RecognitionDecisionContext
        {
            RankedMatches = new[] { best },
            Candidates = Array.Empty<RecognitionCandidate>(),
            Policy = _policy,
            Statistics = new SimilarityStatistics { BestScore = best.NormalizedScore, MatchCount = 1 },
        }, alreadyAssigned);

    private static RecognitionCandidate CreateCandidate(int studentId, float[] vector) =>
        new()
        {
            StudentId = studentId,
            EmbeddingId = Guid.NewGuid(),
            EmbeddingVector = Normalize(vector),
            PhotoVersion = 1,
            EmbeddingModel = "test",
            EmbeddingVersion = "1.0",
        };

    private static float[] Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        return magnitude <= 0 ? vector : vector.Select(v => v / magnitude).ToArray();
    }

    private static RecognitionPipelineRequest CreateRequest(float[] queryEmbedding) =>
        new()
        {
            Context = new RecognitionRequestContext
            {
                RecognitionRequestId = Guid.NewGuid(),
                AttendanceSessionId = Guid.NewGuid(),
                TenantId = 1,
                CorrelationId = Guid.NewGuid(),
                PipelineVersion = 1,
                FaceIndex = 1,
            },
            QueryEmbedding = queryEmbedding,
            CandidateFilter = new RecognitionCandidateFilter { TenantId = 1 },
        };
}
