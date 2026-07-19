using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Pluggable enrollment validation rule executed by the validation pipeline (AI20.PHASE2.1.4A).
/// </summary>
public interface IEnrollmentValidationRule
{
    string Name { get; }

    int Order { get; }

    bool Enabled { get; }

    ValidationRuleEnforcement Enforcement { get; }

    Task<EnrollmentValidationRuleResult> ExecuteAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken = default);
}
