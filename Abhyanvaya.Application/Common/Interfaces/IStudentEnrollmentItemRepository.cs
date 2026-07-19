using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Persistence access for <see cref="StudentEnrollmentItem"/>. Thin data access only — claiming semantics
/// are enforced at the database layer; status transitions and retry policy belong to enrollment services.
/// </summary>
public interface IStudentEnrollmentItemRepository
{
    Task CreateItemsAsync(
        IReadOnlyCollection<StudentEnrollmentItem> items,
        CancellationToken cancellationToken = default);

    Task<StudentEnrollmentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StudentEnrollmentItem?> GetByBatchAndStudentAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentItem>> GetByBatchAsync(
        Guid batchId,
        EnrollmentStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentItem>> GetPendingItemsAsync(
        int limit,
        Guid? batchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next queue-eligible item using PostgreSQL <c>FOR UPDATE SKIP LOCKED</c>.
    /// Returns null when no claimable item exists.
    /// </summary>
    Task<StudentEnrollmentItem?> ClaimNextItemAsync(
        Guid? batchId = null,
        CancellationToken cancellationToken = default);

    Task UpdateItemAsync(StudentEnrollmentItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentItem>> GetFailedItemsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentItem>> GetHistoryForStudentAsync(
        int tenantId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(
        Guid batchId,
        EnrollmentStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentItem>> GetRecentlyCompletedAsync(
        Guid batchId,
        int limit,
        CancellationToken cancellationToken = default);
}
