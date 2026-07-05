using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Recognition;

/// <summary>
/// Classroom photo pipeline: detect faces → match students → persist recognitions → AwaitingReview.
/// </summary>
public sealed class ClassroomRecognitionPipeline : IClassroomRecognitionPipeline
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediaObjectReader _mediaReader;
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IFaceMatcher _faceMatcher;
    private readonly IAttendanceSessionSummaryService _summaryService;
    private readonly IClassroomPhotoQueue _queue;
    private readonly InsightFace.InsightFaceOptions _insightFaceOptions;
    private readonly ILogger<ClassroomRecognitionPipeline> _logger;

    public ClassroomRecognitionPipeline(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IMediaObjectReader mediaReader,
        IFaceDetectionService faceDetectionService,
        IFaceMatcher faceMatcher,
        IAttendanceSessionSummaryService summaryService,
        IClassroomPhotoQueue queue,
        IOptions<InsightFace.InsightFaceOptions> insightFaceOptions,
        ILogger<ClassroomRecognitionPipeline> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _mediaReader = mediaReader;
        _faceDetectionService = faceDetectionService;
        _faceMatcher = faceMatcher;
        _summaryService = summaryService;
        _queue = queue;
        _insightFaceOptions = insightFaceOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(ClassroomPhotoMessage message, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var session = await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == message.AttendanceSessionId && s.TenantId == message.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{message.AttendanceSessionId}' was not found.");

        try
        {
            if (session.Status == AttendanceSessionStatus.Draft)
            {
                session.MoveToPending();
            }

            session.MoveToProcessing();
            session.StartedUtc = DateTime.UtcNow;
            session.RecognitionProvider = _faceDetectionService.ProviderName;
            session.RecognitionModel = _faceDetectionService.ModelName;
            session.RecognitionPipelineVersion = _insightFaceOptions.PipelineVersion;
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

            var imageBytes = message.ImageStorageKey.Contains('.', StringComparison.Ordinal)
                ? await _mediaReader.ReadObjectAsync(message.ImageStorageKey, cancellationToken)
                : await _mediaReader.ReadVariantAsync(message.ImageStorageKey, "original", cancellationToken);
            var detection = await _faceDetectionService.DetectAsync(new FaceDetectionRequest(imageBytes), cancellationToken);

            session.SetImageDimensions(detection.ImageWidth, detection.ImageHeight);
            session.DetectedFaces = detection.Faces.Count;

            var studentEmbeddings = await LoadStudentEmbeddingsAsync(session, cancellationToken);
            var matchInputs = detection.Faces
                .Select(f => new DetectedFaceMatchInput(f.FaceIndex, f.Embedding))
                .ToList();

            var matches = _faceMatcher.Match(matchInputs, studentEmbeddings);

            var existingRecognitions = await _context.AttendanceRecognitions
                .Where(r => r.AttendanceSessionId == session.Id)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingRecognitions)
            {
                _context.Remove(existing);
            }

            var recognitions = new List<AttendanceRecognition>();
            foreach (var face in detection.Faces)
            {
                var match = matches.First(m => m.FaceIndex == face.FaceIndex);
                recognitions.Add(new AttendanceRecognition
                {
                    Id = Guid.NewGuid(),
                    TenantId = session.TenantId,
                    AttendanceSessionId = session.Id,
                    StudentId = match.MatchedStudentId,
                    FaceNumber = face.FaceIndex,
                    ImageSequence = 1,
                    FaceImageKey = BuildFaceImageKey(session, face.FaceIndex),
                    RecognitionStatus = match.SuggestedStatus,
                    ConfidenceScore = match.Confidence,
                    EmbeddingDistance = match.Distance,
                    BoundingBoxX = face.BoundingBoxX,
                    BoundingBoxY = face.BoundingBoxY,
                    BoundingBoxWidth = face.BoundingBoxWidth,
                    BoundingBoxHeight = face.BoundingBoxHeight,
                    RecognitionTimeMilliseconds = detection.DetectionDurationMs,
                    CreatedUtc = DateTime.UtcNow
                });
            }

            await _context.AddRangeAsync(recognitions);
            session.RecognizedFaces = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Recognized);
            session.UnknownFaces = recognitions.Count(r => r.RecognitionStatus is RecognitionStatus.Unknown or RecognitionStatus.LowConfidence);
            session.CompletedUtc = DateTime.UtcNow;
            session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;

            await _summaryService.SyncSessionSummaryAsync(session.Id, cancellationToken);
            session.MoveToAwaitingReview();
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _queue.MarkCompleted(session.Id);

            _logger.LogInformation(
                "Classroom recognition completed. SessionId={SessionId} DetectedFaces={DetectedFaces} Recognized={Recognized} DurationMs={DurationMs}",
                session.Id,
                detection.Faces.Count,
                session.RecognizedFaces,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            session.ProcessingError = ex.Message;
            session.CompletedUtc = DateTime.UtcNow;
            session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;
            session.MoveToFailed();
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _queue.MarkCompleted(session.Id);
            throw;
        }
    }

    private async Task<IReadOnlyList<StudentEmbeddingMatchInput>> LoadStudentEmbeddingsAsync(
        AttendanceSession session,
        CancellationToken cancellationToken)
    {
        var studentIds = await _context.Students
            .AsNoTracking()
            .Where(s => s.TenantId == session.TenantId
                        && s.CourseId == session.CourseId
                        && s.GroupId == session.GroupId
                        && s.SemesterId == session.SemesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var embeddings = await _context.StudentFaceEmbeddings
            .AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId)
                        && e.IsActive
                        && e.EmbeddingStatus == EmbeddingStatus.Completed
                        && e.EmbeddingVector.Length > 0)
            .ToListAsync(cancellationToken);

        return embeddings
            .Select(e => new StudentEmbeddingMatchInput(e.StudentId, e.Id, e.EmbeddingVector, e.PhotoVersion))
            .ToList();
    }

    private static string BuildFaceImageKey(AttendanceSession session, int faceNumber) =>
        $"recognitions/{session.TenantId}/{session.Id}/faces/{faceNumber:D5}.webp";
}
