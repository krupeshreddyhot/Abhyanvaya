using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class StorageEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IEnrollmentStorageService _storageService;
    private readonly IEnrollmentStudentPhotoPublisher _studentPhotoPublisher;

    public StorageEnrollmentPipelineStage(
        IEnrollmentStorageService storageService,
        IEnrollmentStudentPhotoPublisher studentPhotoPublisher)
    {
        _storageService = storageService;
        _studentPhotoPublisher = studentPhotoPublisher;
    }

    public EnrollmentPipelineStage? ManifestStage => EnrollmentPipelineStage.Storage;
    public string Name => "Storage";
    public int Order => 200;
    public string Description => "Persist validated enrollment artifacts and produce a storage manifest.";
    public string Version => "1.0";
    public bool SupportsRetry => true;
    public bool SupportsResume => false;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;

        if (context.ValidationArtifact is null)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                "Storage requires a validation artifact.",
                isRetryable: false);
        }

        var storageResult = await _storageService.StoreAsync(
            new EnrollmentStorageRequest
            {
                TenantId = itemContext.TenantId,
                CollegeId = itemContext.CollegeId,
                AcademicYear = itemContext.AcademicYear,
                StudentId = itemContext.StudentId,
                BatchId = itemContext.BatchId,
                ItemId = itemContext.ItemId,
                PipelineVersion = itemContext.PipelineVersion,
                Artifact = context.ValidationArtifact,
                ExecutionTraceId = itemContext.ExecutionTraceId,
            },
            cancellationToken);

        if (!storageResult.Success || storageResult.Manifest is null)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed, StorageResult = storageResult },
                stopwatch.Elapsed,
                "storage.failure",
                storageResult.FailureReason ?? "Storage failed.",
                Domain.Enums.FailureCategory.StorageUploadFailed,
                isRetryable: true);
        }

        if (context.PhotoBytes is not { Length: > 0 })
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed, StorageResult = storageResult },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                "Student photo publish requires downloaded photo bytes.",
                isRetryable: false);
        }

        var publishResult = await _studentPhotoPublisher.PublishAsync(
            new EnrollmentStudentPhotoPublishRequest
            {
                TenantId = itemContext.TenantId,
                StudentId = itemContext.StudentId,
                BatchId = itemContext.BatchId,
                ItemId = itemContext.ItemId,
                PhotoBytes = context.PhotoBytes,
                ContentType = context.ContentType,
            },
            cancellationToken);

        if (!publishResult.Success)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed, StorageResult = storageResult },
                stopwatch.Elapsed,
                "student_photo.failure",
                publishResult.FailureReason ?? "Student profile photo publish failed.",
                Domain.Enums.FailureCategory.StorageUploadFailed,
                isRetryable: true);
        }

        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Stored,
                CurrentStage = EnrollmentPipelineStage.Storage,
                StorageResult = storageResult,
                StorageManifest = storageResult.Manifest,
            },
            stopwatch.Elapsed);
    }
}
