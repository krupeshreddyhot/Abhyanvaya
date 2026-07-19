using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal sealed class EnrollmentValidationPipelineExecutor
{
    private readonly IEnrollmentValidationRuleRegistry _ruleRegistry;

    public EnrollmentValidationPipelineExecutor(IEnrollmentValidationRuleRegistry ruleRegistry)
    {
        _ruleRegistry = ruleRegistry;
    }

    public async Task<IReadOnlyList<EnrollmentValidationRuleResult>> ExecuteAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var rules = _ruleRegistry.GetOrderedRules(context.Policy.Profile);
        var results = new List<EnrollmentValidationRuleResult>(rules.Count);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!rule.Enabled || !_ruleRegistry.IsRuleEnabled(rule.Name, context.Policy.Profile))
            {
                results.Add(new EnrollmentValidationRuleResult
                {
                    RuleName = rule.Name,
                    Passed = true,
                    Severity = ValidationRuleOutcome.Skipped,
                    Message = "Rule disabled by profile.",
                    ExecutionTime = TimeSpan.Zero,
                });
                continue;
            }

            if (rule.Name == EnrollmentValidationRuleIds.CorruptImage
                && context.AnalysisAccessor.FormatResult is { IsValid: false })
            {
                continue;
            }

            var enforcement = ResolveEnforcement(rule, context.Policy);
            var result = await rule.ExecuteAsync(context, cancellationToken);
            result = ApplyEnforcement(result, enforcement);
            results.Add(result);
        }

        return results;
    }

    private static ValidationRuleEnforcement ResolveEnforcement(
        IEnrollmentValidationRule rule,
        EnrollmentValidationPolicyDecision policy)
    {
        if (policy.SeverityOverrides?.TryGetValue(rule.Name, out var enforcement) == true)
        {
            return enforcement;
        }

        return rule.Enforcement;
    }

    private static EnrollmentValidationRuleResult ApplyEnforcement(
        EnrollmentValidationRuleResult result,
        ValidationRuleEnforcement enforcement)
    {
        if (result.Severity != ValidationRuleOutcome.Fail)
        {
            return result;
        }

        return enforcement switch
        {
            ValidationRuleEnforcement.Warning => result with
            {
                Passed = true,
                Severity = ValidationRuleOutcome.Warning,
                WarningCode = result.FailureCode,
                FailureCode = null,
                FailureCategory = null,
            },
            ValidationRuleEnforcement.Information => result with
            {
                Passed = true,
                Severity = ValidationRuleOutcome.Information,
                FailureCode = null,
                FailureCategory = null,
            },
            _ => result,
        };
    }

    public static (FailureCategory? Category, string? Reason, string? DiagnosticCode, bool Passed) ResolveEligibility(
        IReadOnlyList<EnrollmentValidationRuleResult> results)
    {
        foreach (var result in results)
        {
            if (result.Severity == ValidationRuleOutcome.Fail && result.FailureCategory is not null)
            {
                return (result.FailureCategory, result.Message, result.FailureCode, false);
            }
        }

        return (null, null, null, true);
    }
}
