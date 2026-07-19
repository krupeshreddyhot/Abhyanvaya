using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment;

public sealed class EnrollmentBatchService : IEnrollmentBatchService
{
    private readonly IEnrollmentReferenceValidator _referenceValidator;
    private readonly IStudentEnrollmentBatchRepository _batchRepository;
    private readonly IStudentEnrollmentItemRepository _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPipelineVersionProvider _pipelineVersionProvider;
    private readonly IPipelineManifestProvider _pipelineManifestProvider;
    private readonly IEnrollmentConfigurationSnapshotCapture _snapshotCapture;
    private readonly IEnrollmentEligibleStudentQuery _eligibleStudentQuery;
    private readonly IEnrollmentJobQueue _jobQueue;
    private readonly IStudentPhotoProviderFactory _photoProviderFactory;
    private readonly ExamBranchPhotoProviderOptions _examBranchOptions;
    private readonly EnrollmentPipelineOptions _pipelineOptions;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentBatchService> _logger;

    public EnrollmentBatchService(
        IEnrollmentReferenceValidator referenceValidator,
        IStudentEnrollmentBatchRepository batchRepository,
        IStudentEnrollmentItemRepository itemRepository,
        IUnitOfWork unitOfWork,
        IPipelineVersionProvider pipelineVersionProvider,
        IPipelineManifestProvider pipelineManifestProvider,
        IEnrollmentConfigurationSnapshotCapture snapshotCapture,
        IEnrollmentEligibleStudentQuery eligibleStudentQuery,
        IEnrollmentJobQueue jobQueue,
        IStudentPhotoProviderFactory photoProviderFactory,
        IOptions<ExamBranchPhotoProviderOptions> examBranchOptions,
        IOptions<EnrollmentPipelineOptions> pipelineOptions,
        TimeProvider clock,
        ILogger<EnrollmentBatchService> logger)
    {
        _referenceValidator = referenceValidator;
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _unitOfWork = unitOfWork;
        _pipelineVersionProvider = pipelineVersionProvider;
        _pipelineManifestProvider = pipelineManifestProvider;
        _snapshotCapture = snapshotCapture;
        _eligibleStudentQuery = eligibleStudentQuery;
        _jobQueue = jobQueue;
        _photoProviderFactory = photoProviderFactory;
        _examBranchOptions = examBranchOptions.Value;
        _pipelineOptions = pipelineOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentBatchCreateResult> CreateBatchAsync(
        EnrollmentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        var referenceValidation = await _referenceValidator.ValidateAsync(request, cancellationToken);
        if (!referenceValidation.Succeeded)
        {
            return EnrollmentBatchCreateResult.Failure(
                referenceValidation.FailureCode!.Value,
                referenceValidation.FailureMessage!,
                correlationId);
        }

        if (await _batchRepository.HasActiveBatchAsync(
                request.TenantId,
                request.CollegeId,
                request.AcademicYear,
                cancellationToken))
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.ActiveBatchAlreadyRunning,
                "An enrollment batch is already running for this college and academic year.",
                correlationId);
        }

