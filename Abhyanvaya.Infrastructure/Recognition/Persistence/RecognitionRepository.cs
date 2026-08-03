using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Recognition.Persistence;

public sealed class RecognitionRepository : IRecognitionRepository
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InsightFaceOptions _insightFaceOptions;
    private readonly ILogger<RecognitionRepository> _logger;

    public RecognitionRepository(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IOptions<InsightFaceOptions> insightFaceOptions,
        ILogger<RecognitionRepository> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _insightFaceOptions = insightFaceOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RecognitionCandidate>> GetActiveEmbeddingsAsync(
        RecognitionCandidateFilter filter,
        CancellationToken cancellationToken = default)
    {
        var studentQuery = _context.Students
            .AsNoTracking()
            .Where(s => s.TenantId == filter.TenantId);

        if (filter.CourseId.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.CourseId == filter.CourseId.Value);
        }

        if (filter.GroupId.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.GroupId == filter.GroupId.Value);
        }

        if (filter.SemesterId.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.SemesterId == filter.SemesterId.Value);
        }

        var students = await studentQuery
            .Select(s => new { s.Id, PhotoTicks = s.PhotoUploadedUtc.HasValue ? s.PhotoUploadedUtc.Value.Ticks : 0L })
            .ToListAsync(cancellationToken);
        var studentIds = students.Select(s => s.Id).ToList();
        var photoVersions = students.ToDictionary(s => s.Id, s => s.PhotoTicks);

        var embeddings = await _context.StudentFaceEmbeddings
            .AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId)
                        && e.TenantId == filter.TenantId
                        && e.IsActive
                        && e.EmbeddingStatus == EmbeddingStatus.Completed
                        && e.EmbeddingVector.Length > 0)
            .ToListAsync(cancellationToken);

        var runtimeModel = _insightFaceOptions.RecognitionModelFile;
        var usable = embeddings
            .Where(e => EmbeddingModelCompatibility.MatchesRuntimeModel(e.EmbeddingModel, runtimeModel))
            .Where(e => !photoVersions.TryGetValue(e.StudentId, out var ticks) || e.PhotoVersion == ticks)
            .ToList();

        if (usable.Count == 0 && embeddings.Count > 0)
        {
            _logger.LogWarning(
                "Recognition gallery empty after model/stale filters. Candidates={Candidates}, RuntimeModel={RuntimeModel}",
                embeddings.Count,
                runtimeModel);
        }

        return usable.Select(MapCandidate).ToList();
    }

    public async Task<RecognitionPersistenceResult> PersistRecognitionAsync(
        RecognitionPersistenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = request.Context;
        var decision = request.Decision;

        var entity = new AttendanceRecognition
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId,
            AttendanceSessionId = ctx.AttendanceSessionId,
            StudentId = decision.StudentId,
            FaceNumber = ctx.FaceIndex,
            ImageSequence = ctx.ImageSequence,
            FaceImageKey = request.FaceImageKey,
            RecognitionStatus = decision.Status,
            ConfidenceScore = decision.Confidence,
            EmbeddingDistance = decision.Distance,
            BoundingBoxX = request.BoundingBoxX,
            BoundingBoxY = request.BoundingBoxY,
            BoundingBoxWidth = request.BoundingBoxWidth,
            BoundingBoxHeight = request.BoundingBoxHeight,
            RecognitionTimeMilliseconds = (int)request.Statistics.TotalDuration.TotalMilliseconds,
            CreatedUtc = DateTime.UtcNow,
        };

        await _context.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recognition result persisted. RecognitionId={RecognitionId} SessionId={SessionId} FaceIndex={FaceIndex} Status={Status} CorrelationId={CorrelationId}",
            entity.Id,
            ctx.AttendanceSessionId,
            ctx.FaceIndex,
            decision.Status,
            ctx.CorrelationId);

        return new RecognitionPersistenceResult
        {
            Success = true,
            RecognitionId = entity.Id,
        };
    }

    private static RecognitionCandidate MapCandidate(StudentFaceEmbedding embedding) =>
        new()
        {
            StudentId = embedding.StudentId,
            EmbeddingId = embedding.Id,
            EmbeddingVector = embedding.EmbeddingVector,
            PhotoVersion = embedding.PhotoVersion,
            EmbeddingModel = embedding.EmbeddingModel,
            EmbeddingVersion = embedding.EmbeddingVersion,
        };
}
