using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Persistence;

public sealed class EnrollmentResultWriter : IEnrollmentResultWriter
{
    private readonly IEnrollmentPersistenceRepository _persistenceRepository;
    private readonly IEnrollmentPersistencePolicy _policy;
    private readonly IEnrollmentDuplicateDetector _duplicateDetector;
    private readonly IEnrollmentPersistenceMetrics _metrics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentResultWriter> _logger;

    public EnrollmentResultWriter(
        IEnrollmentPersistenceRepository persistenceRepository,
        IEnrollmentPersistencePolicy policy,
        IEnrollmentDuplicateDetector duplicateDetector,
        IEnrollmentPersistenceMetrics metrics,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        TimeProvider clock,
        ILogger<EnrollmentResultWriter> logger)
    {
        _persistenceRepository = persistenceRepository;
        _policy = policy;
        _duplicateDetector = duplicateDetector;
        _metrics = metrics;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentPersistenceResult> PersistEmbeddingAsync(
        EnrollmentPersistenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var artifact = request.Artifact;
        var totalStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Enrollment persistence started. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} ModelVersion={ModelVersion}",
            artifact.StudentId,
            artifact.BatchId,
            artifact.CorrelationId,
            artifact.EmbeddingModelVersion);

        var context = await _persistenceRepository.LoadContextAsync(
            artifact.BatchId,
            artifact.StudentId,
            cancellationToken);

        if (context is null)
        {
            return Fail(totalStopwatch, artifact, EnrollmentPersistenceFailureCodes.MissingEnrollment,
                "Enrollment item, batch, or student was not found.");
        }

        var item = context.Item;

        var duplicate = await _duplicateDetector.DetectAsync(new EnrollmentDuplicateDetectionRequest
        {
            ItemId = item.Id,
            StudentId = item.StudentId,
            BatchId = item.BatchId,
            EmbeddingModel = artifact.EmbeddingModel,
            EmbeddingModelVersion = artifact.EmbeddingModelVersion,
            PipelineVersion = artifact.PipelineVersion,
            ExistingEmbeddingId = item.StudentFaceEmbeddingId,
            ItemStatus = item.Status,
            ItemEmbeddingVersion = item.EmbeddingVersion,
        }, cancellationToken);

        if (duplicate.IsDuplicate && duplicate.ExistingEmbeddingId.HasValue)
        {
            totalStopwatch.Stop();
            _metrics.RecordSuccess(
                artifact.EmbeddingDimension,
                totalStopwatch.ElapsedMilliseconds,
                0,
                rowsInserted: 0,
                rowsUpdated: 0,
                isDuplicate: true);

            _logger.LogInformation(
                "Enrollment persistence duplicate detected. StudentId={StudentId} EmbeddingId={EmbeddingId} CorrelationId={CorrelationId} DurationMs={DurationMs}",
                artifact.StudentId,
                duplicate.ExistingEmbeddingId,
                artifact.CorrelationId,
                totalStopwatch.ElapsedMilliseconds);

            return BuildDuplicateSuccess(artifact, duplicate.ExistingEmbeddingId.Value, totalStopwatch.Elapsed, request.Warnings);
        }

        if (item.Status != EnrollmentStatus.Embedding)
        {
            return Fail(totalStopwatch, artifact, EnrollmentPersistenceFailureCodes.ValidationMismatch,
                $"Enrollment item must be in Embedding status but was {item.Status}.");
        }

        var policyDecision = _policy.Evaluate(new EnrollmentPersistencePolicyContext
        {
            ItemId = item.Id,
            StudentId = item.StudentId,
            BatchId = item.BatchId,
            CurrentStatus = item.Status,
            ExistingEmbeddingId = item.StudentFaceEmbeddingId,
            ExistingEmbeddingVersion = item.EmbeddingVersion,
            RequestedEmbeddingVersion = artifact.EmbeddingModelVersion,
            PipelineVersion = artifact.PipelineVersion,
        });

        if (!policyDecision.AllowPersist)
        {
            if (policyDecision.ReturnExistingOnDuplicate && item.StudentFaceEmbeddingId.HasValue)
            {
                return BuildDuplicateSuccess(artifact, item.StudentFaceEmbeddingId.Value, totalStopwatch.Elapsed, request.Warnings);
            }

            return Fail(totalStopwatch, artifact, EnrollmentPersistenceFailureCodes.PolicyRejected,
                policyDecision.RejectionReason ?? "Persistence rejected by policy.");
        }

        var persistedUtc = _clock.GetUtcNow().UtcDateTime;
        var createdBy = _currentUser.UserId > 0 ? _currentUser.UserId : (int?)null;
        var audit = BuildAudit(item, artifact, createdBy, persistedUtc, EnrollmentPersistenceState.ReadyForRecognition, null);

