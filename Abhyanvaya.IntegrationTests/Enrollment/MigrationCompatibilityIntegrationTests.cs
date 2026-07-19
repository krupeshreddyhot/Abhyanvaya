using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Infrastructure.ProductionReadiness;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Abhyanvaya.IntegrationTests.Enrollment;

[Collection(nameof(PostgreSqlCollection))]
public sealed class MigrationCompatibilityIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public MigrationCompatibilityIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ValidateAsync_reports_compatible_schema_after_migrations()
    {
        await using var context = _fixture.CreateDbContext();
        var validator = new MigrationCompatibilityValidator(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<MigrationCompatibilityValidator>.Instance);

        var report = await validator.ValidateAsync();

        report.IsCompatible.Should().BeTrue();
        report.Mode.Should().BeOneOf(MigrationCompatibilityMode.Upgrade, MigrationCompatibilityMode.FreshInstall);
    }
}
