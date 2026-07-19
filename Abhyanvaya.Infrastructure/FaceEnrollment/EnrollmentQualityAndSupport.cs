using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.FaceEnrollment;

public sealed class EnrollmentQualityEngine : IEnrollmentQualityEngine
{
    public EnrollmentQualityResult ValidateFaceCount(FaceDetectionResult detection, IEnrollmentPolicy policy)
    {
        var errors = new List<string>();
        if (policy.FaceCountPolicy == FaceCountPolicyMode.ExactlyOne)
        {
            if (detection.FaceCount == 0)
            {
                errors.Add("No face detected.");
            }
            else if (detection.FaceCount > 1)
            {
                errors.Add("Multiple faces detected.");
            }
        }

        if (detection.TopConfidence < (float)policy.MinimumDetectionConfidence)
        {
            errors.Add("Detection confidence below policy threshold.");
        }

        if (detection.ImageWidth < policy.MinimumWidth || detection.ImageHeight < policy.MinimumHeight)
        {
            errors.Add("Image resolution below policy minimum.");
        }

        return new EnrollmentQualityResult
        {
            Passed = errors.Count == 0,
            QualityScore = (decimal)detection.TopConfidence,
            Errors = errors,
        };
    }

    public EnrollmentQualityResult ValidateEmbedding(float[] embedding, IEmbeddingEngine engine, IEnrollmentPolicy policy)
    {
        var errors = new List<string>();
        if (embedding.Length != engine.ExpectedDimension)
        {
            errors.Add($"Embedding dimension {embedding.Length} != expected {engine.ExpectedDimension}.");
        }

        if (policy.EmbeddingPolicy == EmbeddingPolicyMode.RequiredNormalized)
        {
            var magnitude = Math.Sqrt(embedding.Sum(v => v * v));
            if (Math.Abs(magnitude - 1d) > 0.05d)
            {
                errors.Add("Embedding is not normalized.");
            }
        }

        return new EnrollmentQualityResult
        {
            Passed = errors.Count == 0,
            QualityScore = 1m,
            Errors = errors,
        };
    }

    public EnrollmentQualityResult ValidateComposite(decimal qualityScore, IEnrollmentPolicy policy)
    {
        var passed = qualityScore >= policy.MinimumQualityScore;
        return new EnrollmentQualityResult
        {
            Passed = passed,
            QualityScore = qualityScore,
            Errors = passed ? null : new[] { "Quality score below policy minimum." },
        };
    }
}

public sealed class EnrollmentArtifactBuilder : IEnrollmentArtifactBuilder
{
    public EnrollmentArtifact Build(
        EnrollmentContext context,
        string photoReference,
        byte[] alignedFaceBytes,
        float[] embedding,
        string embeddingVersion,
        decimal qualityScore)
    {
        var checksum = Convert.ToHexString(SHA256.HashData(alignedFaceBytes));
        return new EnrollmentArtifact
        {
            StudentId = context.StudentId,
            PhotoReference = photoReference,
            AlignedPhotoReference = $"aligned://{context.EnrollmentId}",
            EmbeddingReference = $"embedding://{context.EnrollmentId}",
            EmbeddingDimension = embedding.Length,
            EmbeddingVersion = embeddingVersion,
            QualityScore = qualityScore,
            ManifestId = context.BatchId,
            EnrollmentVersion = context.EnrollmentPolicyVersion,
            CreatedUtc = DateTime.UtcNow,
            Checksum = checksum,
        };
    }
}

public sealed class EnrollmentProgressTracker : IEnrollmentProgressTracker
{
    private readonly IEnrollmentRepository _repository;
    private readonly ConcurrentDictionary<Guid, Stopwatch> _timers = new();

    public EnrollmentProgressTracker(IEnrollmentRepository repository)
    {
        _repository = repository;
    }

