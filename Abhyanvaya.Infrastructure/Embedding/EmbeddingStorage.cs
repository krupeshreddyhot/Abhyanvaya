using Abhyanvaya.Application;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Persists embedding lifecycle transitions and completed vectors.
/// </summary>
public sealed class EmbeddingStorage : IEmbeddingStorage
{
    public const int MaxRetryCount = 3;
    private const int LastFailureReasonMaxLength = 500;

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentPhotoEmbeddingQueue _queue;
    private readonly ILogger<EmbeddingStorage> _logger;

    public EmbeddingStorage(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IStudentPhotoEmbeddingQueue queue,
        ILogger<EmbeddingStorage> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _queue = queue;
        _logger = logger;
    }

    public async Task MarkPendingAsync(
        StudentPhotoUploadedMessage message,
        CancellationToken cancellationToken = default)
    {
        await DeactivateActiveEmbeddingsAsync(message.StudentId, cancellationToken);

        var photoVersion = await ResolvePhotoVersionAsync(message.StudentId, cancellationToken);

        var embedding = new StudentFaceEmbedding
        {
            Id = Guid.NewGuid(),
            TenantId = message.TenantId,
            StudentId = message.StudentId,
            EmbeddingVector = [],
            EmbeddingModel = "Pending",
            EmbeddingVersion = "0",
            EmbeddingStatus = EmbeddingStatus.Pending,
            EmbeddingQuality = EmbeddingQuality.Unknown,
            EmbeddingDimension = 0,
            PhotoVersion = photoVersion,
            PhotoKey = message.PhotoKey,
            GeneratedUtc = DateTime.UtcNow,
            GeneratedBy = message.RequestedByUserId,
            IsActive = false
        };

        await _context.AddAsync(embedding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _queue.MarkCompleted(message.StudentId);

        _logger.LogInformation(
            "Face embedding marked pending (no provider configured). StudentId={StudentId} TenantId={TenantId}",
            message.StudentId,
            message.TenantId);
    }

    public async Task<Guid> MarkProcessingAsync(
        StudentPhotoUploadedMessage message,
        CancellationToken cancellationToken = default)
    {
        await DeactivateActiveEmbeddingsAsync(message.StudentId, cancellationToken);

        var photoVersion = await ResolvePhotoVersionAsync(message.StudentId, cancellationToken);

        var embedding = new StudentFaceEmbedding
        {
            Id = Guid.NewGuid(),
            TenantId = message.TenantId,
            StudentId = message.StudentId,
            EmbeddingVector = [],
            EmbeddingModel = "Processing",
            EmbeddingVersion = "0",
            EmbeddingStatus = EmbeddingStatus.Processing,
            EmbeddingQuality = EmbeddingQuality.Unknown,
            EmbeddingDimension = 0,
            PhotoVersion = photoVersion,
            PhotoKey = message.PhotoKey,
            GeneratedUtc = DateTime.UtcNow,
            GeneratedBy = message.RequestedByUserId,
            IsActive = false
        };

        await _context.AddAsync(embedding);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Face embedding processing started. StudentId={StudentId} EmbeddingId={EmbeddingId} TenantId={TenantId}",
            message.StudentId,
            embedding.Id,
            message.TenantId);

        return embedding.Id;
    }

    public async Task<StudentFaceEmbeddingDto> StoreCompletedAsync(
        StudentPhotoUploadedMessage message,
        Guid embeddingId,
        float[] normalizedVector,
        EmbeddingGenerationResult result,
        long photoVersion,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _context.StudentFaceEmbeddings
            .FirstOrDefaultAsync(e => e.Id == embeddingId && e.StudentId == message.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Embedding '{embeddingId}' was not found for student {message.StudentId}.");

        embedding.EmbeddingVector = normalizedVector;
        embedding.EmbeddingModel = result.Model;
        embedding.EmbeddingVersion = result.Version;
        embedding.EmbeddingStatus = EmbeddingStatus.Completed;
        embedding.EmbeddingQuality = result.Quality;
        embedding.EmbeddingDimension = normalizedVector.Length;
        embedding.PhotoVersion = photoVersion;
        embedding.PhotoKey = message.PhotoKey;
        embedding.GeneratedUtc = DateTime.UtcNow;
        embedding.GeneratedBy = message.RequestedByUserId;
        embedding.IsActive = true;
        embedding.RetryCount = 0;
        embedding.LastFailureUtc = null;
        embedding.LastFailureReason = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _queue.MarkCompleted(message.StudentId);

        _logger.LogInformation(
            "Face embedding stored. StudentId={StudentId} EmbeddingId={EmbeddingId} TenantId={TenantId} Status={Status} Quality={Quality} EmbeddingDimension={EmbeddingDimension}",
            message.StudentId,
            embedding.Id,
            message.TenantId,
            embedding.EmbeddingStatus,
            embedding.EmbeddingQuality,
            embedding.EmbeddingDimension);

        return EmbeddingStorageMapper.MapToDto(embedding);
    }

    public async Task RecordFailureAsync(
        StudentPhotoUploadedMessage message,
        Guid embeddingId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _context.StudentFaceEmbeddings
            .FirstOrDefaultAsync(e => e.Id == embeddingId && e.StudentId == message.StudentId, cancellationToken);

        if (embedding == null)
        {
            _queue.MarkCompleted(message.StudentId);
            return;
        }

        embedding.RetryCount++;
        embedding.LastFailureUtc = DateTime.UtcNow;
        embedding.LastFailureReason = TruncateReason(failureReason);
        embedding.IsActive = false;

        if (embedding.RetryCount >= MaxRetryCount)
        {
            embedding.EmbeddingStatus = EmbeddingStatus.Failed;
            _queue.MarkCompleted(message.StudentId);

            _logger.LogWarning(
                "Face embedding failed after max retries. StudentId={StudentId} EmbeddingId={EmbeddingId} RetryCount={RetryCount} Reason={Reason}",
                message.StudentId,
                embeddingId,
                embedding.RetryCount,
                embedding.LastFailureReason);
        }
        else
        {
            embedding.EmbeddingStatus = EmbeddingStatus.Processing;

            _logger.LogWarning(
                "Face embedding attempt failed. StudentId={StudentId} EmbeddingId={EmbeddingId} RetryCount={RetryCount} Reason={Reason}",
                message.StudentId,
                embeddingId,
                embedding.RetryCount,
                embedding.LastFailureReason);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetRetryCountAsync(Guid embeddingId, CancellationToken cancellationToken = default)
    {
        var embedding = await _context.StudentFaceEmbeddings
            .FirstOrDefaultAsync(e => e.Id == embeddingId, cancellationToken);

        if (embedding == null)
        {
            return;
        }

        embedding.RetryCount = 0;
        embedding.LastFailureUtc = null;
        embedding.LastFailureReason = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<long> ResolvePhotoVersionAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        return student?.PhotoUploadedUtc?.Ticks ?? 0L;
    }

    private async Task DeactivateActiveEmbeddingsAsync(int studentId, CancellationToken cancellationToken)
    {
        var activeRows = await _context.StudentFaceEmbeddings
            .Where(e => e.StudentId == studentId && e.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var row in activeRows)
        {
            row.IsActive = false;
            if (row.EmbeddingStatus == EmbeddingStatus.Completed)
            {
                row.EmbeddingStatus = EmbeddingStatus.Inactive;
            }
        }
    }

    private static string TruncateReason(string reason) =>
        reason.Length <= LastFailureReasonMaxLength
            ? reason
            : reason[..LastFailureReasonMaxLength];
}
