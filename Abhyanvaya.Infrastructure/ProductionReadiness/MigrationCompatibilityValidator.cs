using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Application.ProductionReadiness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ProductionReadiness;

public sealed class MigrationCompatibilityValidator : IMigrationCompatibilityValidator
{
    private static readonly (string Table, string[] Columns)[] RequiredEnrollmentObjects =
    [
        ("StudentEnrollmentBatch", ["ConfigurationSnapshotJson", "CorrelationId", "PhotoProviderName", "PipelineVersion", "Priority"]),
        ("StudentEnrollmentProgressSnapshot", []),
        ("ArtifactStorageManifest", []),
        ("ArtifactRegistryEntry", []),
    ];

    private readonly ApplicationDbContext _context;
    private readonly ILogger<MigrationCompatibilityValidator> _logger;

    public MigrationCompatibilityValidator(ApplicationDbContext context, ILogger<MigrationCompatibilityValidator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MigrationCompatibilityReport> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<MigrationValidationIssue>();

        foreach (var (table, columns) in RequiredEnrollmentObjects)
        {
            if (!await TableExistsAsync(table, cancellationToken))
            {
                issues.Add(new MigrationValidationIssue
                {
                    ObjectType = "Table",
                    ObjectName = table,
                    Detail = "Required enrollment table is missing.",
                });
                continue;
            }

            foreach (var column in columns)
            {
                if (!await ColumnExistsAsync(table, column, cancellationToken))
                {
                    issues.Add(new MigrationValidationIssue
                    {
                        ObjectType = "Column",
                        ObjectName = $"{table}.{column}",
                        Detail = "Required enrollment column is missing.",
                    });
                }
            }
        }

        var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var schemaVersion = appliedMigrations.LastOrDefault() ?? "none";
        var mode = DetermineMode(appliedMigrations, issues);

        _logger.LogInformation(
            "Migration compatibility validated mode={Mode} issueCount={IssueCount} schemaVersion={SchemaVersion}",
            mode,
            issues.Count,
            schemaVersion);

        return new MigrationCompatibilityReport
        {
            Mode = mode,
            Issues = issues,
            IsCompatible = issues.Count == 0,
            SchemaVersion = schemaVersion,
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    private static MigrationCompatibilityMode DetermineMode(IEnumerable<string> appliedMigrations, IReadOnlyList<MigrationValidationIssue> issues)
    {
        var migrations = appliedMigrations.ToList();
        if (migrations.Count == 0)
        {
            return MigrationCompatibilityMode.FreshInstall;
        }

        if (issues.Count == 0)
        {
            return MigrationCompatibilityMode.Upgrade;
        }

        return issues.Any(i => i.ObjectType == "Table")
            ? MigrationCompatibilityMode.LegacyDatabase
            : MigrationCompatibilityMode.PartialSchema;
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = @tableName)
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool exists && exists;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName)
                """;
            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "tableName";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);
            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "columnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool exists && exists;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }
}
