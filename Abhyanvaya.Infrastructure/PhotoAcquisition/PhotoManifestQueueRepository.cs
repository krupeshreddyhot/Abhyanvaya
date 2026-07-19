using System.Text.Json;
using System.Threading.Channels;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.PhotoAcquisition;

public sealed class PhotoManifestGenerator : IPhotoManifestGenerator
{
    public PhotoDownloadManifest Generate(StudentPhotoAcquisitionBatch batch, IEnumerable<StudentPhotoAcquisitionItem> items)
    {
        var materialized = items.ToList();
        var succeeded = materialized
            .Where(i => i.Status == PhotoAcquisitionItemStatus.ReadyForEnrollment)
            .Select(ToEntry)
            .ToList();

        var failed = materialized
            .Where(i => i.Status is PhotoAcquisitionItemStatus.Failed or PhotoAcquisitionItemStatus.Invalid)
            .Select(ToEntry)
            .ToList();

        var retry = materialized
            .Where(i => i.Status == PhotoAcquisitionItemStatus.RetryQueued)
            .Select(ToEntry)
            .ToList();

        return new PhotoDownloadManifest
        {
            BatchId = batch.Id,
            TenantId = batch.TenantId,
            ProviderName = batch.ProviderName,
            Entries = succeeded,
            FailedEntries = failed,
            RetryEntries = retry,
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    private static PhotoDownloadManifestEntry ToEntry(StudentPhotoAcquisitionItem item)
    {
        decimal? qualityScore = null;
        if (!string.IsNullOrWhiteSpace(item.QualityReportJson))
        {
            var report = JsonSerializer.Deserialize<PhotoQualityReport>(item.QualityReportJson);
            qualityScore = report?.OverallScore;
        }

        return new PhotoDownloadManifestEntry
        {
            ItemId = item.Id,
            StudentId = item.StudentId,
            StudentNumber = item.StudentNumber,
            SourceReference = item.SourceReference ?? string.Empty,
            ContentHash = item.ContentHash,
            ContentType = item.ContentType,
            PhotoByteSize = item.PhotoByteSize,
            QualityScore = qualityScore,
        };
    }
}

public sealed class PhotoDownloadQueue : IPhotoDownloadQueue
{
    private readonly Channel<Guid> _downloadQueue = Channel.CreateUnbounded<Guid>();
    private readonly Channel<Guid> _enrollmentQueue = Channel.CreateUnbounded<Guid>();
    private int _downloadDepth;
    private int _enrollmentDepth;

    public int DownloadQueueDepth => Volatile.Read(ref _downloadDepth);
    public int EnrollmentQueueDepth => Volatile.Read(ref _enrollmentDepth);

    public ValueTask EnqueueDownloadAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _downloadDepth);
        return _downloadQueue.Writer.WriteAsync(itemId, cancellationToken);
    }

    public ValueTask EnqueueRetryAsync(Guid itemId, CancellationToken cancellationToken = default)
        => EnqueueDownloadAsync(itemId, cancellationToken);

    public ValueTask EnqueueEnrollmentAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _enrollmentDepth);
        return _enrollmentQueue.Writer.WriteAsync(itemId, cancellationToken);
    }

    public async IAsyncEnumerable<Guid> ReadDownloadQueueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var itemId in _downloadQueue.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _downloadDepth);
            yield return itemId;
        }
    }

    public async IAsyncEnumerable<Guid> ReadEnrollmentQueueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var itemId in _enrollmentQueue.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _enrollmentDepth);
            yield return itemId;
        }
    }
}

public sealed class PhotoDownloadRepository : IPhotoDownloadRepository
{
    private readonly IApplicationDbContext _context;

    public PhotoDownloadRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentPhotoAcquisitionBatch> CreateBatchAsync(
        PhotoAcquisitionBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var batch = new StudentPhotoAcquisitionBatch
        {
            Id = batchId,
            TenantId = request.TenantId,
            ProviderName = request.ProviderName,
            AcademicYear = request.AcademicYear,
            Status = PhotoAcquisitionBatchStatus.Created,
            TotalItems = request.Students.Count,
            CreatedUtc = now,
        };

        await _context.AddAsync(batch);

        foreach (var student in request.Students)
        {
            await _context.AddAsync(new StudentPhotoAcquisitionItem
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                TenantId = student.TenantId,
                StudentId = student.StudentId,
                StudentNumber = student.StudentNumber,
                CollegeCode = student.CollegeCode,
                Status = PhotoAcquisitionItemStatus.Pending,
                CreatedUtc = now,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<StudentPhotoAcquisitionBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentPhotoAcquisitionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetBatchItemsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentPhotoAcquisitionItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateItemAsync(StudentPhotoAcquisitionItem item, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.StudentPhotoAcquisitionItems
            .FirstOrDefaultAsync(i => i.Id == item.Id, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        tracked.Status = item.Status;
        tracked.SourceReference = item.SourceReference;
        tracked.ContentType = item.ContentType;
        tracked.ContentHash = item.ContentHash;
        tracked.PhotoByteSize = item.PhotoByteSize;
        tracked.PhotoBytes = item.PhotoBytes;
        tracked.ValidationReportJson = item.ValidationReportJson;
        tracked.QualityReportJson = item.QualityReportJson;
        tracked.FailureReason = item.FailureReason;
        tracked.RetryCount = item.RetryCount;
        tracked.NextAttemptUtc = item.NextAttemptUtc;
        tracked.CompletedUtc = item.CompletedUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(StudentPhotoAcquisitionBatch batch, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.StudentPhotoAcquisitionBatches
            .FirstOrDefaultAsync(b => b.Id == batch.Id, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        tracked.Status = batch.Status;
        tracked.SucceededCount = batch.SucceededCount;
        tracked.FailedCount = batch.FailedCount;
        tracked.RetryQueuedCount = batch.RetryQueuedCount;
        tracked.ReadyForEnrollmentCount = batch.ReadyForEnrollmentCount;
        tracked.ManifestJson = batch.ManifestJson;
        tracked.CompletedUtc = batch.CompletedUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetRetryReadyItemsAsync(
        Guid batchId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _context.StudentPhotoAcquisitionItems
            .Where(i => i.BatchId == batchId
                        && i.Status == PhotoAcquisitionItemStatus.RetryQueued
                        && (i.NextAttemptUtc == null || i.NextAttemptUtc <= utcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetEnrollmentReadyItemsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StudentPhotoAcquisitionItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId && i.Status == PhotoAcquisitionItemStatus.ReadyForEnrollment)
            .ToListAsync(cancellationToken);
    }
}
