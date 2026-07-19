using System.Collections.Concurrent;
using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.PhotoAcquisition;

public sealed class PhotoDownloadCoordinator : IPhotoDownloadCoordinator
{
    private readonly IPhotoDownloadRepository _repository;
    private readonly IStudentPhotoSource _photoSource;
    private readonly IStudentPhotoDownloader _downloader;
    private readonly IPhotoValidationService _validationService;
    private readonly IPhotoQualityAssessmentService _qualityService;
    private readonly IPhotoRetryPolicy _retryPolicy;
    private readonly IPhotoManifestGenerator _manifestGenerator;
    private readonly IPhotoDownloadQueue _queue;
    private readonly IEnrollmentJobQueue _enrollmentJobQueue;
    private readonly PhotoAcquisitionOptions _options;
    private readonly ILogger<PhotoDownloadCoordinator> _logger;

    public PhotoDownloadCoordinator(
        IPhotoDownloadRepository repository,
        IStudentPhotoSource photoSource,
        IStudentPhotoDownloader downloader,
        IPhotoValidationService validationService,
        IPhotoQualityAssessmentService qualityService,
        IPhotoRetryPolicy retryPolicy,
        IPhotoManifestGenerator manifestGenerator,
        IPhotoDownloadQueue queue,
        IEnrollmentJobQueue enrollmentJobQueue,
        IOptions<PhotoAcquisitionOptions> options,
        ILogger<PhotoDownloadCoordinator> logger)
    {
        _repository = repository;
        _photoSource = photoSource;
        _downloader = downloader;
        _validationService = validationService;
        _qualityService = qualityService;
        _retryPolicy = retryPolicy;
        _manifestGenerator = manifestGenerator;
        _queue = queue;
        _enrollmentJobQueue = enrollmentJobQueue;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PhotoAcquisitionBatchResult> RunBatchAsync(
        PhotoAcquisitionBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await _repository.CreateBatchAsync(request, cancellationToken);
        batch.Status = PhotoAcquisitionBatchStatus.Running;
        await _repository.UpdateBatchAsync(batch, cancellationToken);

        var items = await _repository.GetBatchItemsAsync(batch.Id, cancellationToken);
        var hashSet = new ConcurrentDictionary<string, byte>();
        var succeeded = 0;
        var failed = 0;
        var retryQueued = 0;
        var ready = 0;

        using var semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentDownloads));
        var tasks = items.Select(item => ProcessItemWithSemaphoreAsync(
            item,
            batch,
            hashSet,
            semaphore,
            cancellationToken)).ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            switch (result)
            {
                case PhotoAcquisitionItemStatus.ReadyForEnrollment:
                    ready++;
                    break;
                case PhotoAcquisitionItemStatus.RetryQueued:
                    retryQueued++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        succeeded = ready;
        var refreshedItems = await _repository.GetBatchItemsAsync(batch.Id, cancellationToken);
        var manifest = _manifestGenerator.Generate(batch, refreshedItems);
        batch.Status = PhotoAcquisitionBatchStatus.Completed;
        batch.SucceededCount = succeeded;
        batch.FailedCount = failed;
        batch.RetryQueuedCount = retryQueued;
        batch.ReadyForEnrollmentCount = ready;
        batch.ManifestJson = JsonSerializer.Serialize(manifest);
        batch.CompletedUtc = DateTime.UtcNow;
        await _repository.UpdateBatchAsync(batch, cancellationToken);

        if (ready > 0)
        {
            _enrollmentJobQueue.SignalWork();
        }

        _logger.LogInformation(
            "Photo acquisition batch {BatchId} completed succeeded={Succeeded} failed={Failed} retry={Retry}",
            batch.Id,
            succeeded,
            failed,
            retryQueued);

        return new PhotoAcquisitionBatchResult
        {
            BatchId = batch.Id,
            Manifest = manifest,
            SucceededCount = succeeded,
            FailedCount = failed,
            RetryQueuedCount = retryQueued,
            ReadyForEnrollmentCount = ready,
        };
    }

    public async Task ProcessPendingRetriesAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Batch not found: {batchId}");

        var retryItems = await _repository.GetRetryReadyItemsAsync(batchId, DateTime.UtcNow, cancellationToken);
        var hashSet = new ConcurrentDictionary<string, byte>(
            (await _repository.GetBatchItemsAsync(batchId, cancellationToken))
            .Where(i => !string.IsNullOrWhiteSpace(i.ContentHash))
            .Select(i => i.ContentHash!)
            .Distinct()
            .ToDictionary(h => h, _ => (byte)0));

