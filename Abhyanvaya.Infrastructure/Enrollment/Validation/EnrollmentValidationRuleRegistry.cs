using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal sealed class EnrollmentValidationRuleRegistry : IEnrollmentValidationRuleRegistry
{
    private readonly List<IEnrollmentValidationRule> _rules;

    public EnrollmentValidationRuleRegistry(IEnumerable<IEnrollmentValidationRule> rules)
    {
        _rules = rules.ToList();
    }

    public IReadOnlyList<IEnrollmentValidationRule> GetOrderedRules(ValidationProfileDefinition profile) =>
        _rules
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.Ordinal)
            .ToList();

    public bool IsRuleEnabled(string ruleName, ValidationProfileDefinition profile)
    {
        if (!profile.EnabledRules.TryGetValue(ruleName, out var enabled))
        {
            return true;
        }

        return enabled;
    }

    public void Register(IEnrollmentValidationRule rule) => _rules.Add(rule);
}
