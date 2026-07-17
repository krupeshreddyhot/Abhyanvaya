namespace Abhyanvaya.Application.Enrollment.Validation;

public sealed record EnrollmentValidationRuleResult
{
    public required string RuleName { get; init; }
    public required bool Passed { get; init; }
    public required ValidationRuleOutcome Severity { get; init; }
    public string? FailureCode { get; init; }
    public string? WarningCode { get; init; }
    public string? Message { get; init; }
    public required TimeSpan ExecutionTime { get; init; }
    public EnrollmentValidationRuleTelemetry? Telemetry { get; init; }
    public double? MeasuredValue { get; init; }
    public double? ThresholdValue { get; init; }
    public Abhyanvaya.Domain.Enums.FailureCategory? FailureCategory { get; init; }
}

public sealed record EnrollmentValidationRuleTelemetry
{
    public required string RuleName { get; init; }
    public required long ElapsedMilliseconds { get; init; }
}

public sealed record EnrollmentValidationPipelineResult
{
    public required IReadOnlyList<EnrollmentValidationRuleResult> RuleResults { get; init; }
    public required ValidationReport Report { get; init; }
    public required EnrollmentValidationArtifact Artifact { get; init; }
    public Abhyanvaya.Domain.Enums.FailureCategory? FailureCategory { get; init; }
    public string? FailureReason { get; init; }
    public string? DiagnosticCode { get; init; }
    public required bool ValidationPassed { get; init; }
    public EnrollmentValidationTelemetry? Telemetry { get; init; }
}
