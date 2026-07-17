using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lockKey)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lockKey";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        var acquiredObj = await command.ExecuteScalarAsync(cancellationToken);
        var acquired = acquiredObj is bool b && b;

        return acquired ? new AdvisoryLockHandle(connection, lockKey) : null;
    }

    private sealed class AdvisoryLockHandle : IAsyncDisposable
    {
        private readonly System.Data.Common.DbConnection _connection;
        private readonly int _lockKey;
        private bool _released;

        public AdvisoryLockHandle(System.Data.Common.DbConnection connection, int lockKey)
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

            await using var command = _connection.CreateCommand();
            command.CommandText = $"SELECT pg_advisory_unlock({_lockKey})";
            await command.ExecuteNonQueryAsync();
            _released = true;
        }
    }
}
