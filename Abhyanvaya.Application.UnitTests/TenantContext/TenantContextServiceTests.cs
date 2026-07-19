using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.TenantContext;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.TenantContext;

public sealed class TenantContextServiceTests
{
    [Fact]
    public async Task ClearContextAsync_SuperAdmin_RemovesStoredContext()
    {
        var user = Mock.Of<ICurrentUserService>(u =>
            u.UserId == 1 &&
            u.Role == nameof(UserRole.SuperAdmin) &&
            u.TenantId == 0);

        var store = new Mock<ITenantContextStore>();
        var accessor = new Mock<ITenantContextAccessor>();
        var audit = new Mock<IAuditService>();
        var events = new Mock<IContextEventPublisher>();

        var service = CreateService(user, store.Object, accessor.Object, audit.Object, events.Object);

        await service.ClearContextAsync();

        store.Verify(s => s.RemoveAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        accessor.Verify(a => a.Clear(), Times.Once);
        events.Verify(e => e.PublishContextClearedAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResolveForOperation_SuperAdminWithoutSelection_ReturnsContextRequired()
    {
        var service = CreateService(
            Mock.Of<ICurrentUserService>(u => u.UserId == 1 && u.Role == nameof(UserRole.SuperAdmin) && u.TenantId == 0));

        var resolution = service.ResolveForOperation();

        Assert.False(resolution.IsResolved);
        Assert.Equal("ContextRequired", resolution.ErrorCode);
    }

    [Fact]
    public void ResolveForOperation_CollegeAdmin_UsesJwtTenant()
    {
        var service = CreateService(
            Mock.Of<ICurrentUserService>(u => u.UserId == 2 && u.Role == nameof(UserRole.Admin) && u.TenantId == 99));

        var resolution = service.ResolveForOperation();

        Assert.True(resolution.IsResolved);
        Assert.Equal(99, resolution.EffectiveTenantId);
    }

    [Fact]
    public async Task SetCurrentContextAsync_NonSuperAdmin_ReturnsNotAllowed()
    {
        var service = CreateService(
            Mock.Of<ICurrentUserService>(u => u.UserId == 3 && u.Role == nameof(UserRole.Admin) && u.TenantId == 5));

        var result = await service.SetCurrentContextAsync(10);

        Assert.False(result.IsValid);
        Assert.Equal("NotAllowed", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateContextAsync_GlobalSuperAdmin_ReturnsContextRequired()
    {
        var store = new Mock<ITenantContextStore>();
        store.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantContextSnapshot
            {
                UserId = 1,
                Role = nameof(UserRole.SuperAdmin),
                TenantId = 0,
                ContextType = ContextType.Global,
                CreatedUtc = DateTime.UtcNow,
                IsGlobal = true,
                ContextSource = "Session",
            });

        var service = CreateService(
            Mock.Of<ICurrentUserService>(u => u.UserId == 1 && u.Role == nameof(UserRole.SuperAdmin) && u.TenantId == 0),
            store.Object);

        var result = await service.ValidateContextAsync();

        Assert.False(result.IsValid);
        Assert.Equal("ContextRequired", result.ErrorCode);
    }

    private static TenantContextService CreateService(
        ICurrentUserService user,
        ITenantContextStore? store = null,
        ITenantContextAccessor? accessor = null,
        IAuditService? audit = null,
        IContextEventPublisher? events = null) =>
        new(
            user,
            store ?? Mock.Of<ITenantContextStore>(),
            Mock.Of<ITenantContextCollegeCatalog>(),
            accessor ?? Mock.Of<ITenantContextAccessor>(),
            Mock.Of<IApplicationDbContext>(),
            audit ?? Mock.Of<IAuditService>(),
            Mock.Of<IRecentContextService>(),
            new ContextExpirationService(Microsoft.Extensions.Options.Options.Create(new ContextPlatformOptions())),
            events ?? Mock.Of<IContextEventPublisher>(),
            new ContextOperationalMetricsCollector(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<TenantContextService>>());
}
