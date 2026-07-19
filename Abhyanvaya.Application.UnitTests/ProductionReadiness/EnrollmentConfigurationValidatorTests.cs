using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Infrastructure.ProductionReadiness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ProductionReadiness;

public sealed class EnrollmentConfigurationValidatorTests
{
    [Fact]
    public async Task ValidateAsync_NonStrictProduction_DowngradesEnrollmentFailuresToWarnings()
    {
        var configuration = BuildConfiguration(strict: false);
        var validator = CreateValidator(configuration, production: true);

        var report = await validator.ValidateAsync();

        Assert.Equal(StartupValidationSeverity.Warning, report.OverallSeverity);
        Assert.Contains(report.Checks, c => c.Name == "CloudflareR2" && c.Severity == StartupValidationSeverity.Warning);
        Assert.Contains(report.Checks, c => c.Name == "ExamBranch" && c.Severity == StartupValidationSeverity.Warning);
    }

    [Fact]
    public async Task ValidateAsync_StrictProduction_FailsWhenEnrollmentConfigMissing()
    {
        var configuration = BuildConfiguration(strict: true);
        var validator = CreateValidator(configuration, production: true);

        var report = await validator.ValidateAsync();

        Assert.Equal(StartupValidationSeverity.Fail, report.OverallSeverity);
        Assert.Contains(report.Checks, c => c.Name == "CloudflareR2" && c.Severity == StartupValidationSeverity.Fail);
    }

    private static IConfiguration BuildConfiguration(bool strict) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-minimum-length",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["EnrollmentStartupValidation:Strict"] = strict.ToString(),
                ["ArtifactStorage:Provider"] = "r2",
            })
            .Build();

    private static EnrollmentConfigurationValidator CreateValidator(IConfiguration configuration, bool production)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(production ? Environments.Production : Environments.Development);

        return new EnrollmentConfigurationValidator(
            configuration,
            environment.Object,
            Options.Create(new ArtifactStorageOptions { Provider = "r2" }),
            NullLogger<EnrollmentConfigurationValidator>.Instance);
    }
}
