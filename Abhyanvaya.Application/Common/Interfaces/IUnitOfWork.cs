namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Unit of work abstraction for persistence and transactional boundaries.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes work inside a single database transaction with automatic commit or rollback.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>Commits the current ambient transaction when one is active.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the current ambient transaction when one is active.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
