using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Infrastructure.Diagnostics;
using Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;
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
    private readonly IRecognitionMediaService _recognitionMediaService;
    private readonly IAttendanceSessionSummaryService _summaryService;
    private readonly IClassroomPhotoQueue _queue;
    private readonly InsightFace.InsightFaceOptions _insightFaceOptions;
    private readonly IRecognitionPipelineDiagnostics _diagnostics;
    private readonly IRecognitionExecutionContext _executionContext;
    private readonly IRecognitionForensicsAudit _forensics;
    private readonly IRecognitionMemoryAudit _memoryAudit;
    private readonly ILogger<ClassroomRecognitionPipeline> _logger;

    public ClassroomRecognitionPipeline(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IMediaObjectReader mediaReader,
        IFaceDetectionService faceDetectionService,
        IFaceMatcher faceMatcher,
        IRecognitionMediaService recognitionMediaService,
        IAttendanceSessionSummaryService summaryService,
        IClassroomPhotoQueue queue,
        IOptions<InsightFace.InsightFaceOptions> insightFaceOptions,
        IRecognitionPipelineDiagnostics diagnostics,
        IRecognitionExecutionContext executionContext,
        IRecognitionForensicsAudit forensics,
        IRecognitionMemoryAudit memoryAudit,
        ILogger<ClassroomRecognitionPipeline> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _mediaReader = mediaReader;
        _faceDetectionService = faceDetectionService;
        _faceMatcher = faceMatcher;
        _recognitionMediaService = recognitionMediaService;
        _summaryService = summaryService;
        _queue = queue;
        _insightFaceOptions = insightFaceOptions.Value;
        _diagnostics = diagnostics;
        _executionContext = executionContext;
        _forensics = forensics;
        _memoryAudit = memoryAudit;
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

        // AI18.MEMORY.1: diagnostics-only — Begin() activates the scoped memory audit for this job;
        // every call below is a read-only snapshot/record and never influences recognition behavior.
        _memoryAudit.Begin();
        _memoryAudit.Snapshot("Queue Received");

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

            var allSessionImages = await _context.AttendanceSessionImages
                .Where(i => i.AttendanceSessionId == session.Id && i.TenantId == session.TenantId)
                .OrderBy(i => i.ImageSequence)
                .ToListAsync(cancellationToken);

            // Legacy single-image sessions (pre Phase 2) may only have ImageMetadata.ImageKey.
            if (allSessionImages.Count == 0 && !string.IsNullOrWhiteSpace(session.ImageMetadata.ImageKey))
            {
                allSessionImages =
                [
                    new AttendanceSessionImage
                    {
                        Id = Guid.Empty,
                        TenantId = session.TenantId,
                        AttendanceSessionId = session.Id,
                        ImageSequence = 1,
                        ImageKey = session.ImageMetadata.ImageKey!,
                        Status = AttendanceSessionImageStatus.Uploaded,
                    },
                ];
            }

            if (allSessionImages.Count == 0)
            {
                // Fall back to the queue message key (original Phase 1 path).
                allSessionImages =
                [
                    new AttendanceSessionImage
                    {
                        Id = Guid.Empty,
                        TenantId = session.TenantId,
                        AttendanceSessionId = session.Id,
                        ImageSequence = 1,
                        ImageKey = message.ImageStorageKey,
                        Status = AttendanceSessionImageStatus.Uploaded,
                    },
                ];
            }

            var imagesToProcess = ResolveImagesToProcess(allSessionImages, message);
            if (imagesToProcess.Count == 0)
            {
                _logger.LogInformation(
                    "No classroom images required processing for scope {Scope}. SessionId={SessionId}",
                    message.Scope,
                    session.Id);
                session.MoveToAwaitingReview();
                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
                _queue.MarkCompleted(session.Id);
                _diagnostics.Complete();
                return;
            }

            foreach (var sessionImage in imagesToProcess.Where(i => i.Id != Guid.Empty))
            {
                sessionImage.Status = AttendanceSessionImageStatus.Processing;
                sessionImage.ProcessingError = null;
            }

            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

            var sequencesToReplace = imagesToProcess
                .Select(i => i.ImageSequence < 1 ? (short)1 : i.ImageSequence)
                .Distinct()
                .ToHashSet();

            var existingRecognitions = await _context.AttendanceRecognitions
                .Where(r => r.AttendanceSessionId == session.Id)
                .ToListAsync(cancellationToken);

            var recognitionsToRemove = message.Scope == ClassroomRecognitionScope.FullSession
                ? existingRecognitions
                : existingRecognitions.Where(r => sequencesToReplace.Contains(r.ImageSequence)).ToList();

            foreach (var existing in recognitionsToRemove)
            {
                _context.Remove(existing);
            }

            var retainedRecognitions = existingRecognitions
                .Except(recognitionsToRemove)
                .ToList();

            var studentEmbeddings = await LoadStudentEmbeddingsAsync(session, cancellationToken);
            var newRecognitions = new List<AttendanceRecognition>();
            var primaryWidth = session.ImageMetadata.Width ?? 0;
            var primaryHeight = session.ImageMetadata.Height ?? 0;

            foreach (var sessionImage in imagesToProcess)
            {
                var imageSequence = sessionImage.ImageSequence < 1 ? (short)1 : sessionImage.ImageSequence;

                var loadImageStage = _diagnostics.StageStart($"Load Image {imageSequence}");
                _forensics.Checkpoint($"Image {imageSequence} Download Started");
                _memoryAudit.Snapshot("Image Download Started");
                var imageBytes = sessionImage.ImageKey.Contains('.', StringComparison.Ordinal)
                    ? await _mediaReader.ReadObjectAsync(sessionImage.ImageKey, cancellationToken)
                    : await _mediaReader.ReadVariantAsync(sessionImage.ImageKey, "original", cancellationToken);
                _diagnostics.StageEnd(loadImageStage);
                _forensics.Checkpoint($"Image {imageSequence} Download Finished");
                _memoryAudit.Snapshot("Image Download Finished");
                _memoryAudit.RegisterObject("Byte Array", imageBytes.Length, "Image Download Finished");

                var detection = await _faceDetectionService.DetectAsync(new FaceDetectionRequest(imageBytes), cancellationToken);
                _memoryAudit.Snapshot("Face Detection Complete");

                if (imageSequence == 1)
                {
                    primaryWidth = detection.ImageWidth;
                    primaryHeight = detection.ImageHeight;
                    session.SetImageDimensions(detection.ImageWidth, detection.ImageHeight);
                }

                if (sessionImage.Id != Guid.Empty)
                {
                    sessionImage.Width = detection.ImageWidth;
                    sessionImage.Height = detection.ImageHeight;
                }

                var matchInputs = detection.Faces
                    .Select(f => new DetectedFaceMatchInput(f.FaceIndex, f.Embedding))
                    .ToList();

                var matchingStage = _diagnostics.StageStart($"Matching Image {imageSequence}");
                _forensics.Checkpoint($"Before Matching Image {imageSequence}");
                _memoryAudit.Snapshot("Before Matching");
                var beforeMatchingSnapshot = RecognitionMemorySnapshot.Capture();
                var beforeMatchingAuditSnapshot = CaptureMemoryAuditSnapshot("Before Matching (raw)");
                var matches = _faceMatcher.Match(matchInputs, studentEmbeddings);
                var afterMatchingSnapshot = RecognitionMemorySnapshot.Capture();
                var afterMatchingAuditSnapshot = CaptureMemoryAuditSnapshot("After Matching (raw)");
                _forensics.Checkpoint($"After Matching Image {imageSequence}");
                _memoryAudit.Snapshot("After Matching");
                _forensics.RecordMatching(matchInputs.Count, studentEmbeddings.Count, beforeMatchingSnapshot, afterMatchingSnapshot);
                _memoryAudit.RecordMatchingMemory(matchInputs.Count, studentEmbeddings.Count, beforeMatchingAuditSnapshot, afterMatchingAuditSnapshot);
                _diagnostics.StageEnd(matchingStage);

                foreach (var match in matches)
                {
                    _diagnostics.FaceEvent("Matching", match.FaceIndex, matches.Count);
                }

                foreach (var face in detection.Faces)
                {
                    var match = matches.First(m => m.FaceIndex == face.FaceIndex);

                    _memoryAudit.Snapshot("Before Thumbnail Persistence", face.FaceIndex);
                    var thumbnailBytesId = face.AlignedFaceBytes is { Length: > 0 }
                        ? _memoryAudit.RegisterObject("Byte Array", face.AlignedFaceBytes.Length, "Thumbnail Persistence", face.FaceIndex)
                        : -1;

                    var faceImageKey = await _recognitionMediaService.PersistFaceThumbnailAsync(
                        session.TenantId,
                        session.Id,
                        face.FaceIndex,
                        face.AlignedFaceBytes,
                        _executionContext.ExecutionTraceId,
                        cancellationToken,
                        imageSequence);

                    _memoryAudit.DisposeObject(thumbnailBytesId);
                    _memoryAudit.Snapshot("After Thumbnail Persistence", face.FaceIndex);

                    newRecognitions.Add(new AttendanceRecognition
                    {
                        Id = Guid.NewGuid(),
                        TenantId = session.TenantId,
                        AttendanceSessionId = session.Id,
                        StudentId = match.MatchedStudentId,
                        FaceNumber = face.FaceIndex,
                        ImageSequence = imageSequence,
                        FaceImageKey = faceImageKey,
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

                if (sessionImage.Id != Guid.Empty)
                {
                    sessionImage.Status = AttendanceSessionImageStatus.Processed;
                }
            }

            await _context.AddRangeAsync(newRecognitions);

            var mergedRecognitions = retainedRecognitions.Concat(newRecognitions).ToList();
            session.DetectedFaces = mergedRecognitions.Count;
            if (primaryWidth > 0 && primaryHeight > 0)
            {
                session.SetImageDimensions(primaryWidth, primaryHeight);
            }

            session.RecognizedFaces = mergedRecognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Recognized);
            session.UnknownFaces = mergedRecognitions.Count(r => r.RecognitionStatus is RecognitionStatus.Unknown or RecognitionStatus.LowConfidence);
            session.CompletedUtc = DateTime.UtcNow;
            session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;

            await _summaryService.SyncSessionSummaryAsync(session.Id, cancellationToken);
            session.MoveToAwaitingReview();

            var saveStage = _diagnostics.StageStart("Database Save");
            _forensics.Checkpoint("Before Database Save");
            _memoryAudit.Snapshot("Before Database Save");
            var pendingEntityCount = newRecognitions.Count + recognitionsToRemove.Count + 1 + imagesToProcess.Count(i => i.Id != Guid.Empty);
            _memoryAudit.RecordDatabaseSave(
                phase: "Before",
                pendingEntityCount: pendingEntityCount,
                attendanceRecognitionCount: newRecognitions.Count,
                attendanceSessionCount: 1,
                estimatedGraphBytes: newRecognitions.Count * 256L);
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _memoryAudit.RecordDatabaseSave(
                phase: "After",
                pendingEntityCount: 0,
                attendanceRecognitionCount: newRecognitions.Count,
                attendanceSessionCount: 1,
                estimatedGraphBytes: 0);
            _forensics.Checkpoint("After Database Save");
            _memoryAudit.Snapshot("After Database Save");
            _diagnostics.StageEnd(saveStage);

            _queue.MarkCompleted(session.Id);
            _diagnostics.Complete();
            _forensics.Checkpoint("Completed");
            _forensics.FinalizeAudit();
            _memoryAudit.Snapshot("Completed");
            _memoryAudit.Complete();

            _logger.LogInformation(
                "Classroom recognition completed. SessionId={SessionId} Scope={Scope} ProcessedImages={ProcessedCount} DetectedFaces={DetectedFaces} Recognized={Recognized} DurationMs={DurationMs}",
                session.Id,
                message.Scope,
                imagesToProcess.Count,
                session.DetectedFaces,
                session.RecognizedFaces,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _diagnostics.Fail(ex);
            _forensics.Checkpoint("Completed (Failed)");
            _forensics.FinalizeAudit();
            _memoryAudit.Snapshot("Completed (Failed)");
            _memoryAudit.Complete();

            session.ProcessingError = ex.Message;
            session.CompletedUtc = DateTime.UtcNow;
            session.ProcessingMilliseconds = (int)stopwatch.ElapsedMilliseconds;
            session.MoveToFailed();

            var failedImages = await _context.AttendanceSessionImages
                .Where(i => i.AttendanceSessionId == session.Id && i.TenantId == session.TenantId)
                .ToListAsync(cancellationToken);
            foreach (var image in failedImages.Where(i => i.Status == AttendanceSessionImageStatus.Processing))
            {
                image.Status = AttendanceSessionImageStatus.Failed;
                image.ProcessingError = ex.Message;
            }

            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            _queue.MarkCompleted(session.Id);
            throw;
        }
    }

    /// <summary>AI22.7A Phase 3 — select which session images this job should process.</summary>
    private static List<AttendanceSessionImage> ResolveImagesToProcess(
        IReadOnlyList<AttendanceSessionImage> allImages,
        ClassroomPhotoMessage message)
    {
        return message.Scope switch
        {
            ClassroomRecognitionScope.SingleImage when message.TargetImageId is { } targetId =>
                allImages.Where(i => i.Id == targetId).ToList(),
            ClassroomRecognitionScope.SingleImage =>
                allImages
                    .Where(i =>
                        string.Equals(i.ImageKey, message.ImageStorageKey, StringComparison.Ordinal) ||
                        i.Status is AttendanceSessionImageStatus.Uploaded or AttendanceSessionImageStatus.Failed)
                    .Take(1)
                    .ToList(),
            ClassroomRecognitionScope.PendingOnly =>
                allImages
                    .Where(i =>
                        i.Status is AttendanceSessionImageStatus.Uploaded
                            or AttendanceSessionImageStatus.Failed
                            or AttendanceSessionImageStatus.Processing)
                    .ToList(),
            _ => allImages.ToList(),
        };
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

    /// <summary>
    /// AI18.MEMORY.1 — a raw <see cref="MemoryAuditSnapshot"/> capture for before/after deltas that are
    /// reported through a dedicated Record*() call (matching, ONNX) rather than through
    /// <see cref="IRecognitionMemoryAudit.Snapshot"/> itself. Peaks are seeded at 0 here deliberately —
    /// this snapshot's own Peak* fields are never read by the Record*() calls it feeds (only the raw
    /// WorkingSet/Native fields are), so it does not corrupt the audit instance's own running peak state
    /// (which only <see cref="IRecognitionMemoryAudit.Snapshot"/> updates).
    /// </summary>
    private MemoryAuditSnapshot CaptureMemoryAuditSnapshot(string stage) =>
        MemoryAuditSnapshot.Capture(
            ExecutionTraceLog.FormatTraceId(_executionContext),
            stage,
            ExecutionTraceLog.ElapsedSincePipelineStartMs(_executionContext),
            0, 0, 0, 0);

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

        // AI18.MEMORY.1 STEP 4: the projection above (`.Select(s => s.Id)`) materializes only a
        // List<Guid> — no Student entities, no navigation properties, no photos — so
        // studentPhotosLoaded/navigationCollectionsLoaded are false by construction, not by estimate.
        _memoryAudit.RecordEntityFrameworkQuery(
            queryName: "Students (id projection)",
            asNoTracking: true,
            entitiesMaterialized: studentIds.Count,
            navigationPropertiesLoaded: "None",
            studentPhotosLoaded: false,
            navigationCollectionsLoaded: false,
            estimatedGraphBytes: studentIds.Count * 16L);

        var runtimeModel = _insightFaceOptions.RecognitionModelFile;
        var candidateEmbeddings = await _context.StudentFaceEmbeddings
            .AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId)
                        && e.IsActive
                        && e.EmbeddingStatus == EmbeddingStatus.Completed
                        && e.EmbeddingVector.Length > 0)
            .ToListAsync(cancellationToken);

        var photoVersions = await _context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, PhotoTicks = s.PhotoUploadedUtc.HasValue ? s.PhotoUploadedUtc.Value.Ticks : 0L })
            .ToDictionaryAsync(s => s.Id, s => s.PhotoTicks, cancellationToken);

        var skippedWrongModel = candidateEmbeddings
            .Count(e => !EmbeddingModelCompatibility.MatchesRuntimeModel(e.EmbeddingModel, runtimeModel));
        var skippedStale = candidateEmbeddings
            .Count(e => EmbeddingModelCompatibility.MatchesRuntimeModel(e.EmbeddingModel, runtimeModel)
                        && photoVersions.TryGetValue(e.StudentId, out var ticks)
                        && e.PhotoVersion != ticks);

        var embeddings = candidateEmbeddings
            .Where(e => EmbeddingModelCompatibility.MatchesRuntimeModel(e.EmbeddingModel, runtimeModel))
            .Where(e => !photoVersions.TryGetValue(e.StudentId, out var ticks) || e.PhotoVersion == ticks)
            .ToList();

        if (embeddings.Count == 0)
        {
            _logger.LogWarning(
                "Classroom match gallery empty for session {SessionId}. CohortStudents={StudentCount}, Candidates={CandidateCount}, SkippedWrongModel={SkippedWrongModel}, SkippedStalePhoto={SkippedStale}, RuntimeModel={RuntimeModel}. Regenerate embeddings under the current model.",
                session.Id,
                studentIds.Count,
                candidateEmbeddings.Count,
                skippedWrongModel,
                skippedStale,
                runtimeModel);
        }
        else if (skippedWrongModel > 0 || skippedStale > 0)
        {
            _logger.LogWarning(
                "Classroom match gallery filtered embeddings for session {SessionId}. Usable={Usable}, SkippedWrongModel={SkippedWrongModel}, SkippedStalePhoto={SkippedStale}, RuntimeModel={RuntimeModel}",
                session.Id,
                embeddings.Count,
                skippedWrongModel,
                skippedStale,
                runtimeModel);
        }

        // AI18.MEMORY.1 STEP 4/5: this query has no `.Include()` and selects the full
        // StudentFaceEmbedding entity (AsNoTracking) — it never touches Student.PhotoKey or any
        // navigation collection, so studentPhotosLoaded/navigationCollectionsLoaded are false by
        // construction.
        var totalEmbeddingFloats = embeddings.Sum(e => e.EmbeddingVector.Length);
        var estimatedEmbeddingGraphBytes = (long)totalEmbeddingFloats * sizeof(float) + (embeddings.Count * 96L);
        _memoryAudit.RecordEntityFrameworkQuery(
            queryName: "StudentFaceEmbeddings (AsNoTracking)",
            asNoTracking: true,
            entitiesMaterialized: embeddings.Count,
            navigationPropertiesLoaded: "None",
            studentPhotosLoaded: false,
            navigationCollectionsLoaded: false,
            estimatedGraphBytes: estimatedEmbeddingGraphBytes);

        var duplicateStudentIds = embeddings
            .GroupBy(e => e.StudentId)
            .Count(g => g.Count() > 1);
        // The query's own predicate (`EmbeddingVector.Length > 0`) already excludes null/empty vectors,
        // so this is a structural guarantee, not a runtime estimate.
        var nullEmbeddings = 0;

        _memoryAudit.RecordStudentEmbeddingLoad(
            studentsLoaded: studentIds.Count,
            embeddingsLoaded: embeddings.Count,
            embeddingDimensions: embeddings.Count > 0 ? embeddings[0].EmbeddingVector.Length : 0,
            totalFloatCount: totalEmbeddingFloats,
            duplicateStudentIds: duplicateStudentIds,
            nullEmbeddings: nullEmbeddings,
            imageBytesLoaded: false,
            photoLoaded: false,
            navigationLoaded: false);
        _memoryAudit.RegisterObject("StudentEmbedding Collection", estimatedEmbeddingGraphBytes, "After Student Embedding Load");

        var result = embeddings
            .Select(e => new StudentEmbeddingMatchInput(e.StudentId, e.Id, e.EmbeddingVector, e.PhotoVersion))
            .ToList();

        // AI17.RUNTIME.3: diagnostics-only — reports the query shape exactly as written above
        // (AsNoTracking, no .Include(), no lazy-loading proxies registered in this DbContext); does
        // not change the query itself.
        _forensics.RecordStudentEmbeddingLoad(
            studentCount: studentIds.Count,
            embeddingCount: embeddings.Count,
            totalEmbeddingFloats: totalEmbeddingFloats,
            asNoTracking: true,
            navigationPropertiesLoaded: "None",
            lazyLoadingEnabled: false);

        return result;
    }
}
