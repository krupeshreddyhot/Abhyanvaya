namespace Abhyanvaya.Application.ProductionReadiness;

public enum StartupValidationSeverity
{
    Pass = 0,
    Warning = 1,
    Fail = 2,
}

public sealed record StartupValidationCheck
{
    public required string Name { get; init; }
    public required StartupValidationSeverity Severity { get; init; }
    public required string Detail { get; init; }
}

public sealed record StartupValidationReport
{
    public required IReadOnlyList<StartupValidationCheck> Checks { get; init; }
    public required StartupValidationSeverity OverallSeverity { get; init; }
    public required DateTime GeneratedUtc { get; init; }
}

public interface IEnrollmentConfigurationValidator
{
    Task<StartupValidationReport> ValidateAsync(CancellationToken cancellationToken = default);
}
