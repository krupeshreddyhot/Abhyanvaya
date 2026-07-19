using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class PostgreSqlDistributedLockProvider : IDistributedLockProvider
{
    private readonly ApplicationDbContext _context;

    public PostgreSqlDistributedLockProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IAsyncDisposable?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        _ = timeout;
        var lockKey = Math.Abs(resourceKey.GetHashCode(StringComparison.Ordinal));
        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockKey)", connection);
            command.Parameters.AddWithValue("lockKey", lockKey);

            var acquiredObj = await command.ExecuteScalarAsync(cancellationToken);
            var acquired = acquiredObj is bool b && b;

            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new AdvisoryLockHandle(connection, lockKey);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class AdvisoryLockHandle : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly int _lockKey;
        private bool _released;

        public AdvisoryLockHandle(NpgsqlConnection connection, int lockKey)
        {
            _connection = connection;
            _lockKey = lockKey;
        }

        public async ValueTask DisposeAsync()
        {
            if (_released)
            {
                return;
            }

            try
            {
                await using var command = new NpgsqlCommand($"SELECT pg_advisory_unlock({_lockKey})", _connection);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
                _released = true;
            }
        }
    }
}
