using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Abhyanvaya.Infrastructure.Persistence;

/// <summary>
/// Executes PostgreSQL DML with RETURNING on a dedicated connection so EF Core's shared
/// <see cref="Microsoft.EntityFrameworkCore.Storage.RelationalConnection"/> is never mutated.
/// </summary>
internal static class PostgreSqlReturningExecutor
{
    public static async Task<Guid?> ExecuteReturningGuidAsync(
        DatabaseFacade database,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        var connectionString = database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            return null;
        }

        return result switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => throw new InvalidOperationException($"Unexpected RETURNING type: {result.GetType().Name}"),
        };
    }

    private static NpgsqlParameter CloneParameter(NpgsqlParameter source) =>
        new(source.ParameterName, source.NpgsqlDbType)
        {
            Value = source.Value ?? DBNull.Value,
        };
}
