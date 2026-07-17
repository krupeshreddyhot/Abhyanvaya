using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment;

public sealed class EnrollmentProgressReporter : IEnrollmentProgressReporter
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IStudentEnrollmentBatchRepository _batchRepository;
    private readonly IStudentEnrollmentItemRepository _itemRepository;
    private readonly IEnrollmentProgressSnapshotRepository _snapshotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentProgressReporter> _logger;

    public EnrollmentProgressReporter(
        IStudentEnrollmentBatchRepository batchRepository,
        IStudentEnrollmentItemRepository itemRepository,
        IEnrollmentProgressSnapshotRepository snapshotRepository,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<EnrollmentProgressReporter> logger)
    {
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _snapshotRepository = snapshotRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public Task<EnrollmentTransitionResult> MarkItemStartedAsync(
        EnrollmentProgressOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var toStatus = EnrollmentStatus.Downloading;
        var stamp = EnrollmentStageTimestamp.DownloadStarted;

        if (request.ExpectedStatus == EnrollmentStatus.RetryRequired)
        {
            return TransitionItemAsync(
                BuildTransitionRequest(request, request.ExpectedStatus, toStatus, stamp),
                cancellationToken);
        }

        return TransitionItemAsync(
            BuildTransitionRequest(request, EnrollmentStatus.Pending, toStatus, stamp),
            cancellationToken);
    }

    public Task<EnrollmentTransitionResult> MarkStageCompletedAsync(
        EnrollmentStageProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var (fromStatus, toStatus, stamp) = ResolveStageCompletionTransition(request.Stage, request.ExpectedStatus);

        return TransitionItemAsync(
            BuildTransitionRequest(request, fromStatus, toStatus, stamp),
            cancellationToken);
    }

    public Task<EnrollmentTransitionResult> MarkStageFailedAsync(
        EnrollmentStageProgressRequest request,
        EnrollmentStatus terminalStatus,
        CancellationToken cancellationToken = default)
    {
        if (terminalStatus is not (EnrollmentStatus.Failed or EnrollmentStatus.RetryRequired))
        {
            throw new ArgumentException("Stage failure must target Failed or RetryRequired.", nameof(terminalStatus));
        }

        return TransitionItemAsync(
            BuildTransitionRequest(
                request,
                request.ExpectedStatus,
                terminalStatus,
                stamp: null,
                request.FailureCategory,
                request.LastError),
            cancellationToken);
    }

    public Task<EnrollmentTransitionResult> MarkRetryScheduledAsync(
        EnrollmentRetryProgressRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionItemAsync(
            BuildTransitionRequest(
                request,
                request.FromStatus,
                EnrollmentStatus.RetryRequired,
                stamp: null,
                request.FailureCategory,
                request.LastError),
            cancellationToken);

    public Task<EnrollmentTransitionResult> MarkItemCompletedAsync(
        EnrollmentProgressOperationRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionItemAsync(
            BuildTransitionRequest(
                request,
                request.ExpectedStatus,
                EnrollmentStatus.Completed,
                EnrollmentStageTimestamp.Completed),
            cancellationToken);

    public async Task<EnrollmentTransitionResult> MarkBatchCompletedAsync(
        EnrollmentBatchProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(request.BatchId, request.TenantId, cancellationToken);
        if (batch == null)
        {
            return EnrollmentTransitionResult.NotApplied("Batch not found.");
        }

        if (batch.Status is BatchStatus.Completed or BatchStatus.Cancelled)
        {
            return EnrollmentTransitionResult.NotApplied($"Batch is already {batch.Status}.");
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                batch.Status = BatchStatus.Completed;
                batch.CompletedUtc ??= utcNow;
                await _batchRepository.UpdateBatchAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EnrollmentTransitionResult.Conflict();
        }

        LogBatchCompleted(request, batch);

        return EnrollmentTransitionResult.AppliedOk();
    }

    public async Task<EnrollmentTransitionResult> MarkBatchFailedAsync(
        EnrollmentBatchProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(request.BatchId, request.TenantId, cancellationToken);
        if (batch == null)
        {
            return EnrollmentTransitionResult.NotApplied("Batch not found.");
        }

        if (batch.Status is BatchStatus.Completed or BatchStatus.Cancelled)
        {
            return EnrollmentTransitionResult.NotApplied($"Batch is already {batch.Status}.");
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                batch.Status = BatchStatus.PartiallyFailed;
                batch.CompletedUtc ??= utcNow;
                await _batchRepository.UpdateBatchAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EnrollmentTransitionResult.Conflict();
        }

        _logger.LogWarning(
            "Enrollment Batch Failed. CorrelationId={CorrelationId} BatchId={BatchId} PipelineVersion={PipelineVersion} Status={Status}",
            request.CorrelationId,
            request.BatchId,
            request.PipelineVersion ?? batch.PipelineVersion,
            BatchStatus.PartiallyFailed);

        return EnrollmentTransitionResult.AppliedOk();
    }

    public async Task<EnrollmentTransitionResult> TransitionItemAsync(
        EnrollmentTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null || item.BatchId != request.BatchId || item.TenantId != request.TenantId)
        {
            return EnrollmentTransitionResult.NotApplied("Enrollment item not found.");
        }

        if (item.Status != request.FromStatus)
        {
            return EnrollmentTransitionResult.NotApplied(
                $"Item is {item.Status}, expected {request.FromStatus}.");
        }

        EnrollmentStatusTransitionRules.EnsureAllowed(item.Status, request.ToStatus);

        var batch = await _batchRepository.GetBatchAsync(request.BatchId, request.TenantId, cancellationToken);
        if (batch == null)
        {
            return EnrollmentTransitionResult.NotApplied("Batch not found.");
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;
        var originalItemVersion = item.RowVersion.ToArray();
        var originalBatchVersion = batch.RowVersion.ToArray();

        ApplyItemMutation(item, request, utcNow);
        EnrollmentBatchCounterRules.ApplyTransition(batch, request.FromStatus, request.ToStatus);
        EnsureBatchStarted(batch, utcNow);

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _itemRepository.UpdateItemAsync(item, ct);
                await _batchRepository.UpdateBatchAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            item.RowVersion = originalItemVersion;
            batch.RowVersion = originalBatchVersion;
            return EnrollmentTransitionResult.Conflict();
        }

        LogTransition(request, item, batch);

        if (IsTerminalItemStatus(request.ToStatus))
        {
            await FinalizeBatchIfCompleteAsync(request.BatchId, request.TenantId, cancellationToken);
        }

        return EnrollmentTransitionResult.AppliedOk();
    }

    public async Task FinalizeBatchIfCompleteAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);
        if (batch == null || batch.Status is BatchStatus.Completed or BatchStatus.Cancelled)
        {
            return;
        }

        var statistics = EnrollmentProgressCalculator.MapStatistics(MapCounters(batch));
        if (statistics.InFlightCount > 0)
        {
            return;
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;
        batch.CompletedUtc ??= utcNow;
        batch.Status = statistics.Completed == statistics.Total
            ? BatchStatus.Completed
            : statistics.Cancelled == statistics.Total
                ? BatchStatus.Cancelled
                : BatchStatus.PartiallyFailed;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _batchRepository.UpdateBatchAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);

            if (batch.Status == BatchStatus.Completed)
            {
                _logger.LogInformation(
                    "Enrollment Batch Completed. CorrelationId={CorrelationId} BatchId={BatchId} PipelineVersion={PipelineVersion} Total={Total} Completed={Completed} Failed={Failed}",
                    batch.CorrelationId,
                    batch.Id,
                    batch.PipelineVersion,
                    batch.TotalStudents,
                    batch.CompletedCount,
                    batch.FailedCount);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Enrollment batch finalize concurrency conflict. BatchId={BatchId}",
                batchId);
        }
    }

    public async Task<EnrollmentProgressDetail?> UpdateProgressAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);
        if (batch == null)
        {
            return null;
        }

        var detail = await BuildProgressDetailAsync(batch, cancellationToken);

        _logger.LogInformation(
            "Enrollment Progress Updated. CorrelationId={CorrelationId} BatchId={BatchId} PipelineVersion={PipelineVersion} CompletionPercentage={CompletionPercentage} EtaIsKnown={EtaIsKnown}",
            batch.CorrelationId,
            batch.Id,
            batch.PipelineVersion,
            detail.Metrics.CompletionPercentage,
            detail.Metrics.EtaIsKnown);

        if (detail.Metrics.EtaIsKnown)
        {
            _logger.LogInformation(
                "Enrollment ETA Updated. CorrelationId={CorrelationId} BatchId={BatchId} EstimatedCompletionUtc={EstimatedCompletionUtc}",
                batch.CorrelationId,
                batch.Id,
                detail.Metrics.EstimatedCompletionUtc);
        }

        return detail;
    }

    public EnrollmentProgressMetrics CalculateProgress(
        StudentEnrollmentBatchCounters counters,
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions,
        int uploadingItems,
        DateTime utcNow) =>
        EnrollmentProgressCalculator.BuildMetrics(counters, recentCompletions, uploadingItems, utcNow);

    public EnrollmentEtaResult CalculateETA(
        int remainingItems,
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions,
        DateTime utcNow)
    {
        var average = EnrollmentProgressCalculator.CalculateAverageItemDurationSeconds(recentCompletions);
        return EnrollmentProgressCalculator.CalculateEta(
            utcNow,
            remainingItems,
            average,
            recentCompletions.Count);
    }

    public async Task<EnrollmentProgressSnapshotRecord?> PersistProgressSnapshotAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);
        if (batch == null)
        {
            return null;
        }

        var detail = await BuildProgressDetailAsync(batch, cancellationToken);
        var capturedUtc = _clock.GetUtcNow().UtcDateTime;
        var snapshotId = Guid.NewGuid();

        var record = new EnrollmentProgressSnapshotRecord
        {
            SnapshotId = snapshotId,
            BatchId = batchId,
            TenantId = tenantId,
            CapturedUtc = capturedUtc,
            Completed = detail.Metrics.CompletedItems,
            Pending = detail.Metrics.PendingItems,
            Failed = detail.Metrics.FailedItems,
            Retry = detail.Metrics.RetryItems,
            Downloading = detail.Metrics.DownloadingItems,
            Validating = detail.Metrics.ValidatingItems,
            Uploading = detail.Metrics.UploadingItems,
            Embedding = detail.Metrics.EmbeddingItems,
            Cancelled = detail.Metrics.CancelledItems,
            CompletionPercentage = detail.Metrics.CompletionPercentage,
            EstimatedCompletionUtc = detail.Metrics.EstimatedCompletionUtc,
            EtaIsKnown = detail.Metrics.EtaIsKnown,
            ItemsPerMinute = detail.Metrics.ItemsPerMinute,
        };

        var entity = new StudentEnrollmentProgressSnapshot
        {
            Id = snapshotId,
            TenantId = tenantId,
            BatchId = batchId,
            CapturedUtc = capturedUtc,
            SnapshotJson = JsonSerializer.Serialize(record, SnapshotJsonOptions),
        };

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _snapshotRepository.AppendAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return record;
    }

    public async Task<EnrollmentProgress?> GetProgressAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);
        return batch == null ? null : ComposeProgress(batch);
    }

    private async Task<EnrollmentProgressDetail> BuildProgressDetailAsync(
        StudentEnrollmentBatch batch,
        CancellationToken cancellationToken)
    {
        var recentItems = await _itemRepository.GetRecentlyCompletedAsync(
            batch.Id,
            EnrollmentProgressCalculator.DefaultRecentSampleSize,
            cancellationToken);

        var recentSamples = recentItems
            .Select(item => new RecentEnrollmentCompletionSample
            {
                CreatedUtc = item.CreatedUtc,
                CompletedUtc = item.CompletedUtc!.Value,
                DownloadStartedUtc = item.DownloadStartedUtc,
                DownloadedUtc = item.DownloadedUtc,
            })
            .ToList();

        var uploadingItems = await _itemRepository.CountByStatusAsync(
            batch.Id,
            EnrollmentStatus.Downloaded,
            cancellationToken);

        var utcNow = _clock.GetUtcNow().UtcDateTime;
        var metrics = CalculateProgress(MapCounters(batch), recentSamples, uploadingItems, utcNow);

        return new EnrollmentProgressDetail
        {
            Progress = ComposeProgress(batch),
            Metrics = metrics,
            RowVersion = batch.RowVersion.ToArray(),
        };
    }

    private static EnrollmentProgress ComposeProgress(StudentEnrollmentBatch batch)
    {
        var statistics = EnrollmentProgressCalculator.MapStatistics(MapCounters(batch));
        return new EnrollmentProgress(
            batch.Status,
            statistics,
            batch.StartedUtc,
            batch.CompletedUtc,
            batch.CancellationRequestedUtc);
    }

    private static StudentEnrollmentBatchCounters MapCounters(StudentEnrollmentBatch batch) =>
        new()
        {
            TotalStudents = batch.TotalStudents,
            PendingCount = batch.PendingCount,
            DownloadingCount = batch.DownloadingCount,
            ValidatingCount = batch.ValidatingCount,
            EmbeddingCount = batch.EmbeddingCount,
            CompletedCount = batch.CompletedCount,
            FailedCount = batch.FailedCount,
            RetryRequiredCount = batch.RetryRequiredCount,
            CancelledCount = batch.CancelledCount,
        };

    private static void ApplyItemMutation(
        StudentEnrollmentItem item,
        EnrollmentTransitionRequest request,
        DateTime utcNow)
    {
        item.Status = request.ToStatus;
        item.LastAttemptUtc = utcNow;

        if (request.FailureCategory.HasValue)
        {
            item.FailureCategory = request.FailureCategory;
        }
        else if (request.ToStatus is EnrollmentStatus.Completed)
        {
            item.FailureCategory = null;
        }

        if (!string.IsNullOrWhiteSpace(request.LastError))
        {
            item.LastError = request.LastError;
        }

        if (request.ToStatus == EnrollmentStatus.RetryRequired)
        {
            item.RetryCount++;
        }

        StampTimestamp(item, request.StampTimestamp, utcNow);
    }

    private static void StampTimestamp(
        StudentEnrollmentItem item,
        EnrollmentStageTimestamp? stamp,
        DateTime utcNow)
    {
        switch (stamp)
        {
            case EnrollmentStageTimestamp.DownloadStarted:
                item.DownloadStartedUtc ??= utcNow;
                break;
            case EnrollmentStageTimestamp.Downloaded:
                item.DownloadedUtc = utcNow;
                break;
            case EnrollmentStageTimestamp.ValidationStarted:
                item.ValidationStartedUtc ??= utcNow;
                break;
            case EnrollmentStageTimestamp.Validated:
                item.ValidatedUtc = utcNow;
                break;
            case EnrollmentStageTimestamp.EmbeddingStarted:
                item.EmbeddingStartedUtc ??= utcNow;
                break;
            case EnrollmentStageTimestamp.Completed:
                item.CompletedUtc = utcNow;
                break;
        }
    }

    private static void EnsureBatchStarted(StudentEnrollmentBatch batch, DateTime utcNow)
    {
        if (batch.Status == BatchStatus.Created)
        {
            batch.Status = BatchStatus.Running;
            batch.StartedUtc ??= utcNow;
        }
    }

    private static bool IsTerminalItemStatus(EnrollmentStatus status) =>
        status is EnrollmentStatus.Completed
            or EnrollmentStatus.Failed
            or EnrollmentStatus.Cancelled;

    private static (EnrollmentStatus FromStatus, EnrollmentStatus ToStatus, EnrollmentStageTimestamp? Stamp)
        ResolveStageCompletionTransition(EnrollmentPipelineStage stage, EnrollmentStatus expectedStatus) =>
        stage switch
        {
            EnrollmentPipelineStage.Download => (
                EnrollmentStatus.Downloading,
                EnrollmentStatus.Downloaded,
                EnrollmentStageTimestamp.Downloaded),
            EnrollmentPipelineStage.Storage => (
                EnrollmentStatus.Downloaded,
                EnrollmentStatus.Validating,
                EnrollmentStageTimestamp.ValidationStarted),
            EnrollmentPipelineStage.Validation => (
                EnrollmentStatus.Validating,
                EnrollmentStatus.Embedding,
                EnrollmentStageTimestamp.Validated),
            EnrollmentPipelineStage.Embedding => (
                EnrollmentStatus.Embedding,
                EnrollmentStatus.Completed,
                EnrollmentStageTimestamp.Completed),
            EnrollmentPipelineStage.Finalize => (
                expectedStatus,
                expectedStatus,
                null),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown pipeline stage."),
        };

    private static EnrollmentTransitionRequest BuildTransitionRequest(
        EnrollmentProgressOperationRequest request,
        EnrollmentStatus fromStatus,
        EnrollmentStatus toStatus,
        EnrollmentStageTimestamp? stamp,
        FailureCategory? failureCategory = null,
        string? lastError = null) =>
        new()
        {
            ItemId = request.ItemId,
            BatchId = request.BatchId,
            TenantId = request.TenantId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            StampTimestamp = stamp,
            FailureCategory = failureCategory ?? request.FailureCategory,
            LastError = lastError ?? request.LastError,
            CorrelationId = request.CorrelationId,
            ExecutionTraceId = request.ExecutionTraceId,
            PipelineVersion = request.PipelineVersion,
        };

    private static EnrollmentTransitionRequest BuildTransitionRequest(
        EnrollmentStageProgressRequest request,
        EnrollmentStatus fromStatus,
        EnrollmentStatus toStatus,
        EnrollmentStageTimestamp? stamp,
        FailureCategory? failureCategory = null,
        string? lastError = null) =>
        new()
        {
            ItemId = request.ItemId,
            BatchId = request.BatchId,
            TenantId = request.TenantId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            StampTimestamp = stamp,
            FailureCategory = failureCategory ?? request.FailureCategory,
            LastError = lastError ?? request.LastError,
            CorrelationId = request.CorrelationId,
            ExecutionTraceId = request.ExecutionTraceId,
            PipelineVersion = request.PipelineVersion,
        };

    private static EnrollmentTransitionRequest BuildTransitionRequest(
        EnrollmentRetryProgressRequest request,
        EnrollmentStatus fromStatus,
        EnrollmentStatus toStatus,
        EnrollmentStageTimestamp? stamp,
        FailureCategory? failureCategory = null,
        string? lastError = null) =>
        new()
        {
            ItemId = request.ItemId,
            BatchId = request.BatchId,
            TenantId = request.TenantId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            StampTimestamp = stamp,
            FailureCategory = failureCategory ?? request.FailureCategory,
            LastError = lastError ?? request.LastError,
            CorrelationId = request.CorrelationId,
            ExecutionTraceId = request.ExecutionTraceId,
            PipelineVersion = request.PipelineVersion,
        };

    private void LogTransition(
        EnrollmentTransitionRequest request,
        StudentEnrollmentItem item,
        StudentEnrollmentBatch batch)
    {
        if (request.ToStatus == EnrollmentStatus.Completed)
        {
            _logger.LogInformation(
                "Enrollment Stage Completed. CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId} BatchId={BatchId} ItemId={ItemId} PipelineVersion={PipelineVersion} Status={Status}",
                request.CorrelationId ?? batch.CorrelationId,
                request.ExecutionTraceId,
                batch.Id,
                item.Id,
                request.PipelineVersion ?? batch.PipelineVersion,
                request.ToStatus);
            return;
        }

        if (request.ToStatus is EnrollmentStatus.Failed or EnrollmentStatus.RetryRequired)
        {
            _logger.LogWarning(
                "Enrollment Stage Failed. CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId} BatchId={BatchId} ItemId={ItemId} PipelineVersion={PipelineVersion} Status={Status} FailureCategory={FailureCategory}",
                request.CorrelationId ?? batch.CorrelationId,
                request.ExecutionTraceId,
                batch.Id,
                item.Id,
                request.PipelineVersion ?? batch.PipelineVersion,
                request.ToStatus,
                request.FailureCategory);
        }
    }

    private void LogBatchCompleted(EnrollmentBatchProgressRequest request, StudentEnrollmentBatch batch) =>
        _logger.LogInformation(
            "Enrollment Batch Completed. CorrelationId={CorrelationId} BatchId={BatchId} PipelineVersion={PipelineVersion} Total={Total} Completed={Completed} Failed={Failed}",
            request.CorrelationId ?? batch.CorrelationId,
            batch.Id,
            request.PipelineVersion ?? batch.PipelineVersion,
            batch.TotalStudents,
            batch.CompletedCount,
            batch.FailedCount);
}
