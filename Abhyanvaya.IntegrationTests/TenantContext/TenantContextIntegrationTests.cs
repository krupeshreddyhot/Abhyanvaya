using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Abhyanvaya.IntegrationTests.TenantContext;

[Collection(nameof(PostgreSqlCollection))]
public sealed class TenantContextIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public TenantContextIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public void College_admin_resolution_uses_jwt_tenant()
    {
        var resolution = TenantContextResolution.FromContext(new TenantContextSnapshot
        {
            UserId = 10,
            Role = nameof(UserRole.Admin),
            TenantId = 1053,
            SelectedCollegeId = 1053,
            ContextType = ContextType.College,
            CreatedUtc = DateTime.UtcNow,
            IsGlobal = false,
            ContextSource = "JwtTenant",
        });

        resolution.IsResolved.Should().BeTrue();
        resolution.EffectiveTenantId.Should().Be(1053);
    }

    [Fact]
    public void Super_admin_without_context_is_not_resolved()
    {
        var resolution = TenantContextResolution.ContextRequired();
        resolution.IsResolved.Should().BeFalse();
        resolution.ErrorCode.Should().Be("ContextRequired");
    }
}