        var pipelineVersion = _pipelineVersionProvider.GetActiveVersionForNewBatch(request);
        if (!_pipelineVersionProvider.VersionExists(pipelineVersion))
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.PipelineVersionNotFound,
                $"Pipeline version {pipelineVersion.Value} is not registered.",
                correlationId);
        }

        var pipelineName = _pipelineOptions.PipelineName;
        if (!_pipelineManifestProvider.ManifestExists(pipelineName, pipelineVersion.Value))
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.PipelineManifestNotFound,
                $"Pipeline manifest for {pipelineName} v{pipelineVersion.Value} was not found.",
                correlationId);
        }

        var manifest = _pipelineManifestProvider.GetManifest(pipelineName, pipelineVersion.Value);

        string photoProviderName;
        try
        {
            photoProviderName = ResolvePhotoProviderName(request.PhotoProvider);
        }
        catch (InvalidOperationException ex)
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.InvalidRequest,
                ex.Message,
                correlationId);
        }

        var students = await _eligibleStudentQuery.GetEligibleStudentsAsync(
            new EnrollmentStudentDiscoveryCriteria
            {
                TenantId = request.TenantId,
                CourseId = request.CourseId,
                GroupId = request.GroupId,
                Batch = request.Batch,
                SubjectId = request.SubjectId,
                StudentFilter = request.StudentFilter,
                ForceReEnrollment = request.ForceReEnrollment,
            },
            cancellationToken);

        _logger.LogInformation(
            "Enrollment Students Loaded. CorrelationId={CorrelationId} TenantId={TenantId} CollegeId={CollegeId} Count={Count}",
            correlationId,
            request.TenantId,
            request.CollegeId,
            students.Count);

        if (students.Count == 0)
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.NoEligibleStudents,
                "No eligible students were found for the requested scope.",
                correlationId);
        }

        var snapshotResult = await _snapshotCapture.CaptureAsync(
            request,
            pipelineVersion.Value,
            manifest,
            photoProviderName,
            cancellationToken);

        if (!snapshotResult.Succeeded || snapshotResult.Snapshot == null || snapshotResult.SerializedJson == null)
        {
            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.ConfigurationSnapshotFailed,
                snapshotResult.FailureMessage ?? "Configuration snapshot capture failed.",
                correlationId);
        }

        _logger.LogInformation(
            "Enrollment Snapshot Captured. CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
            correlationId,
            pipelineVersion.Value);

        var batchId = Guid.NewGuid();
        var createdUtc = _clock.GetUtcNow().UtcDateTime;
        var collegeCode = referenceValidation.CollegeCode!;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var batch = BuildBatch(
                    batchId,
                    request,
                    pipelineVersion.Value,
                    snapshotResult.SerializedJson,
                    photoProviderName,
                    correlationId,
                    students.Count,
                    createdUtc);

                await _batchRepository.CreateBatchAsync(batch, ct);

                var items = BuildItems(
                    batchId,
                    request,
                    students,
                    collegeCode,
                    request.AcademicYear,
                    createdUtc);

                await _itemRepository.CreateItemsAsync(items, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Enrollment Batch Persistence Failed. CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
                correlationId,
                pipelineVersion.Value);

            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.PersistenceFailed,
                "Failed to persist enrollment batch and items.",
                correlationId);
        }

        try
        {
            _jobQueue.SignalWork();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Enrollment Batch Queue Signal Failed. CorrelationId={CorrelationId} BatchId={BatchId}",
                correlationId,
                batchId);

            return EnrollmentBatchCreateResult.Failure(
                EnrollmentBatchFailureCode.QueueFailed,
                "Batch was created but the queue wake signal failed.",
                correlationId);
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Enrollment Batch Created. CorrelationId={CorrelationId} BatchId={BatchId} StudentsLoaded={StudentsLoaded} ItemsCreated={ItemsCreated} PipelineVersion={PipelineVersion} DurationMs={DurationMs}",
            correlationId,
            batchId,
            students.Count,
            students.Count,
            pipelineVersion.Value,
            stopwatch.ElapsedMilliseconds);

        _logger.LogInformation(
            "Enrollment Batch Queued. CorrelationId={CorrelationId} BatchId={BatchId} PipelineVersion={PipelineVersion}",
            correlationId,
            batchId,
            pipelineVersion.Value);

        return EnrollmentBatchCreateResult.Success(
            batchId,
            students.Count,
            BatchStatus.Created,
            correlationId,
            pipelineVersion.Value);
    }

    public Task<EnrollmentCommandResult> CancelBatchAsync(
        Guid batchId,
        int tenantId,
        int requestedByUserId,
        CancellationToken cancellationToken = default) =>
        CancelBatchCoreAsync(batchId, tenantId, requestedByUserId, cancellationToken);

    public Task<EnrollmentCommandResult> ResumeBatchAsync(
        Guid batchId,
        int tenantId,
        int requestedByUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EnrollmentCommandResult.NoOp(
            BatchStatus.Created,
            "Resume batch is not implemented in Phase 2.1.2."));

    private async Task<EnrollmentCommandResult> CancelBatchCoreAsync(
        Guid batchId,
        int tenantId,
        int requestedByUserId,
        CancellationToken cancellationToken)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);
        if (batch is null)
        {
            return EnrollmentCommandResult.NoOp(BatchStatus.Created, "Batch not found.");
        }

        if (batch.Status is BatchStatus.Completed or BatchStatus.Cancelled)
        {
            return EnrollmentCommandResult.NoOp(batch.Status, $"Batch is already {batch.Status}.");
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _itemRepository.CancelNonTerminalItemsAsync(batchId, utcNow, ct);

            batch.CancellationRequestedUtc = utcNow;
            batch.Status = BatchStatus.Cancelled;
            batch.CompletedUtc = utcNow;
            batch.PendingCount = 0;
            batch.DownloadingCount = 0;
            batch.ValidatingCount = 0;
            batch.EmbeddingCount = 0;
            batch.RetryRequiredCount = 0;
            batch.CompletedCount = await _itemRepository.CountByStatusAsync(
                batchId,
                EnrollmentStatus.Completed,
                ct);
            batch.FailedCount = await _itemRepository.CountByStatusAsync(
                batchId,
                EnrollmentStatus.Failed,
                ct);
            batch.CancelledCount = await _itemRepository.CountByStatusAsync(
                batchId,
                EnrollmentStatus.Cancelled,
                ct);

            await _batchRepository.UpdateBatchAsync(batch, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Enrollment batch cancelled. BatchId={BatchId} TenantId={TenantId} RequestedByUserId={RequestedByUserId}",
            batchId,
            tenantId,
            requestedByUserId);

        try
        {
            _jobQueue.SignalWork();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to signal enrollment workers after batch cancellation.");
        }

        return EnrollmentCommandResult.Ok(BatchStatus.Cancelled);
    }

    private string ResolvePhotoProviderName(string? requestedProvider)
    {
        if (!string.IsNullOrWhiteSpace(requestedProvider))
        {
            var registered = _photoProviderFactory.GetRegisteredProviders();
            if (!registered.Any(p => string.Equals(p, requestedProvider, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Photo provider '{requestedProvider}' is not registered.");
            }

            return registered.First(p => string.Equals(p, requestedProvider, StringComparison.OrdinalIgnoreCase));
        }

        return _photoProviderFactory.GetDefaultProvider().ProviderName;
    }

    private static StudentEnrollmentBatch BuildBatch(
        Guid batchId,
        EnrollmentBatchRequest request,
        int pipelineVersion,
        string snapshotJson,
        string photoProviderName,
        Guid correlationId,
        int totalStudents,
        DateTime createdUtc) =>
        new()
        {
            Id = batchId,
            TenantId = request.TenantId,
            UniversityId = request.UniversityId,
            CollegeId = request.CollegeId,
            AcademicYear = request.AcademicYear,
            Status = BatchStatus.Created,
            TotalStudents = totalStudents,
            PendingCount = totalStudents,
            DownloadingCount = 0,
            ValidatingCount = 0,
            EmbeddingCount = 0,
            CompletedCount = 0,
            FailedCount = 0,
            RetryRequiredCount = 0,
            CancelledCount = 0,
            CreatedUtc = createdUtc,
            CreatedBy = request.RequestedByUserId,
            PipelineVersion = pipelineVersion,
            ConfigurationSnapshotJson = snapshotJson,
            CorrelationId = correlationId,
            PhotoProviderName = photoProviderName,
            Priority = request.Priority,
        };

    private IReadOnlyList<StudentEnrollmentItem> BuildItems(
        Guid batchId,
        EnrollmentBatchRequest request,
        IReadOnlyList<EnrollmentEligibleStudent> students,
        string collegeCode,
        int academicYear,
        DateTime createdUtc)
    {
        var baseUrlTemplate = _examBranchOptions.BaseUrlTemplate;
        var items = new List<StudentEnrollmentItem>(students.Count);

        foreach (var student in students)
        {
            var sourceUrl = EnrollmentSourceUrlBuilder.Build(
                baseUrlTemplate,
                collegeCode,
                academicYear,
                student.StudentNumber);

            items.Add(new StudentEnrollmentItem
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                BatchId = batchId,
                StudentId = student.StudentId,
                Status = EnrollmentStatus.Pending,
                SourceUrl = sourceUrl,
                CreatedUtc = createdUtc,
            });
        }

        return items;
    }
}
