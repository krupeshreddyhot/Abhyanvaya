using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ClassroomAttendance;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ClassroomAttendance;

public sealed class ClassroomAttendanceFrameworkTests
{
    private readonly ConfigurableAttendancePolicy _policy = new(Options.Create(new ClassroomAttendanceOptions
    {
        MinimumConfidence = 55f,
        ManualReviewEnabled = true,
        AllowDuplicateStudents = false,
    }));

    [Fact]
    public void DecisionEngine_MarksPresent_WhenRecognizedWithConfidence()
    {
        var engine = new AttendanceDecisionEngine(new ManualReviewService());
        var context = CreateContext(CreateOutcome(1, RecognitionDecisionType.Recognized, RecognitionStatus.Recognized, 42, 80m));

        var decisions = engine.Decide(context);

        Assert.Single(decisions);
        Assert.Equal(AttendanceDecisionType.Present, decisions[0].DecisionType);
        Assert.Equal(42, decisions[0].StudentId);
    }

    [Fact]
    public void DecisionEngine_MarksUnknown_WhenNoRecognitionDecision()
    {
        var engine = new AttendanceDecisionEngine(new ManualReviewService());
        var context = CreateContext(new FaceRecognitionOutcome
        {
            FaceIndex = 1,
            DetectedFace = new DetectedFaceDto { FaceIndex = 1, Embedding = [1f, 0f] },
            RecognitionResult = null,
        });

        var decisions = engine.Decide(context);

        Assert.Equal(AttendanceDecisionType.Unknown, decisions[0].DecisionType);
    }

    [Fact]
    public void DecisionEngine_MarksDuplicate_WhenSameStudentTwice()
    {
        var engine = new AttendanceDecisionEngine(new ManualReviewService());
        var outcomes = new[]
        {
            CreateOutcome(1, RecognitionDecisionType.Recognized, RecognitionStatus.Recognized, 7, 85m),
            CreateOutcome(2, RecognitionDecisionType.Duplicate, RecognitionStatus.Duplicate, 7, 82m),
        };
        var context = CreateContext(outcomes);

        var decisions = engine.Decide(context);

        Assert.Equal(2, decisions.Count);
        Assert.Equal(AttendanceDecisionType.Present, decisions[0].DecisionType);
        Assert.Equal(AttendanceDecisionType.Duplicate, decisions[1].DecisionType);
    }

    [Fact]
    public void ConflictResolver_DetectsDuplicateStudent()
    {
        var resolver = new AttendanceConflictResolver(new IAttendanceConflictStrategy[]
        {
            new HighestConfidenceConflictStrategy(),
            new ManualReviewConflictStrategy(),
        });

        var outcomes = new[]
        {
            CreateOutcome(1, RecognitionDecisionType.Recognized, RecognitionStatus.Recognized, 5, 90m),
            CreateOutcome(2, RecognitionDecisionType.Recognized, RecognitionStatus.Recognized, 5, 88m),
        };

        var result = resolver.Resolve(CreateContext(outcomes));

        Assert.Contains(result.ResolvedConflicts, c => c.ConflictType == AttendanceConflictType.DuplicateStudent);
    }

    [Fact]
    public void ValidationService_RejectsEmptyOutcomes()
    {
        var service = new AttendanceValidationService(NullLogger<AttendanceValidationService>.Instance);
        var result = service.Validate(CreateContext(Array.Empty<FaceRecognitionOutcome>()));

        Assert.False(result.IsValid);
        Assert.NotNull(result.Errors);
    }

    [Fact]
    public async Task MultiFaceCoordinator_DispatchesRecognitionPerFace()
    {
        var orchestrator = new Mock<IRecognitionOrchestrator>();
        orchestrator.Setup(o => o.RecognizeAsync(It.IsAny<RecognitionPipelineRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecognitionPipelineRequest req, CancellationToken _) => new RecognitionResult
            {
                Success = true,
                RecognitionRequestId = req.Context.RecognitionRequestId,
                AttendanceSessionId = req.Context.AttendanceSessionId,
                TenantId = req.Context.TenantId,
                FaceIndex = req.Context.FaceIndex,
                Status = RecognitionPipelineState.Recognized,
                Decision = new RecognitionDecision
                {
                    DecisionType = RecognitionDecisionType.Recognized,
                    Status = RecognitionStatus.Recognized,
                    StudentId = req.Context.FaceIndex,
                    Confidence = 80,
                    Distance = 0.2m,
                },
                PersistedRecognitionId = Guid.NewGuid(),
            });

        var media = new Mock<IRecognitionMediaService>();
        media.Setup(m => m.PersistFaceThumbnailAsync(
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<byte[]?>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<short>()))
            .ReturnsAsync("face-key");

        var coordinator = new MultiFaceRecognitionCoordinator(
            orchestrator.Object,
            media.Object,
            Options.Create(new ClassroomAttendanceOptions()),
            NullLogger<MultiFaceRecognitionCoordinator>.Instance);

        var faces = new[]
        {
            new DetectedFaceDto { FaceIndex = 1, Embedding = [1f, 0f], AlignedFaceBytes = [1, 2, 3] },
            new DetectedFaceDto { FaceIndex = 2, Embedding = [0f, 1f], AlignedFaceBytes = [4, 5, 6] },
        };

        var outcomes = await coordinator.RecognizeFacesAsync(
            CreateContext(Array.Empty<FaceRecognitionOutcome>()),
            faces,
            [0xFF],
            CancellationToken.None);

        Assert.Equal(2, outcomes.Count);
        orchestrator.Verify(o => o.RecognizeAsync(It.IsAny<RecognitionPipelineRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void ManualReviewService_FlagsLowConfidence()
    {
        var service = new ManualReviewService();
        var result = service.Evaluate(new ManualReviewRequest
        {
            SessionId = Guid.NewGuid(),
            FaceIndex = 1,
            Reason = "Low confidence match",
        });

        Assert.True(result.RequiresReview);
    }

    private AttendanceSessionContext CreateContext(params FaceRecognitionOutcome[] outcomes) =>
        CreateContext((IReadOnlyList<FaceRecognitionOutcome>)outcomes);

    private AttendanceSessionContext CreateContext(IReadOnlyList<FaceRecognitionOutcome> outcomes) =>
        new()
        {
            Session = new AttendanceSessionMetadata
            {
                SessionId = Guid.NewGuid(),
                TenantId = 1,
                CourseId = 1,
                GroupId = 1,
                SemesterId = 1,
                SubjectId = 1,
                AttendanceDateUtc = DateTime.UtcNow,
            },
            CorrelationId = Guid.NewGuid(),
            Policy = _policy,
            RecognitionOutcomes = outcomes,
        };

    private static FaceRecognitionOutcome CreateOutcome(
        int faceIndex,
        RecognitionDecisionType decisionType,
        RecognitionStatus status,
        int studentId,
        decimal confidence) =>
        new()
        {
            FaceIndex = faceIndex,
            DetectedFace = new DetectedFaceDto { FaceIndex = faceIndex, Embedding = [1f, 0f] },
            RecognitionResult = new RecognitionResult
            {
                Success = true,
                RecognitionRequestId = Guid.NewGuid(),
                AttendanceSessionId = Guid.NewGuid(),
                TenantId = 1,
                FaceIndex = faceIndex,
                Status = RecognitionPipelineState.Recognized,
                PersistedRecognitionId = Guid.NewGuid(),
                Decision = new RecognitionDecision
                {
                    DecisionType = decisionType,
                    Status = status,
                    StudentId = studentId,
                    Confidence = confidence,
                    Distance = 0.2m,
                },
            },
        };
}