    public async Task UpdateStateAsync(Guid enrollmentId, EnrollmentState state, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(enrollmentId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.State = state;
        job.LastStateChangeUtc = DateTime.UtcNow;
        if (state == EnrollmentState.Processing && job.StartedUtc is null)
        {
            job.StartedUtc = DateTime.UtcNow;
            _timers.TryAdd(enrollmentId, Stopwatch.StartNew());
        }

        if (state is EnrollmentState.Completed or EnrollmentState.Failed or EnrollmentState.Cancelled)
        {
            job.CompletedUtc = DateTime.UtcNow;
        }

        await _repository.UpdateJobAsync(job, cancellationToken);
    }

    public async Task<EnrollmentProgressSnapshot> GetProgressAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(enrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Enrollment job not found: {enrollmentId}");

        var duration = _timers.TryGetValue(enrollmentId, out var timer)
            ? timer.Elapsed
            : job.CompletedUtc.HasValue && job.StartedUtc.HasValue
                ? job.CompletedUtc.Value - job.StartedUtc.Value
                : TimeSpan.Zero;

        return new EnrollmentProgressSnapshot
        {
            EnrollmentId = enrollmentId,
            State = job.State,
            ProgressPercent = CalculateProgressPercent(job.State),
            Duration = duration,
        };
    }

    public decimal CalculateProgressPercent(EnrollmentState state) => state switch
    {
        EnrollmentState.Queued => 0m,
        EnrollmentState.DownloadingCompleted => 10m,
        EnrollmentState.Processing => 15m,
        EnrollmentState.DetectingFace => 25m,
        EnrollmentState.AligningFace => 40m,
        EnrollmentState.GeneratingEmbedding => 55m,
        EnrollmentState.QualityValidation => 70m,
        EnrollmentState.DuplicateChecking => 80m,
        EnrollmentState.ArtifactBuilding => 90m,
        EnrollmentState.Completed => 100m,
        EnrollmentState.Failed => 100m,
        EnrollmentState.Retry => 5m,
        EnrollmentState.Cancelled => 100m,
        _ => 0m,
    };
}

public sealed class EnrollmentFailureHandler : IEnrollmentFailureHandler
{
    private readonly IEnrollmentRepository _repository;
    private readonly IEnrollmentProgressTracker _progressTracker;
    private readonly ILogger<EnrollmentFailureHandler> _logger;

    public EnrollmentFailureHandler(
        IEnrollmentRepository repository,
        IEnrollmentProgressTracker progressTracker,
        ILogger<EnrollmentFailureHandler> logger)
    {
        _repository = repository;
        _progressTracker = progressTracker;
        _logger = logger;
    }

    public async Task HandleAsync(
        FaceEnrollmentJob job,
        EnrollmentState failedAt,
        string reason,
        bool isRetryable,
        CancellationToken cancellationToken = default)
    {
        job.State = isRetryable ? EnrollmentState.Retry : EnrollmentState.Failed;
        job.FailureReason = reason;
        if (isRetryable)
        {
            job.RetryCount++;
        }

        await _repository.UpdateJobAsync(job, cancellationToken);
        await _progressTracker.UpdateStateAsync(job.Id, job.State, cancellationToken);
        _ = new EnrollmentFailed(job.Id, reason, failedAt, DateTime.UtcNow);

        _logger.LogWarning(
            "Enrollment failed enrollmentId={EnrollmentId} state={State} retry={Retry} reason={Reason}",
            job.Id,
            failedAt,
            isRetryable,
            reason);
    }
}

public sealed class EnrollmentDuplicateDetectorService : IEnrollmentDuplicateDetectorService
{
    private readonly IEnrollmentDuplicateDetector _duplicateDetector;
    private readonly ConcurrentDictionary<string, byte> _batchStudentNumbers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _batchContentHashes = new(StringComparer.OrdinalIgnoreCase);

    public EnrollmentDuplicateDetectorService(IEnrollmentDuplicateDetector duplicateDetector)
    {
        _duplicateDetector = duplicateDetector;
    }

    public async Task<EnrollmentDuplicateResult> DetectAsync(
        EnrollmentContext context,
        string studentNumber,
        string contentHash,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        if (!_batchStudentNumbers.TryAdd(studentNumber, 0))
        {
            return new EnrollmentDuplicateResult
            {
                IsDuplicate = true,
                DuplicateType = "StudentNumber",
                Detail = studentNumber,
            };
        }

        if (!_batchContentHashes.TryAdd(contentHash, 0))
        {
            return new EnrollmentDuplicateResult
            {
                IsDuplicate = true,
                DuplicateType = "Artifact",
                Detail = contentHash,
            };
        }

        var embeddingHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join(',', embedding.Select(v => v.ToString("R"))))));

        var metadataResult = await _duplicateDetector.DetectAsync(
            new Application.Enrollment.Persistence.EnrollmentDuplicateDetectionRequest
            {
                ItemId = context.EnrollmentId,
                StudentId = context.StudentId,
                BatchId = context.BatchId,
                EmbeddingModel = context.RecognitionConfigurationVersion,
                EmbeddingModelVersion = context.EnrollmentPolicyVersion,
                PipelineVersion = 1,
            },
            cancellationToken);

        if (metadataResult.IsDuplicate)
        {
            return new EnrollmentDuplicateResult
            {
                IsDuplicate = true,
                DuplicateType = "Enrollment",
                Detail = metadataResult.Reason,
            };
        }

        return new EnrollmentDuplicateResult { IsDuplicate = false };
    }
}
