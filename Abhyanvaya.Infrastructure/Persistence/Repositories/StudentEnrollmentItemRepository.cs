using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

public sealed class StudentEnrollmentItemRepository : IStudentEnrollmentItemRepository
{
    private const string ClaimNextItemSql = """
        UPDATE "StudentEnrollmentItem" AS item
        SET
            "Status" = @claimStatus,
            "LastAttemptUtc" = @utcNow,
            "DownloadStartedUtc" = COALESCE(item."DownloadStartedUtc", @utcNow),
            "RowVersion" = @rowVersion
        FROM (
            SELECT i."Id"
            FROM "StudentEnrollmentItem" i
            INNER JOIN "StudentEnrollmentBatch" b ON b."Id" = i."BatchId"
            WHERE i."Status" IN (@pendingStatus, @retryStatus)
              AND b."CancellationRequestedUtc" IS NULL
              AND (@batchId IS NULL OR i."BatchId" = @batchId)
            ORDER BY i."CreatedUtc"
            FOR UPDATE OF i SKIP LOCKED
            LIMIT 1
        ) AS candidate
        WHERE item."Id" = candidate."Id"
        RETURNING item."Id"
        """;

    private readonly ApplicationDbContext _context;

    public StudentEnrollmentItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateItemsAsync(
        IReadOnlyCollection<StudentEnrollmentItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        await _context.Set<StudentEnrollmentItem>().AddRangeAsync(items, cancellationToken);
    }

    public Task<StudentEnrollmentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<StudentEnrollmentItem?> GetByBatchAndStudentAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentItems
            .FirstOrDefaultAsync(i => i.BatchId == batchId && i.StudentId == studentId, cancellationToken);

    public async Task<IReadOnlyList<StudentEnrollmentItem>> GetByBatchAsync(
        Guid batchId,
        EnrollmentStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StudentEnrollmentItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId);

        if (statusFilter.HasValue)
        {
            query = query.Where(i => i.Status == statusFilter.Value);
        }

        return await query.OrderBy(i => i.CreatedUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentEnrollmentItem>> GetPendingItemsAsync(
        int limit,
        Guid? batchId = null,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<StudentEnrollmentItem>();
        }

        var query = _context.StudentEnrollmentItems
            .AsNoTracking()
            .Where(i =>
                (i.Status == EnrollmentStatus.Pending || i.Status == EnrollmentStatus.RetryRequired)
                && i.Batch.CancellationRequestedUtc == null);

        if (batchId.HasValue)
        {
            query = query.Where(i => i.BatchId == batchId.Value);
        }

        return await query
            .OrderBy(i => i.CreatedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentEnrollmentItem?> ClaimNextItemAsync(
        Guid? batchId = null,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var rowVersion = Guid.NewGuid().ToByteArray();

        var parameters = new[]
        {
            new NpgsqlParameter("claimStatus", NpgsqlDbType.Integer)
            {
                Value = (int)EnrollmentStatus.Downloading,
            },
            new NpgsqlParameter("utcNow", NpgsqlDbType.TimestampTz) { Value = utcNow },
            new NpgsqlParameter("rowVersion", NpgsqlDbType.Bytea) { Value = rowVersion },
            new NpgsqlParameter("pendingStatus", NpgsqlDbType.Integer)
            {
                Value = (int)EnrollmentStatus.Pending,
            },
            new NpgsqlParameter("retryStatus", NpgsqlDbType.Integer)
            {
                Value = (int)EnrollmentStatus.RetryRequired,
            },
            new NpgsqlParameter("batchId", NpgsqlDbType.Uuid)
            {
                Value = batchId.HasValue ? batchId.Value : DBNull.Value,
            },
        };

        var claimedId = await _context.Database
            .SqlQueryRaw<Guid>(ClaimNextItemSql, parameters)
            .FirstOrDefaultAsync(cancellationToken);

        if (claimedId == Guid.Empty)
        {
            return null;
        }

        DetachTrackedItem(claimedId);

        return await _context.StudentEnrollmentItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == claimedId, cancellationToken);
    }

    public Task UpdateItemAsync(StudentEnrollmentItem item, CancellationToken cancellationToken = default)
    {
        _context.Set<StudentEnrollmentItem>().Update(item);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<StudentEnrollmentItem>> GetFailedItemsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        await _context.StudentEnrollmentItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId && i.Status == EnrollmentStatus.Failed)
            .OrderBy(i => i.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentItems.AnyAsync(
            i => i.BatchId == batchId && i.StudentId == studentId,
            cancellationToken);

    public async Task<IReadOnlyList<StudentEnrollmentItem>> GetHistoryForStudentAsync(
        int tenantId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await _context.StudentEnrollmentItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.StudentId == studentId)
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountByStatusAsync(
        Guid batchId,
        EnrollmentStatus status,
        CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentItems.CountAsync(
            i => i.BatchId == batchId && i.Status == status,
            cancellationToken);

    public async Task<IReadOnlyList<StudentEnrollmentItem>> GetRecentlyCompletedAsync(
        Guid batchId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<StudentEnrollmentItem>();
        }

        return await _context.StudentEnrollmentItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId && i.Status == EnrollmentStatus.Completed && i.CompletedUtc != null)
            .OrderByDescending(i => i.CompletedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private void DetachTrackedItem(Guid itemId)
    {
        var tracked = _context.ChangeTracker
            .Entries<StudentEnrollmentItem>()
            .FirstOrDefault(entry => entry.Entity.Id == itemId);

        if (tracked != null)
        {
            tracked.State = EntityState.Detached;
        }
    }
}