        using var semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentDownloads));
        var tasks = retryItems.Select(item => ProcessItemWithSemaphoreAsync(item, batch, hashSet, semaphore, cancellationToken));
        await Task.WhenAll(tasks);

        var refreshedItems = await _repository.GetBatchItemsAsync(batchId, cancellationToken);
        var manifest = _manifestGenerator.Generate(batch, refreshedItems);
        batch.ManifestJson = JsonSerializer.Serialize(manifest);
        batch.ReadyForEnrollmentCount = refreshedItems.Count(i => i.Status == PhotoAcquisitionItemStatus.ReadyForEnrollment);
        batch.RetryQueuedCount = refreshedItems.Count(i => i.Status == PhotoAcquisitionItemStatus.RetryQueued);
        batch.FailedCount = refreshedItems.Count(i => i.Status is PhotoAcquisitionItemStatus.Failed or PhotoAcquisitionItemStatus.Invalid);
        await _repository.UpdateBatchAsync(batch, cancellationToken);
    }

    private async Task<PhotoAcquisitionItemStatus> ProcessItemWithSemaphoreAsync(
        StudentPhotoAcquisitionItem item,
        StudentPhotoAcquisitionBatch batch,
        ConcurrentDictionary<string, byte> hashSet,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ProcessItemAsync(item, batch, hashSet, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<PhotoAcquisitionItemStatus> ProcessItemAsync(
        StudentPhotoAcquisitionItem item,
        StudentPhotoAcquisitionBatch batch,
        ConcurrentDictionary<string, byte> hashSet,
        CancellationToken cancellationToken)
    {
        item.Status = PhotoAcquisitionItemStatus.Downloading;
        await _repository.UpdateItemAsync(item, cancellationToken);
        await _queue.EnqueueDownloadAsync(item.Id, cancellationToken);

        var student = new PhotoAcquisitionStudentMaster
        {
            TenantId = item.TenantId,
            StudentId = item.StudentId,
            StudentNumber = item.StudentNumber,
            CollegeCode = item.CollegeCode,
            AcademicYear = batch.AcademicYear,
            PreferredProviderName = batch.ProviderName,
        };

        var source = await _photoSource.ResolveAsync(student, cancellationToken);
        var download = await _downloader.DownloadAsync(source, cancellationToken);

        if (!download.Success || download.PhotoBytes is not { Length: > 0 })
        {
            if (_retryPolicy.ShouldRetry(item.RetryCount, download))
            {
                item.Status = PhotoAcquisitionItemStatus.RetryQueued;
                item.RetryCount++;
                item.NextAttemptUtc = DateTime.UtcNow.Add(_retryPolicy.GetDelay(item.RetryCount));
                item.FailureReason = download.FailureReason;
                await _repository.UpdateItemAsync(item, cancellationToken);
                await _queue.EnqueueRetryAsync(item.Id, cancellationToken);
                return item.Status;
            }

            item.Status = PhotoAcquisitionItemStatus.Failed;
            item.FailureReason = download.FailureReason;
            item.CompletedUtc = DateTime.UtcNow;
            await _repository.UpdateItemAsync(item, cancellationToken);
            return item.Status;
        }

        item.Status = PhotoAcquisitionItemStatus.Validating;
        item.SourceReference = download.SourceReference;
        item.ContentType = download.ContentType;
        item.PhotoBytes = download.PhotoBytes;
        item.PhotoByteSize = download.PhotoBytes.Length;
        await _repository.UpdateItemAsync(item, cancellationToken);

        var existingHashes = hashSet.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validation = _validationService.Validate(download.PhotoBytes, download.ContentType, existingHashes);
        item.ValidationReportJson = JsonSerializer.Serialize(validation);

        if (!validation.IsValid)
        {
            item.Status = validation.IsDuplicate
                ? PhotoAcquisitionItemStatus.Duplicate
                : PhotoAcquisitionItemStatus.Invalid;
            item.FailureReason = string.Join("; ", validation.Errors);
            item.CompletedUtc = DateTime.UtcNow;
            await _repository.UpdateItemAsync(item, cancellationToken);
            return item.Status;
        }

        var hash = PhotoValidationService.ComputeHash(download.PhotoBytes);
        hashSet.TryAdd(hash, 0);
        item.ContentHash = hash;

        item.Status = PhotoAcquisitionItemStatus.QualityAssessment;
        await _repository.UpdateItemAsync(item, cancellationToken);

        var quality = _qualityService.Assess(download.PhotoBytes);
        item.QualityReportJson = JsonSerializer.Serialize(quality);
        item.Status = PhotoAcquisitionItemStatus.ReadyForEnrollment;
        item.CompletedUtc = DateTime.UtcNow;
        await _repository.UpdateItemAsync(item, cancellationToken);
        await _queue.EnqueueEnrollmentAsync(item.Id, cancellationToken);

        return item.Status;
    }
}

public sealed class PhotoAcquisitionReportService : IPhotoAcquisitionReportService
{
    private readonly IPhotoDownloadRepository _repository;
    private readonly IPhotoManifestGenerator _manifestGenerator;
    private readonly IPhotoDownloadQueue _queue;

    public PhotoAcquisitionReportService(
        IPhotoDownloadRepository repository,
        IPhotoManifestGenerator manifestGenerator,
        IPhotoDownloadQueue queue)
    {
        _repository = repository;
        _manifestGenerator = manifestGenerator;
        _queue = queue;
    }

    public async Task<PhotoAcquisitionReport> GenerateReportAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Batch not found: {batchId}");

        var items = await _repository.GetBatchItemsAsync(batchId, cancellationToken);
        var manifest = _manifestGenerator.Generate(batch, items);
        var qualityReports = items
            .Where(i => !string.IsNullOrWhiteSpace(i.QualityReportJson))
            .Select(i => JsonSerializer.Deserialize<PhotoQualityReport>(i.QualityReportJson!)!)
            .ToList();

        var failed = items
            .Where(i => i.Status is PhotoAcquisitionItemStatus.Failed or PhotoAcquisitionItemStatus.Invalid or PhotoAcquisitionItemStatus.Duplicate)
            .Select(i => $"{i.StudentNumber}: {i.FailureReason}")
            .ToList();

        return new PhotoAcquisitionReport
        {
            BatchId = batchId,
            Manifest = manifest,
            QualityReports = qualityReports,
            FailedDownloads = failed,
            EnrollmentQueueDepth = _queue.EnrollmentQueueDepth,
        };
    }
}