        var dbStopwatch = Stopwatch.StartNew();
        var transactionStopwatch = Stopwatch.StartNew();
        EnrollmentPersistenceWriteOutcome? writeOutcome = null;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                writeOutcome = await _persistenceRepository.PersistEmbeddingAsync(
                    new EnrollmentPersistenceWriteRequest
                    {
                        Item = context.Item,
                        Batch = context.Batch,
                        Student = context.Student,
                        Artifact = artifact,
                        Metadata = request.Metadata,
                        Audit = audit,
                        CreatedByUserId = createdBy,
                        PersistedUtc = persistedUtc,
                        KeepHistoricalVersions = policyDecision.KeepHistoricalVersions,
                    },
                    ct);

                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _metrics.RecordFailure(EnrollmentPersistenceFailureCodes.ConcurrencyConflict);
            return Fail(totalStopwatch, artifact, EnrollmentPersistenceFailureCodes.ConcurrencyConflict, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordFailure(EnrollmentPersistenceFailureCodes.DatabaseFailure);
            _ = new EmbeddingPersistenceFailed(
                item.Id,
                artifact.StudentId,
                artifact.BatchId,
                artifact.CorrelationId,
                EnrollmentPersistenceFailureCodes.DatabaseFailure,
                ex.Message,
                DateTime.UtcNow);

            return Fail(totalStopwatch, artifact, EnrollmentPersistenceFailureCodes.DatabaseFailure, ex.Message);
        }

        dbStopwatch.Stop();
        transactionStopwatch.Stop();
        totalStopwatch.Stop();

        var embeddingId = writeOutcome!.EmbeddingId;
        _metrics.RecordSuccess(
            artifact.EmbeddingDimension,
            totalStopwatch.ElapsedMilliseconds,
            dbStopwatch.ElapsedMilliseconds,
            writeOutcome.RowsInserted,
            writeOutcome.RowsUpdated,
            isDuplicate: false);

        _ = new EmbeddingPersisted(
            item.Id,
            embeddingId,
            artifact.StudentId,
            artifact.BatchId,
            artifact.CorrelationId,
            artifact.EmbeddingModelVersion,
            persistedUtc);

        _logger.LogInformation(
            "Enrollment persistence completed. StudentId={StudentId} EmbeddingId={EmbeddingId} CorrelationId={CorrelationId} ModelVersion={ModelVersion} DurationMs={DurationMs}",
            artifact.StudentId,
            embeddingId,
            artifact.CorrelationId,
            artifact.EmbeddingModelVersion,
            totalStopwatch.ElapsedMilliseconds);

        return EnrollmentPersistenceResult.Succeeded(
            artifact.StudentId,
            artifact.BatchId,
            embeddingId,
            EnrollmentStatus.Completed,
            EnrollmentPersistenceState.ReadyForRecognition,
            _clock.GetUtcNow(),
            totalStopwatch.Elapsed,
            artifact,
            request.Warnings,
            new EnrollmentPersistenceStatistics
            {
                WriteDuration = totalStopwatch.Elapsed,
                DatabaseDuration = dbStopwatch.Elapsed,
                TransactionDuration = transactionStopwatch.Elapsed,
                RowsInserted = writeOutcome.RowsInserted,
                RowsUpdated = writeOutcome.RowsUpdated,
                RetryCount = item.RetryCount,
                Warnings = request.Warnings,
            });
    }

    private EnrollmentPersistenceResult BuildDuplicateSuccess(
        EnrollmentEmbeddingArtifact artifact,
        Guid embeddingId,
        TimeSpan duration,
        IReadOnlyList<string>? warnings) =>
        EnrollmentPersistenceResult.Succeeded(
            artifact.StudentId,
            artifact.BatchId,
            embeddingId,
            EnrollmentStatus.Completed,
            EnrollmentPersistenceState.ReadyForRecognition,
            _clock.GetUtcNow(),
            duration,
            artifact,
            warnings,
            new EnrollmentPersistenceStatistics
            {
                WriteDuration = duration,
                DatabaseDuration = TimeSpan.Zero,
                TransactionDuration = TimeSpan.Zero,
                RowsInserted = 0,
                RowsUpdated = 0,
                RetryCount = 0,
                Warnings = warnings,
            },
            isDuplicate: true);

    private EnrollmentPersistenceResult Fail(
        Stopwatch stopwatch,
        EnrollmentEmbeddingArtifact artifact,
        string code,
        string reason)
    {
        stopwatch.Stop();
        _metrics.RecordFailure(code);

        _logger.LogWarning(
            "Enrollment persistence failed. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} FailureCode={FailureCode} DurationMs={DurationMs} Reason={Reason}",
            artifact.StudentId,
            artifact.BatchId,
            artifact.CorrelationId,
            code,
            stopwatch.ElapsedMilliseconds,
            reason);

        return EnrollmentPersistenceResult.Failed(artifact, stopwatch.Elapsed, code, reason);
    }

    private static EnrollmentPersistenceAudit BuildAudit(
        StudentEnrollmentItem item,
        EnrollmentEmbeddingArtifact artifact,
        int? userId,
        DateTime timestampUtc,
        string outcome,
        string? detail) =>
        new()
        {
            Id = Guid.NewGuid(),
            EnrollmentItemId = item.Id,
            TenantId = item.TenantId,
            StudentId = item.StudentId,
            EmbeddingId = item.StudentFaceEmbeddingId,
            PipelineVersion = artifact.PipelineVersion,
            StorageVersion = artifact.StorageVersion,
            ValidationVersion = artifact.ValidationVersion,
            ModelVersion = artifact.EmbeddingModelVersion,
            UserId = userId,
            TimestampUtc = timestampUtc,
            CorrelationId = artifact.CorrelationId,
            Outcome = outcome,
            Detail = detail,
        };
}
