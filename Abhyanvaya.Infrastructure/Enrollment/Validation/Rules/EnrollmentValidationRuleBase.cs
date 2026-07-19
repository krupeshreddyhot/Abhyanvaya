using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;

internal abstract class EnrollmentValidationRuleBase : IEnrollmentValidationRule
{
    public abstract string Name { get; }
    public abstract int Order { get; }
    public virtual bool Enabled => true;
    public virtual ValidationRuleEnforcement Enforcement => ValidationRuleEnforcement.Hard;

    public async Task<EnrollmentValidationRuleResult> ExecuteAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteCoreAsync(context, cancellationToken);
        stopwatch.Stop();

        return result with
        {
            RuleName = Name,
            ExecutionTime = stopwatch.Elapsed,
            Telemetry = new EnrollmentValidationRuleTelemetry
            {
                RuleName = Name,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            },
        };
    }

    protected abstract Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken);

    protected static EnrollmentValidationRuleResult Pass(
        string? message = null,
        double? measuredValue = null,
        double? thresholdValue = null) =>
        new()
        {
            RuleName = string.Empty,
            Passed = true,
            Severity = ValidationRuleOutcome.Pass,
            Message = message,
            MeasuredValue = measuredValue,
            ThresholdValue = thresholdValue,
            ExecutionTime = TimeSpan.Zero,
        };

    protected static EnrollmentValidationRuleResult Fail(
        string message,
        string failureCode,
        FailureCategory failureCategory,
        double? measuredValue = null,
        double? thresholdValue = null) =>
        new()
        {
            RuleName = string.Empty,
            Passed = false,
            Severity = ValidationRuleOutcome.Fail,
            Message = message,
            FailureCode = failureCode,
            FailureCategory = failureCategory,
            MeasuredValue = measuredValue,
            ThresholdValue = thresholdValue,
            ExecutionTime = TimeSpan.Zero,
        };

    protected static EnrollmentValidationRuleResult NotApplicable(string message) =>
        new()
        {
            RuleName = string.Empty,
            Passed = true,
            Severity = ValidationRuleOutcome.NotApplicable,
            Message = message,
            ExecutionTime = TimeSpan.Zero,
        };

    protected static EnrollmentValidationRuleResult Skipped(string message) =>
        new()
        {
            RuleName = string.Empty,
            Passed = true,
            Severity = ValidationRuleOutcome.Skipped,
            Message = message,
            ExecutionTime = TimeSpan.Zero,
        };

    protected static EnrollmentValidationRuleResult Warning(
        string message,
        string warningCode,
        double? measuredValue = null,
        double? thresholdValue = null) =>
        new()
        {
            RuleName = string.Empty,
            Passed = true,
            Severity = ValidationRuleOutcome.Warning,
            Message = message,
            WarningCode = warningCode,
            MeasuredValue = measuredValue,
            ThresholdValue = thresholdValue,
            ExecutionTime = TimeSpan.Zero,
        };

    protected static EnrollmentValidationRuleResult Information(string message) =>
        new()
        {
            RuleName = string.Empty,
            Passed = true,
            Severity = ValidationRuleOutcome.Information,
            Message = message,
            ExecutionTime = TimeSpan.Zero,
        };
}
