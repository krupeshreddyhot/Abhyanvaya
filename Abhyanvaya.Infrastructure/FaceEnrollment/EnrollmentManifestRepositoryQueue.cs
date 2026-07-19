using System.Threading.Channels;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.FaceEnrollment;

public sealed class EnrollmentManifestGenerator : IEnrollmentManifestGenerator
{
    public EnrollmentManifest Generate(FaceEnrollmentBatch batch, IEnumerable<FaceEnrollmentJob> jobs)
    {
        var materialized = jobs.ToList();
        return new EnrollmentManifest
        {
            BatchId = batch.Id,
            ManifestId = Guid.NewGuid(),
            SuccessList = materialized.Where(j => j.State == EnrollmentState.Completed).Select(ToEntry).ToList(),
            FailureList = materialized.Where(j => j.State == EnrollmentState.Failed).Select(ToEntry).ToList(),
            DuplicateList = materialized
                .Where(j => j.FailureReason?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true)
                .Select(ToEntry)
                .ToList(),
            RetryList = materialized.Where(j => j.State == EnrollmentState.Retry).Select(ToEntry).ToList(),
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    private static EnrollmentManifestEntry ToEntry(FaceEnrollmentJob job) =>
        new()
        {
            EnrollmentId = job.Id,
            StudentId = job.StudentId,
            StudentNumber = job.StudentNumber,
            FinalState = job.State,
            QualityScore = job.QualityScore,
            FailureReason = job.FailureReason,
        };
}

public sealed class EnrollmentReportService : IEnrollmentReportService
{
    private readonly IEnrollmentRepository _repository;
    private readonly IEnrollmentManifestGenerator _manifestGenerator;

    public EnrollmentReportService(IEnrollmentRepository repository, IEnrollmentManifestGenerator manifestGenerator)
    {
        _repository = repository;
        _manifestGenerator = manifestGenerator;
    }

    public async Task<EnrollmentReport> GenerateBatchReportAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Batch not found: {batchId}");

        var jobs = await _repository.GetJobsByBatchAsync(batchId, cancellationToken);
        var manifest = _manifestGenerator.Generate(batch, jobs);

        var completed = jobs.Count(j => j.State == EnrollmentState.Completed);
        var total = jobs.Count;

        return new EnrollmentReport
        {
            BatchId = batchId,
            Manifest = manifest,
            Statistics = new EnrollmentStatistics
            {
                Queued = jobs.Count(j => j.State == EnrollmentState.Queued),
                Completed = completed,
                Failed = jobs.Count(j => j.State == EnrollmentState.Failed),
                Duplicates = manifest.DuplicateList.Count,
                RetryCount = jobs.Sum(j => j.RetryCount),
                SuccessRate = total == 0 ? 0 : (decimal)completed / total,
            },
            FailureDetails = manifest.FailureList.Select(f => $"{f.StudentNumber}: {f.FailureReason}").ToList(),
            DuplicateDetails = manifest.DuplicateList.Select(d => $"{d.StudentNumber}: {d.FailureReason}").ToList(),
        };
    }
}

public sealed class ArtifactUploadQueue : IArtifactUploadQueue
{
    private readonly Channel<ArtifactUploadRequest> _channel = Channel.CreateUnbounded<ArtifactUploadRequest>();
    private int _depth;

    public int QueueDepth => Volatile.Read(ref _depth);

    public ValueTask EnqueueAsync(ArtifactUploadRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _depth);
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<ArtifactUploadRequest> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _depth);
            yield return request;
        }
    }
}

public sealed class FaceEnrollmentRecoveryService : IFaceEnrollmentRecoveryService
{
    private readonly IEnrollmentRepository _repository;
    private readonly IEnrollmentBatchProcessor _batchProcessor;

    public FaceEnrollmentRecoveryService(IEnrollmentRepository repository, IEnrollmentBatchProcessor batchProcessor)
    {
        _repository = repository;
        _batchProcessor = batchProcessor;
    }

