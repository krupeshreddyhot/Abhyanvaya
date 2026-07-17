using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Discovers, registers, and orders validation rules for a profile (AI20.PHASE2.1.4A).
/// </summary>
public interface IEnrollmentValidationRuleRegistry
{
    IReadOnlyList<IEnrollmentValidationRule> GetOrderedRules(ValidationProfileDefinition profile);

    bool IsRuleEnabled(string ruleName, ValidationProfileDefinition profile);

    void Register(IEnrollmentValidationRule rule);
}
