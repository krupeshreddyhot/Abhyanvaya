using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    private IDbContextTransaction? _ambientTransaction;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_ambientTransaction == null)
        {
            return;
        }

        await _ambientTransaction.CommitAsync(cancellationToken);
        await _ambientTransaction.DisposeAsync();
        _ambientTransaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_ambientTransaction == null)
        {
            return;
        }

        _logger?.LogWarning("Transaction rollback initiated.");
        await _ambientTransaction.RollbackAsync(cancellationToken);
        await _ambientTransaction.DisposeAsync();
        _ambientTransaction = null;
    }

    async Task IUnitOfWork.ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        _ambientTransaction = transaction;
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Transaction rollback due to failure.");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _ambientTransaction = null;
        }
    }
}
