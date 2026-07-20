using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal sealed class DefaultEnrollmentValidationPolicy : IEnrollmentValidationPolicy
{
    private readonly EnrollmentValidationOptions _options;
    private readonly InsightFaceOptions _insightFaceOptions;

    public DefaultEnrollmentValidationPolicy(
        IOptions<EnrollmentValidationOptions> options,
        IOptions<InsightFaceOptions> insightFaceOptions)
    {
        _options = options.Value;
        _insightFaceOptions = insightFaceOptions.Value;
    }

    public Task<EnrollmentValidationPolicyDecision> ResolveAsync(
        EnrollmentValidationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileKind = request.RequestedProfile ?? _options.DefaultProfile;
        var profile = ValidationProfiles.Resolve(profileKind);
        var baseline = EnrollmentValidationThresholdMapper.FromOptions(_options, _insightFaceOptions);
        var thresholds = EnrollmentValidationThresholdMapper.ApplyProfile(baseline, profile);

        return Task.FromResult(new EnrollmentValidationPolicyDecision
        {
            Profile = profile,
            Thresholds = thresholds,
            RuleEnableOverrides = profile.EnabledRules,
            SeverityOverrides = profile.SeverityOverrides,
        });
    }
}
