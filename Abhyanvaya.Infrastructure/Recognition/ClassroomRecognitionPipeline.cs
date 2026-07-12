using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Infrastructure.Diagnostics;
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
    private readonly IRecognitionPipelineDiagnostics _diagnostics;
    private readonly IRecognitionExecutionContext _executionContext;
    private readonly IRecognitionForensicsAudit _forensics;
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
        IRecognitionPipelineDiagnostics diagnostics,
        IRecognitionExecutionContext executionContext,
        IRecognitionForensicsAudit forensics,
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
        _diagnostics = diagnostics;
        _executionContext = executionContext;
        _forensics = forensics;
        _logger = logger;
    }

    public async Task ProcessAsync(ClassroomPhotoMessage message, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // AI15.DIAGNOSTICS.2A/2B/2C: diagnostics-only — logs the moment ProcessAsync begins, before
        // the session even loads, so a death during the DB fetch itself is still visible in logs.
        // MarkPipelineStarted() only records a timestamp on the scoped execution context; it does not
        // participate in any recognition/matching/persistence decision.
        _executionContext.MarkPipelineStarted();
        LogPipelineEntry(message);

        var session = await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == message.AttendanceSessionId && s.TenantId == message.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{message.AttendanceSessionId}' was not found.");

        // AI15.DIAGNOSTICS.1: diagnostics-only instrumentation (memory/timing snapshots + logging).
        // None of the calls below change any value used by the recognition/matching/persistence logic.
        _diagnostics.Begin(session.Id, session.TenantId);

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

            var loadImageStage = _diagnostics.StageStart("Load Image");
            _forensics.Checkpoint("Image Download Started");
            var imageBytes = message.ImageStorageKey.Contains('.', StringComparison.Ordinal)
                ? await _mediaReader.ReadObjectAsync(message.ImageStorageKey, cancellationToken)
                : await _mediaReader.ReadVariantAsync(message.ImageStorageKey, "original", cancellationToken);
            _diagnostics.StageEnd(loadImageStage);
            _forensics.Checkpoint("Image Download Finished");

            var detection = await _faceDetectionService.DetectAsync(new FaceDetectionRequest(imageBytes), cancellationToken);

            session.SetImageDimensions(detection.ImageWidth, detection.ImageHeight);
            session.DetectedFaces = detection.Faces.Count;

            _forensics.Checkpoint("Before Student Embedding Load");
            var studentEmbeddings = await LoadStudentEmbeddingsAsync(session, cancellationToken);
            _forensics.Checkpoint("After Student Embedding Load");
            var matchInputs = detection.Faces
                .Select(f => new DetectedFaceMatchInput(f.FaceIndex, f.Embedding))
                .ToList();

            var matchingStage = _diagnostics.StageStart("Matching");
            _forensics.Checkpoint("Before Matching");
            var beforeMatchingSnapshot = RecognitionMemorySnapshot.Capture();
            var matches = _faceMatcher.Match(matchInputs, studentEmbeddings);
            var afterMatchingSnapshot = RecognitionMemorySnapshot.Capture();
            _forensics.Checkpoint("After Matching");
            _forensics.RecordMatching(matchInputs.Count, studentEmbeddings.Count, beforeMatchingSnapshot, afterMatchingSnapshot);
            _diagnostics.StageEnd(matchingStage);

            // Per-face matching visibility (Task 3): the matcher itself is called once for the whole
            // batch above and is completely untouched — this loop only reports, per face, the result
            // that batched call already computed, without altering matching logic in any way.
            foreach (var match in matches)
            {
                _diagnostics.FaceEvent("Matching", match.FaceIndex, matches.Count);
            }

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

            var saveStage = _diagnostics.StageStart("Database Save");
            _forensics.Checkpoint("Before Database Save");
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _forensics.Checkpoint("After Database Save");
            _diagnostics.StageEnd(saveStage);

            _queue.MarkCompleted(session.Id);
            _diagnostics.Complete();
            _forensics.Checkpoint("Completed");
            _forensics.FinalizeAudit();

            _logger.LogInformation(
                "Classroom recognition completed. SessionId={SessionId} DetectedFaces={DetectedFaces} Recognized={Recognized} DurationMs={DurationMs}",
                session.Id,
                detection.Faces.Count,
                session.RecognizedFaces,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _diagnostics.Fail(ex);
            _forensics.Checkpoint("Completed (Failed)");
            _forensics.FinalizeAudit();

            session.ProcessingError = ex.Message;
            session.CompletedUtc = DateTime.UtcNow;
            session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;
            session.MoveToFailed();
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _queue.MarkCompleted(session.Id);
            throw;
        }
    }

    // AI15.DIAGNOSTICS.2A/2B/2C: pipeline-entry checkpoint — read-only snapshot + logging, no
    // influence on message/session data or control flow.
    private void LogPipelineEntry(ClassroomPhotoMessage message)
    {
        try
        {
            var snapshot = RecognitionMemorySnapshot.Capture();

            _logger.LogInformation("====================================================");
            _logger.LogInformation("PIPELINE ENTRY");
            _logger.LogInformation("  Attendance Session Id              : {AttendanceSessionId}", message.AttendanceSessionId);
            _logger.LogInformation("  Tenant Id                          : {TenantId}", message.TenantId);
            _logger.LogInformation("  Storage Key                        : {StorageKey}", message.ImageStorageKey);
            _logger.LogInformation("  Current UTC                        : {CurrentUtc:O}", snapshot.TimestampUtc);
            _logger.LogInformation("  Managed Heap                       : {ManagedHeapMB} MB", snapshot.ManagedHeapMegabytes);
            _logger.LogInformation("  Working Set                        : {WorkingSetMB} MB", snapshot.WorkingSetMegabytes);
            _logger.LogInformation("  Private Memory                     : {PrivateMemoryMB} MB", snapshot.PrivateMegabytes);
            _logger.LogInformation("  Current Thread                     : {ThreadId}", snapshot.ThreadId);
            _logger.LogInformation("  Current Task Id                    : {TaskId}", Task.CurrentId);
            _logger.LogInformation("====================================================");

            ExecutionTraceLog.LogBlock(_logger, _executionContext, _insightFaceOptions.PipelineVersion, EmbeddingProviders.InsightFace);
        }
        catch (Exception ex)
        {
            // Diagnostics-only: a logging failure here must never prevent the pipeline from running.
            _logger.LogWarning(ex, "Pipeline entry diagnostics logging failed; continuing without it.");
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

        var result = embeddings
            .Select(e => new StudentEmbeddingMatchInput(e.StudentId, e.Id, e.EmbeddingVector, e.PhotoVersion))
            .ToList();

        // AI17.RUNTIME.3: diagnostics-only — reports the query shape exactly as written above
        // (AsNoTracking, no .Include(), no lazy-loading proxies registered in this DbContext); does
        // not change the query itself.
        var totalEmbeddingFloats = embeddings.Sum(e => e.EmbeddingVector.Length);
        _forensics.RecordStudentEmbeddingLoad(
            studentCount: studentIds.Count,
            embeddingCount: embeddings.Count,
            totalEmbeddingFloats: totalEmbeddingFloats,
            asNoTracking: true,
            navigationPropertiesLoaded: "None",
            lazyLoadingEnabled: false);

        return result;
    }

    private static string BuildFaceImageKey(AttendanceSession session, int faceNumber) =>
        $"recognitions/{session.TenantId}/{session.Id}/faces/{faceNumber:D5}.webp";
}