    public async Task<EnrollmentBatchResult> ResumeBatchAsync(
        Guid batchId,
        IReadOnlyDictionary<Guid, byte[]> photoBytesByItemId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Batch not found: {batchId}");

        var incomplete = await _repository.GetIncompleteJobsAsync(batchId, cancellationToken);
        foreach (var job in incomplete)
        {
            if (job.State == EnrollmentState.Retry)
            {
                job.State = EnrollmentState.Queued;
                await _repository.UpdateJobAsync(job, cancellationToken);
            }
        }

        batch.State = EnrollmentState.Processing;
        await _repository.UpdateBatchAsync(batch, cancellationToken);
        return await _batchProcessor.ProcessBatchAsync(batch, photoBytesByItemId, cancellationToken);
    }
}

public sealed class EnrollmentRepository : IEnrollmentRepository
{
    private readonly IApplicationDbContext _context;

    public EnrollmentRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FaceEnrollmentBatch> CreateBatchAsync(
        Guid acquisitionBatchId,
        int tenantId,
        IReadOnlyList<StudentPhotoAcquisitionItem> items,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var batch = new FaceEnrollmentBatch
        {
            Id = batchId,
            AcquisitionBatchId = acquisitionBatchId,
            TenantId = tenantId,
            TotalItems = items.Count,
            CreatedUtc = now,
            State = EnrollmentState.Queued,
        };

        await _context.AddAsync(batch);

        foreach (var item in items)
        {
            await _context.AddAsync(new FaceEnrollmentJob
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                AcquisitionItemId = item.Id,
                AcquisitionBatchId = acquisitionBatchId,
                TenantId = tenantId,
                StudentId = item.StudentId,
                StudentNumber = item.StudentNumber,
                State = EnrollmentState.Queued,
                CorrelationId = Guid.NewGuid(),
                TraceId = Guid.NewGuid(),
                CreatedUtc = now,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<FaceEnrollmentBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.FaceEnrollmentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    public async Task<FaceEnrollmentJob?> GetJobAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        return await _context.FaceEnrollmentJobs.FirstOrDefaultAsync(j => j.Id == enrollmentId, cancellationToken);
    }

    public async Task UpdateJobAsync(FaceEnrollmentJob job, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.FaceEnrollmentJobs.FirstOrDefaultAsync(j => j.Id == job.Id, cancellationToken);
        if (tracked is null)
        {
            return;
        }

        tracked.State = job.State;
        tracked.ArtifactJson = job.ArtifactJson;
        tracked.FailureReason = job.FailureReason;
        tracked.RetryCount = job.RetryCount;
        tracked.QualityScore = job.QualityScore;
        tracked.StartedUtc = job.StartedUtc;
        tracked.CompletedUtc = job.CompletedUtc;
        tracked.LastStateChangeUtc = job.LastStateChangeUtc;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(FaceEnrollmentBatch batch, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.FaceEnrollmentBatches.FirstOrDefaultAsync(b => b.Id == batch.Id, cancellationToken);
        if (tracked is null)
        {
            return;
        }

        tracked.State = batch.State;
        tracked.CompletedCount = batch.CompletedCount;
        tracked.FailedCount = batch.FailedCount;
        tracked.DuplicateCount = batch.DuplicateCount;
        tracked.RetryCount = batch.RetryCount;
        tracked.ManifestJson = batch.ManifestJson;
        tracked.CompletedUtc = batch.CompletedUtc;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FaceEnrollmentJob>> GetIncompleteJobsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.FaceEnrollmentJobs
            .Where(j => j.BatchId == batchId
                        && j.State != EnrollmentState.Completed
                        && j.State != EnrollmentState.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FaceEnrollmentJob>> GetJobsByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.FaceEnrollmentJobs
            .AsNoTracking()
            .Where(j => j.BatchId == batchId)
            .ToListAsync(cancellationToken);
    }
}
