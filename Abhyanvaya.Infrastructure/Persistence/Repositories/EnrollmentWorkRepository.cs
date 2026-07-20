using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

public sealed class EnrollmentWorkRepository : IEnrollmentWorkRepository
{
    private const string ClaimNextSql = """
        UPDATE "StudentEnrollmentItem" AS item
        SET
            "LastAttemptUtc" = @utcNow,
            "RowVersion" = @rowVersion
        FROM (
            SELECT i."Id"
            FROM "StudentEnrollmentItem" i
            INNER JOIN "StudentEnrollmentBatch" b ON b."Id" = i."BatchId"
            WHERE i."Status" IN (@pendingStatus, @retryStatus)
              AND b."CancellationRequestedUtc" IS NULL
              AND (i."NextAttemptUtc" IS NULL OR i."NextAttemptUtc" <= @utcNow)
              AND NOT EXISTS (
                  SELECT 1
                  FROM "EnrollmentWorkLease" l
                  WHERE l."ItemId" = i."Id"
                    AND l."IsActive" = TRUE
                    AND l."ExpiresUtc" > @utcNow)
            ORDER BY b."Priority" DESC, i."CreatedUtc"
            FOR UPDATE OF i SKIP LOCKED
            LIMIT 1
        ) AS candidate
        WHERE item."Id" = candidate."Id"
        RETURNING item."Id"
        """;

    private readonly ApplicationDbContext _context;

    public EnrollmentWorkRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentWorkItem?> ClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var rowVersion = Guid.NewGuid().ToByteArray();

        var parameters = new[]
        {
            new NpgsqlParameter("utcNow", NpgsqlDbType.TimestampTz) { Value = utcNow },
            new NpgsqlParameter("rowVersion", NpgsqlDbType.Bytea) { Value = rowVersion },
            new NpgsqlParameter("pendingStatus", NpgsqlDbType.Integer) { Value = (int)EnrollmentStatus.Pending },
            new NpgsqlParameter("retryStatus", NpgsqlDbType.Integer) { Value = (int)EnrollmentStatus.RetryRequired },
        };

        var claimedId = await PostgreSqlReturningExecutor.ExecuteReturningGuidAsync(
            _context.Database,
            ClaimNextSql,
            parameters,
            cancellationToken);

        if (claimedId is null || claimedId == Guid.Empty)
        {
            return null;
        }

        return await LoadWorkItemAsync(claimedId.Value, cancellationToken);
    }

    public Task<EnrollmentWorkItem?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        LoadWorkItemAsync(itemId, cancellationToken);

    public async Task ScheduleRetryAsync(
        Guid itemId,
        DateTime nextAttemptUtc,
        string? lastError,
        FailureCategory? failureCategory,
        CancellationToken cancellationToken = default)
    {
        var item = await _context.StudentEnrollmentItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item == null)
        {
            return;
        }

        item.Status = EnrollmentStatus.RetryRequired;
        item.NextAttemptUtc = nextAttemptUtc;
        item.LastAttemptUtc = DateTime.UtcNow;
        item.LastError = lastError;
        item.FailureCategory = failureCategory;
        item.RetryCount++;
        item.RowVersion = Guid.NewGuid().ToByteArray();
        _context.Set<StudentEnrollmentItem>().Update(item);
    }

    public async Task<IReadOnlyList<EnrollmentWorkItem>> GetStuckInFlightItemsAsync(
        DateTime staleBeforeUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<EnrollmentWorkItem>();
        }

        var stuckStatuses = new[]
        {
            EnrollmentStatus.Downloading,
            EnrollmentStatus.Validating,
            EnrollmentStatus.Embedding,
        };

        var itemIds = await _context.StudentEnrollmentItems
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => stuckStatuses.Contains(i.Status)
                        && i.LastAttemptUtc != null
                        && i.LastAttemptUtc < staleBeforeUtc)
            .OrderBy(i => i.LastAttemptUtc)
            .Select(i => i.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var results = new List<EnrollmentWorkItem>(itemIds.Count);
        foreach (var itemId in itemIds)
        {
            var workItem = await LoadWorkItemAsync(itemId, cancellationToken);
            if (workItem != null)
            {
                results.Add(workItem);
            }
        }

        return results;
    }

    public async Task RequeueAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await _context.StudentEnrollmentItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item == null)
        {
            return;
        }

        if (item.Status is EnrollmentStatus.Completed or EnrollmentStatus.Cancelled or EnrollmentStatus.Failed)
        {
            return;
        }

        item.Status = EnrollmentStatus.RetryRequired;
        item.NextAttemptUtc = DateTime.UtcNow;
        item.LastAttemptUtc = DateTime.UtcNow;
        item.RowVersion = Guid.NewGuid().ToByteArray();
        _context.Set<StudentEnrollmentItem>().Update(item);
    }

    public async Task<int> RequeueUnleasedInFlightItemsAsync(
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return 0;
        }

        var inFlightStatuses = new[]
        {
            EnrollmentStatus.Downloading,
            EnrollmentStatus.Validating,
            EnrollmentStatus.Embedding,
        };

        var itemIds = await _context.StudentEnrollmentItems
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => inFlightStatuses.Contains(i.Status))
            .Where(i => !_context.EnrollmentWorkLeases
                .IgnoreQueryFilters()
                .Any(l => l.ItemId == i.Id && l.IsActive && l.ExpiresUtc > utcNow))
            .OrderBy(i => i.LastAttemptUtc)
            .Select(i => i.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var itemId in itemIds)
        {
            await RequeueAsync(itemId, cancellationToken);
        }

        return itemIds.Count;
    }

    private async Task<EnrollmentWorkItem?> LoadWorkItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var row = await _context.StudentEnrollmentItems
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.Id == itemId)
            .Select(i => new
            {
                Item = i,
                Batch = i.Batch,
                StudentNumber = i.Student.StudentNumber,
                CollegeCode = i.Batch.College.Code,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            return null;
        }

        return Map(row.Item, row.Batch, row.StudentNumber, row.CollegeCode);
    }

    private static EnrollmentWorkItem Map(
        StudentEnrollmentItem item,
        StudentEnrollmentBatch batch,
        string studentNumber,
        string collegeCode) =>
        new()
        {
            ItemId = item.Id,
            BatchId = item.BatchId,
            TenantId = item.TenantId,
            StudentId = item.StudentId,
            Status = item.Status,
            PipelineVersion = batch.PipelineVersion,
            CorrelationId = batch.CorrelationId,
            PhotoProviderName = batch.PhotoProviderName,
            CollegeId = batch.CollegeId,
            AcademicYear = batch.AcademicYear,
            StudentNumber = studentNumber,
            CollegeCode = collegeCode,
            SourceUrl = item.SourceUrl,
            ContentType = item.ContentType,
            ByteSize = item.ByteSize,
            RetryCount = item.RetryCount,
            NextAttemptUtc = item.NextAttemptUtc,
            BatchPriority = batch.Priority,
            RowVersion = item.RowVersion,
        };
}
