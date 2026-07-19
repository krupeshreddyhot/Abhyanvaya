using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public sealed class ArtifactRegistryRepository : IArtifactRegistryRepository
{
    private readonly IApplicationDbContext _context;

    public ArtifactRegistryRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(ArtifactRegistryRecord record, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(new ArtifactRegistryEntry
        {
            Id = record.Id,
            EnrollmentId = record.EnrollmentId,
            BatchId = record.BatchId,
            ManifestId = record.ManifestId,
            TenantId = record.TenantId,
            StudentId = record.StudentId,
            ArtifactType = record.ArtifactType,
            Status = record.Status,
            StorageProvider = record.StorageProvider,
            Bucket = record.Bucket,
            StorageKey = record.StorageKey,
            Checksum = record.Checksum,
            FileSize = record.FileSize,
            ArtifactVersion = record.ArtifactVersion,
            StorageVersion = record.StorageVersion,
            CorrelationId = record.CorrelationId,
            TraceId = record.TraceId,
            RetryCount = record.RetryCount,
            CreatedUtc = record.CreatedUtc,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid artifactId,
        ArtifactUploadState status,
        string? verificationJson = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.ArtifactRegistryEntries.FirstOrDefaultAsync(x => x.Id == artifactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artifact registry entry not found: {artifactId}");

        entry.Status = status;
        entry.VerificationResultJson = verificationJson;
        entry.FailureReason = failureReason;
        if (status == ArtifactUploadState.Verified)
        {
            entry.VerifiedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArtifactRegistryRecord>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var entries = await _context.ArtifactRegistryEntries
            .Where(x => x.BatchId == batchId)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForArchiveAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var entries = await _context.ArtifactRegistryEntries
            .Where(x => x.Status == ArtifactUploadState.Verified && x.CreatedUtc <= cutoffUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForDeleteAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var entries = await _context.ArtifactRegistryEntries
            .Where(x => x.Status == ArtifactUploadState.Archived && x.ArchivedUtc <= cutoffUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList();
    }

    private static ArtifactRegistryRecord Map(ArtifactRegistryEntry entry) =>
        new()
        {
            Id = entry.Id,
            EnrollmentId = entry.EnrollmentId,
            BatchId = entry.BatchId,
            ManifestId = entry.ManifestId,
            TenantId = entry.TenantId,
            StudentId = entry.StudentId,
            ArtifactType = entry.ArtifactType,
            Status = entry.Status,
            StorageProvider = entry.StorageProvider,
            Bucket = entry.Bucket,
            StorageKey = entry.StorageKey,
            Checksum = entry.Checksum,
            FileSize = entry.FileSize,
            ArtifactVersion = entry.ArtifactVersion,
            StorageVersion = entry.StorageVersion,
            CorrelationId = entry.CorrelationId,
            TraceId = entry.TraceId,
            RetryCount = entry.RetryCount,
            CreatedUtc = entry.CreatedUtc,
        };
}

public sealed class ArtifactManifestRepository : IArtifactManifestRepository
{
    private readonly IApplicationDbContext _context;

    public ArtifactManifestRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SaveManifestAsync(ArtifactStorageManifestRecord record, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ArtifactStorageManifests.FirstOrDefaultAsync(x => x.Id == record.Id, cancellationToken);
        if (existing is null)
        {
            await _context.AddAsync(new ArtifactStorageManifest
            {
                Id = record.Id,
                BatchId = record.BatchId,
                EnrollmentId = record.EnrollmentId,
                TenantId = record.TenantId,
                ManifestJson = record.ManifestJson,
                ManifestVersion = record.ManifestVersion,
                Status = record.Status,
                CreatedUtc = record.CreatedUtc,
            });
        }
        else
        {
            existing.ManifestJson = record.ManifestJson;
            existing.Status = record.Status;
            existing.VerifiedUtc = record.Status == ArtifactUploadState.Verified ? DateTime.UtcNow : existing.VerifiedUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArtifactStorageManifestRecord?> GetManifestAsync(Guid manifestId, CancellationToken cancellationToken = default)
    {
        var manifest = await _context.ArtifactStorageManifests.FirstOrDefaultAsync(x => x.Id == manifestId, cancellationToken);
        return manifest is null
            ? null
            : new ArtifactStorageManifestRecord
            {
                Id = manifest.Id,
                BatchId = manifest.BatchId,
                EnrollmentId = manifest.EnrollmentId,
                TenantId = manifest.TenantId,
                ManifestJson = manifest.ManifestJson,
                ManifestVersion = manifest.ManifestVersion,
                Status = manifest.Status,
                CreatedUtc = manifest.CreatedUtc,
            };
    }

    public async Task UpdateManifestStatusAsync(Guid manifestId, ArtifactUploadState status, CancellationToken cancellationToken = default)
    {
        var manifest = await _context.ArtifactStorageManifests.FirstOrDefaultAsync(x => x.Id == manifestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artifact manifest not found: {manifestId}");

        manifest.Status = status;
        if (status == ArtifactUploadState.Verified)
        {
            manifest.VerifiedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ArtifactLifecycleManager : IArtifactLifecycleManager
{
    private readonly IArtifactRegistryRepository _registryRepository;
    private readonly IArtifactStorageProvider _storageProvider;
    private readonly IArtifactRetentionPolicy _retentionPolicy;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ArtifactLifecycleManager> _logger;

    public ArtifactLifecycleManager(
        IArtifactRegistryRepository registryRepository,
        IArtifactStorageProvider storageProvider,
        IArtifactRetentionPolicy retentionPolicy,
        IApplicationDbContext context,
        ILogger<ArtifactLifecycleManager> logger)
    {
        _registryRepository = registryRepository;
        _storageProvider = storageProvider;
        _retentionPolicy = retentionPolicy;
        _context = context;
        _logger = logger;
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken = default)
    {
        await ArchiveEligibleAsync(cancellationToken);
        await DeleteEligibleAsync(cancellationToken);
    }

    public async Task ArchiveEligibleAsync(CancellationToken cancellationToken = default)
    {
        if (_retentionPolicy.Mode != ArtifactRetentionMode.ArchiveAfterDays)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-_retentionPolicy.ArchiveAfterDays);
        var eligible = await _registryRepository.GetEligibleForArchiveAsync(cutoff, cancellationToken);

        foreach (var record in eligible)
        {
            await _storageProvider.ArchiveAsync(record.StorageKey, cancellationToken);
            var entry = await _context.ArtifactRegistryEntries.FirstAsync(x => x.Id == record.Id, cancellationToken);
            entry.Status = ArtifactUploadState.Archived;
            entry.ArchivedUtc = DateTime.UtcNow;
            _ = new ArtifactArchived(record.Id, DateTime.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteEligibleAsync(CancellationToken cancellationToken = default)
    {
        if (_retentionPolicy.Mode != ArtifactRetentionMode.DeleteAfterDays)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-_retentionPolicy.DeleteAfterDays);
        var eligible = await _registryRepository.GetEligibleForDeleteAsync(cutoff, cancellationToken);

        foreach (var record in eligible)
        {
            if (record.Status == ArtifactUploadState.Verified)
            {
                _logger.LogWarning("Skipping delete for active artifact artifactId={ArtifactId}", record.Id);
                continue;
            }

            await _storageProvider.DeleteAsync(record.StorageKey, cancellationToken);
            var entry = await _context.ArtifactRegistryEntries.FirstAsync(x => x.Id == record.Id, cancellationToken);
            entry.Status = ArtifactUploadState.Deleted;
            _ = new ArtifactDeleted(record.Id, DateTime.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ArtifactReportService : IArtifactReportService
{
    private readonly IArtifactRegistryRepository _registryRepository;
    private readonly IArtifactManifestRepository _manifestRepository;

    public ArtifactReportService(
        IArtifactRegistryRepository registryRepository,
        IArtifactManifestRepository manifestRepository)
    {
        _registryRepository = registryRepository;
        _manifestRepository = manifestRepository;
    }

    public async Task<ArtifactUploadReport> GenerateUploadReportAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var records = await _registryRepository.GetByBatchAsync(batchId, cancellationToken);
        var stats = BuildStatistics(records);

        return new ArtifactUploadReport
        {
            BatchId = batchId,
            Statistics = stats,
            Failures = records
                .Where(x => x.Status == ArtifactUploadState.Failed)
                .Select(x => $"{x.ArtifactType}:{x.StorageKey}")
                .ToList(),
        };
    }

    public async Task<ArtifactVerificationReport> GenerateVerificationReportAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var records = await _registryRepository.GetByBatchAsync(batchId, cancellationToken);
        var passed = records.Count(x => x.Status == ArtifactUploadState.Verified);
        var failed = records.Count(x => x.Status == ArtifactUploadState.Failed);

        return new ArtifactVerificationReport
        {
            BatchId = batchId,
            Passed = passed,
            Failed = failed,
            Failures = records
                .Where(x => x.Status == ArtifactUploadState.Failed)
                .Select(x => x.StorageKey)
                .ToList(),
        };
    }

    public async Task<ArtifactStorageStatistics> GenerateStorageStatisticsAsync(Guid? batchId = null, CancellationToken cancellationToken = default)
    {
        if (batchId is null)
        {
            return new ArtifactStorageStatistics();
        }

        var records = await _registryRepository.GetByBatchAsync(batchId.Value, cancellationToken);
        return BuildStatistics(records);
    }

    public Task<ArtifactLifecycleReport> GenerateLifecycleReportAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ArtifactLifecycleReport());

    private static ArtifactStorageStatistics BuildStatistics(IReadOnlyList<ArtifactRegistryRecord> records) =>
        new()
        {
            Uploaded = records.Count,
            Verified = records.Count(x => x.Status == ArtifactUploadState.Verified),
            Failed = records.Count(x => x.Status == ArtifactUploadState.Failed),
            Archived = records.Count(x => x.Status == ArtifactUploadState.Archived),
            Deleted = records.Count(x => x.Status == ArtifactUploadState.Deleted),
            RetryCount = records.Sum(x => x.RetryCount),
            AverageFileSize = records.Count == 0 ? 0 : (long)records.Average(x => x.FileSize),
            StorageUsed = records.Sum(x => x.FileSize),
            CompressionRatio = 1m,
        };
}
