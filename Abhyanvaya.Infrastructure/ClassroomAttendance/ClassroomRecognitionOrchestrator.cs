using System.Diagnostics;
using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class ClassroomRecognitionOrchestrator : IClassroomRecognitionOrchestrator
{
    private readonly IAttendanceSessionManager _sessionManager;
    private readonly IMediaObjectReader _mediaReader;
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IMultiFaceRecognitionCoordinator _multiFaceCoordinator;
    private readonly IAttendanceValidationService _validationService;
    private readonly IAttendanceConflictResolver _conflictResolver;
    private readonly IAttendanceDecisionEngine _decisionEngine;
    private readonly IAttendanceResultWriter _resultWriter;
    private readonly IAttendanceRecognitionRepository _recognitionRepository;
    private readonly IClassroomPhotoQueue _queue;
    private readonly IAttendancePolicy _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClassroomRecognitionOrchestrator> _logger;

    public ClassroomRecognitionOrchestrator(
        IAttendanceSessionManager sessionManager,
        IMediaObjectReader mediaReader,
        IFaceDetectionService faceDetectionService,
        IMultiFaceRecognitionCoordinator multiFaceCoordinator,
        IAttendanceValidationService validationService,
        IAttendanceConflictResolver conflictResolver,
        IAttendanceDecisionEngine decisionEngine,
        IAttendanceResultWriter resultWriter,
        IAttendanceRecognitionRepository recognitionRepository,
        IClassroomPhotoQueue queue,
        IAttendancePolicy policy,
        IUnitOfWork unitOfWork,
        ILogger<ClassroomRecognitionOrchestrator> logger)
    {
        _sessionManager = sessionManager;
        _mediaReader = mediaReader;
        _faceDetectionService = faceDetectionService;
        _multiFaceCoordinator = multiFaceCoordinator;
        _validationService = validationService;
        _conflictResolver = conflictResolver;
        _decisionEngine = decisionEngine;
        _resultWriter = resultWriter;
        _recognitionRepository = recognitionRepository;
        _queue = queue;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AttendanceSessionResult> ProcessSessionAsync(
        ClassroomPhotoMessage message,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var recognitionStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Classroom attendance orchestration started. SessionId={SessionId} TenantId={TenantId} CorrelationId={CorrelationId}",
            message.AttendanceSessionId,
            message.TenantId,
            correlationId);

        _ = new AttendanceSessionStarted(message.AttendanceSessionId, message.TenantId, correlationId, DateTime.UtcNow);

        var session = await _sessionManager.LoadSessionAsync(message.AttendanceSessionId, message.TenantId, cancellationToken);

        try
        {
            if (!_policy.AllowReRecognition)
            {
                await _recognitionRepository.ReplaceSessionRecognitionsAsync(
                    message.AttendanceSessionId,
                    message.TenantId,
                    cancellationToken);
            }

            await _sessionManager.BeginProcessingAsync(session, cancellationToken);

            var metadata = _sessionManager.CreateMetadata(session, message.ImageStorageKey);
            var context = new AttendanceSessionContext
            {
                Session = metadata,
                CorrelationId = correlationId,
                State = AttendanceSessionState.Detecting,
                Policy = _policy,
            };

            var imageBytes = message.ImageStorageKey.Contains('.', StringComparison.Ordinal)
                ? await _mediaReader.ReadObjectAsync(message.ImageStorageKey, cancellationToken)
                : await _mediaReader.ReadVariantAsync(message.ImageStorageKey, "original", cancellationToken);

            var detection = await _faceDetectionService.DetectAsync(new FaceDetectionRequest(imageBytes), cancellationToken);
            session.SetImageDimensions(detection.ImageWidth, detection.ImageHeight);

            context = context with
            {
                DetectedFaces = detection.Faces,
                State = AttendanceSessionState.Recognizing,
            };

            recognitionStopwatch.Restart();
            var outcomes = await _multiFaceCoordinator.RecognizeFacesAsync(
                context,
                detection.Faces,
                imageBytes,
                cancellationToken);
            recognitionStopwatch.Stop();

            _ = new SessionRecognitionCompleted(
                message.AttendanceSessionId,
                correlationId,
                outcomes.Count,
                recognitionStopwatch.Elapsed,
                DateTime.UtcNow);

            context = context with
            {
                RecognitionOutcomes = outcomes,
                State = AttendanceSessionState.Validating,
            };

            var validation = _validationService.Validate(context);
            _ = new ValidationCompleted(
                message.AttendanceSessionId,
                correlationId,
                validation.ValidOutcomes?.Count ?? 0,
                validation.Errors?.Count ?? 0,
                DateTime.UtcNow);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join("; ", validation.Errors ?? Array.Empty<string>()));
            }

            context = context with
            {
                RecognitionOutcomes = validation.ValidOutcomes,
                State = AttendanceSessionState.ResolvingConflicts,
            };

            var conflictResult = _conflictResolver.Resolve(context);
            _ = new ConflictResolved(
                message.AttendanceSessionId,
                correlationId,
                conflictResult.ResolvedConflicts.Count,
                DateTime.UtcNow);

            context = context with
            {
                RecognitionOutcomes = conflictResult.ResolvedOutcomes,
                Conflicts = conflictResult.ResolvedConflicts,
            };

            var decisionStopwatch = Stopwatch.StartNew();
            var decisions = _decisionEngine.Decide(context);
            decisionStopwatch.Stop();

            context = context with { Decisions = decisions, State = AttendanceSessionState.WritingAttendance };

            var statistics = BuildStatistics(outcomes, decisions, recognitionStopwatch.Elapsed, decisionStopwatch.Elapsed, stopwatch.Elapsed);
            context = context with { Statistics = statistics };

            var persistenceStopwatch = Stopwatch.StartNew();
            var persistence = await _resultWriter.PersistAsync(new AttendancePersistenceRequest
            {
                Context = context,
                Decisions = decisions,
                Statistics = statistics,
            }, cancellationToken);
            persistenceStopwatch.Stop();

            statistics = statistics with { PersistenceTime = persistenceStopwatch.Elapsed };
            _ = new AttendanceWritten(message.AttendanceSessionId, correlationId, persistence.DecisionsPersisted, DateTime.UtcNow);

            await _sessionManager.CompleteProcessingAsync(session, statistics, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _queue.MarkCompleted(message.AttendanceSessionId);

            stopwatch.Stop();
            _ = new ClassroomOrchestrationCompleted(
                message.AttendanceSessionId,
                correlationId,
                AttendanceSessionState.Completed,
                stopwatch.Elapsed,
                DateTime.UtcNow);

            _logger.LogInformation(
                "Classroom attendance orchestration completed. SessionId={SessionId} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                message.AttendanceSessionId,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return new AttendanceSessionResult
            {
                Success = true,
                SessionId = message.AttendanceSessionId,
                TenantId = message.TenantId,
                State = AttendanceSessionState.Completed,
                Decisions = decisions,
                Statistics = statistics,
                CorrelationId = correlationId,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _sessionManager.FailProcessingAsync(session, ex.Message, cancellationToken);
            _queue.MarkCompleted(message.AttendanceSessionId);

            _logger.LogError(
                ex,
                "Classroom attendance orchestration failed. SessionId={SessionId} CorrelationId={CorrelationId}",
                message.AttendanceSessionId,
                correlationId);

            return new AttendanceSessionResult
            {
                Success = false,
                SessionId = message.AttendanceSessionId,
                TenantId = message.TenantId,
                State = AttendanceSessionState.Failed,
                FailureReason = ex.Message,
                CorrelationId = correlationId,
            };
        }
    }

    private static AttendanceSessionStatistics BuildStatistics(
        IReadOnlyList<FaceRecognitionOutcome> outcomes,
        IReadOnlyList<AttendanceDecision> decisions,
        TimeSpan recognitionTime,
        TimeSpan decisionTime,
        TimeSpan totalDuration) =>
        new()
        {
            DetectedFaces = outcomes.Count,
            StudentsPresent = decisions.Count(d => d.DecisionType is AttendanceDecisionType.Present or AttendanceDecisionType.Late),
            StudentsAbsent = 0,
            UnknownFaces = decisions.Count(d => d.DecisionType == AttendanceDecisionType.Unknown),
            Duplicates = decisions.Count(d => d.DecisionType == AttendanceDecisionType.Duplicate),
            ManualReviews = decisions.Count(d => d.DecisionType == AttendanceDecisionType.ManualReview),
            RecognitionTime = recognitionTime,
            DecisionTime = decisionTime,
            TotalDuration = totalDuration,
        };
}
