using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Validation;

internal static class EnrollmentValidationTestFactory
{
    internal static IEnrollmentValidationRuleRegistry CreateRegistry()
    {
        IReadOnlyList<IEnrollmentValidationRule> rules =
        [
            new ImageFormatRule(),
            new CorruptImageRule(),
            new MinimumResolutionRule(),
            new MaximumResolutionRule(),
            new ExactlyOneFaceRule(),
            new FaceConfidenceRule(),
            new MinimumFaceCropResolutionRule(),
            new FaceCoverageRule(),
            new BlurRule(),
            new PoseRule(),
            new BrightnessRule(),
            new ContrastRule(),
            ..FutureValidationRuleFactory.CreateRules(),
        ];

        return new EnrollmentValidationRuleRegistry(rules);
    }
}
