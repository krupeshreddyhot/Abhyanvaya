using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;

internal sealed class FuturePlaceholderValidationRule : EnrollmentValidationRuleBase
{
    private readonly string _ruleId;
    private readonly string _description;
    private readonly int _order;

    public FuturePlaceholderValidationRule(string ruleId, string description, int order)
    {
        _ruleId = ruleId;
        _description = description;
        _order = order;
    }

    public override string Name => _ruleId;
    public override int Order => _order;
    public override bool Enabled => false;

    protected override Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Skipped($"{_description} is reserved for a future release."));
}

internal static class FutureValidationRuleFactory
{
    internal static IReadOnlyList<IEnrollmentValidationRule> CreateRules() =>
    [
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.Liveness, "Liveness detection", 1000),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.MaskDetection, "Mask detection", 1010),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.EyeOpenness, "Eye openness", 1020),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.SpoofDetection, "Spoof detection", 1030),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.Occlusion, "Occlusion detection", 1040),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.Sunglasses, "Sunglasses detection", 1050),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.Smile, "Smile detection", 1060),
        new FuturePlaceholderValidationRule(EnrollmentValidationRuleIds.Expression, "Expression detection", 1070),
    ];
}
