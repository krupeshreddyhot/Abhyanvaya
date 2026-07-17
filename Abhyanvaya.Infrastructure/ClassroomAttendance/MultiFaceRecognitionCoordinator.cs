using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Recognition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class MultiFaceRecognitionCoordinator : IMultiFaceRecognitionCoordinator
{
    private readonly IRecognitionOrchestrator _recognitionOrchestrator;
    private readonly IRecognitionMediaService _recognitionMediaService;
    private readonly ClassroomAttendanceOptions _options;
    private readonly ILogger<MultiFaceRecognitionCoordinator> _logger;

    public MultiFaceRecognitionCoordinator(
        IRecognitionOrchestrator recognitionOrchestrator,
        IRecognitionMediaService recognitionMediaService,
        IOptions<ClassroomAttendanceOptions> options,
        ILogger<MultiFaceRecognitionCoordinator> logger)
    {
        _recognitionOrchestrator = recognitionOrchestrator;
        _recognitionMediaService = recognitionMediaService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FaceRecognitionOutcome>> RecognizeFacesAsync(
        AttendanceSessionContext context,
        IReadOnlyList<DetectedFaceDto> faces,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var outcomes = new List<FaceRecognitionOutcome>(faces.Count);
        var assignedStudentIds = new HashSet<int>();

        foreach (var face in faces.OrderBy(f => f.FaceIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? faceImageKey = null;
            if (face.AlignedFaceBytes is { Length: > 0 })
            {
                faceImageKey = await _recognitionMediaService.PersistFaceThumbnailAsync(
                    context.Session.TenantId,
                    context.Session.SessionId,
                    face.FaceIndex,
                    face.AlignedFaceBytes,
                    context.CorrelationId,
                    cancellationToken);
            }

            var request = BuildRecognitionRequest(context, face, imageBytes, faceImageKey, assignedStudentIds);
            var result = await _recognitionOrchestrator.RecognizeAsync(request, cancellationToken);

            if (result.Success && result.Decision?.StudentId is int studentId
                && result.Decision.Status == Domain.Enums.RecognitionStatus.Recognized)
            {
                assignedStudentIds.Add(studentId);
            }

            outcomes.Add(new FaceRecognitionOutcome
            {
                FaceIndex = face.FaceIndex,
                DetectedFace = face,
                RecognitionResult = result,
                FaceImageKey = faceImageKey,
            });

            _logger.LogInformation(
                "Face recognition completed. SessionId={SessionId} FaceIndex={FaceIndex} Success={Success} CorrelationId={CorrelationId}",
                context.Session.SessionId,
                face.FaceIndex,
                result.Success,
                context.CorrelationId);
        }

        return outcomes;
    }

    private RecognitionPipelineRequest BuildRecognitionRequest(
        AttendanceSessionContext context,
        DetectedFaceDto face,
        byte[] imageBytes,
        string? faceImageKey,
        IReadOnlySet<int> assignedStudentIds) =>
        new()
        {
            Context = new RecognitionRequestContext
            {
                RecognitionRequestId = Guid.NewGuid(),
                AttendanceSessionId = context.Session.SessionId,
                TenantId = context.Session.TenantId,
                CorrelationId = context.CorrelationId,
                PipelineVersion = _options.PipelineVersion,
                FaceIndex = face.FaceIndex,
            },
            QueryEmbedding = face.Embedding,
            ImageBytes = face.Embedding.Length == 0 ? imageBytes : null,
            CandidateFilter = new RecognitionCandidateFilter
            {
                TenantId = context.Session.TenantId,
                CourseId = context.Session.CourseId,
                GroupId = context.Session.GroupId,
                SemesterId = context.Session.SemesterId,
                AttendanceSessionId = context.Session.SessionId,
                Scope = RecognitionCandidateScope.AttendanceSession,
            },
            TopK = _options.DefaultTopK,
            BoundingBoxX = face.BoundingBoxX,
            BoundingBoxY = face.BoundingBoxY,
            BoundingBoxWidth = face.BoundingBoxWidth,
            BoundingBoxHeight = face.BoundingBoxHeight,
            FaceImageKey = faceImageKey,
            AlreadyAssignedStudentIds = assignedStudentIds.Count > 0 ? assignedStudentIds : null,
        };
}
