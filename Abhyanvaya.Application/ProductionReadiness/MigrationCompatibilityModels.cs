namespace Abhyanvaya.Application.ProductionReadiness;

public enum MigrationCompatibilityMode
{
    FreshInstall = 0,
    Upgrade = 1,
    PartialSchema = 2,
    LegacyDatabase = 3,
}

public sealed record MigrationValidationIssue
{
    public required string ObjectType { get; init; }
    public required string ObjectName { get; init; }
    public required string Detail { get; init; }
}

public sealed record MigrationCompatibilityReport
{
    public required MigrationCompatibilityMode Mode { get; init; }
    public required IReadOnlyList<MigrationValidationIssue> Issues { get; init; }
    public required bool IsCompatible { get; init; }
    public required string SchemaVersion { get; init; }
    public required DateTime GeneratedUtc { get; init; }
}

public interface IMigrationCompatibilityValidator
{
    Task<MigrationCompatibilityReport> ValidateAsync(CancellationToken cancellationToken = default);
}
