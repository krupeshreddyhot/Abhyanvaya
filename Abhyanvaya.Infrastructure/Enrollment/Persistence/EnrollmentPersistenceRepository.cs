using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Enrollment.Persistence;

public sealed class EnrollmentPersistenceRepository : IEnrollmentPersistenceRepository
{
    private readonly IApplicationDbContext _context;

    public EnrollmentPersistenceRepository(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentPersistenceContext?> LoadContextAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var item = await _context.StudentEnrollmentItems
            .FirstOrDefaultAsync(i => i.BatchId == batchId && i.StudentId == studentId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var batch = await _context.StudentEnrollmentBatches
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
        {
            return null;
        }

        return new EnrollmentPersistenceContext
        {
            Item = item,
            Batch = batch,
            Student = student,
        };
    }

    public Task<StudentFaceEmbedding?> GetEmbeddingByIdAsync(
        Guid embeddingId,
        CancellationToken cancellationToken = default) =>
        _context.StudentFaceEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == embeddingId, cancellationToken);

    public async Task<EnrollmentPersistenceWriteOutcome> PersistEmbeddingAsync(
        EnrollmentPersistenceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = request.Item;
        var batch = request.Batch;
        var student = request.Student;
        var artifact = request.Artifact;
        var vector = artifact.EmbeddingVector.ToArray();

        var rowsInserted = 0;
        var rowsUpdated = 0;

        if (request.KeepHistoricalVersions)
        {
            var activeRows = await _context.StudentFaceEmbeddings
                .Where(e => e.StudentId == student.Id && e.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var row in activeRows)
            {
                row.IsActive = false;
                if (row.EmbeddingStatus == EmbeddingStatus.Completed)
                {
                    row.EmbeddingStatus = EmbeddingStatus.Inactive;
                }

                rowsUpdated++;
            }
        }

        var embeddingId = Guid.NewGuid();
        var embedding = new StudentFaceEmbedding
        {
            Id = embeddingId,
            TenantId = item.TenantId,
            StudentId = student.Id,
            EmbeddingVector = vector,
            EmbeddingModel = artifact.EmbeddingModel,
            EmbeddingVersion = artifact.EmbeddingModelVersion,
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingQuality = MapQuality(artifact.QualityScore),
            EmbeddingDimension = artifact.EmbeddingDimension,
            PhotoVersion = student.PhotoUploadedUtc?.Ticks ?? 0L,
            PhotoKey = item.PhotoKey ?? student.PhotoKey ?? string.Empty,
            GeneratedUtc = request.PersistedUtc,
            GeneratedBy = request.CreatedByUserId,
            IsActive = true,
        };

        await _context.AddAsync(embedding);
        rowsInserted++;

        var snapshot = new EnrollmentEmbeddingVersionSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = item.TenantId,
            StudentFaceEmbeddingId = embeddingId,
            EnrollmentItemId = item.Id,
            EmbeddingModel = artifact.EmbeddingModel,
            EmbeddingModelVersion = artifact.EmbeddingModelVersion,
            PipelineVersion = artifact.PipelineVersion,
            ValidationVersion = artifact.ValidationVersion,
            StorageVersion = artifact.StorageVersion,
            ManifestVersion = artifact.ManifestVersion,
            ArtifactVersion = artifact.ArtifactVersion,
            FrameworkVersion = request.Metadata?.FrameworkVersion,
            OnnxVersion = request.Metadata?.OnnxVersion,
            CorrelationId = artifact.CorrelationId,
            CreatedUtc = request.PersistedUtc,
        };

        await _context.AddAsync(snapshot);
        rowsInserted++;

        await _context.AddAsync(request.Audit);
        rowsInserted++;

        if (!string.IsNullOrWhiteSpace(item.PhotoKey))
        {
            student.PhotoKey = item.PhotoKey;
            student.PhotoUploadedUtc = request.PersistedUtc;
            rowsUpdated++;
        }

        var fromStatus = item.Status;
        EnrollmentStatusTransitionRules.EnsureAllowed(fromStatus, EnrollmentStatus.Completed);

        item.Status = EnrollmentStatus.Completed;
        item.StudentFaceEmbeddingId = embeddingId;
        item.EmbeddingVersion = artifact.EmbeddingModelVersion;
        item.QualityScore = artifact.QualityScore;
        item.CompletedUtc = request.PersistedUtc;
        item.LastAttemptUtc = request.PersistedUtc;
        item.FailureCategory = null;
        item.LastError = null;
        rowsUpdated++;

        EnrollmentBatchCounterRules.ApplyTransition(batch, fromStatus, EnrollmentStatus.Completed);
        rowsUpdated++;

        return new EnrollmentPersistenceWriteOutcome
        {
            EmbeddingId = embeddingId,
            RowsInserted = rowsInserted,
            RowsUpdated = rowsUpdated,
        };
    }

    private static EmbeddingQuality MapQuality(float score) =>
        score switch
        {
            >= 0.9f => EmbeddingQuality.Excellent,
            >= 0.75f => EmbeddingQuality.Good,
            >= 0.5f => EmbeddingQuality.Fair,
            >= 0.25f => EmbeddingQuality.Poor,
            _ => EmbeddingQuality.Unknown,
        };
}
