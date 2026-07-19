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

        var service = new TenantContextService(
            user,
            store.Object,
            Mock.Of<ITenantContextCollegeCatalog>(),
            accessor.Object,
            Mock.Of<IApplicationDbContext>(),
            audit.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<TenantContextService>>());

        await service.ClearContextAsync();

        store.Verify(s => s.RemoveAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        accessor.Verify(a => a.Clear(), Times.Once);
        audit.Verify(
            a => a.RecordAsync(
                "TenantContext",
                "1",
                AuditAction.Custom,
                null,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ResolveForOperation_SuperAdminWithoutSelection_ReturnsContextRequired()
    {
        var service = CreateService(
            userId: 1,
            role: nameof(UserRole.SuperAdmin),
            tenantId: 0);

        var resolution = service.ResolveForOperation();

        Assert.False(resolution.IsResolved);
        Assert.Equal("ContextRequired", resolution.ErrorCode);
    }

    [Fact]
    public void ResolveForOperation_CollegeAdmin_UsesJwtTenant()
    {
        var service = CreateService(
            userId: 2,
            role: nameof(UserRole.Admin),
            tenantId: 99);

        var resolution = service.ResolveForOperation();

        Assert.True(resolution.IsResolved);
        Assert.Equal(99, resolution.EffectiveTenantId);
    }

    [Fact]
    public async Task SetCurrentContextAsync_NonSuperAdmin_ReturnsNotAllowed()
    {
        var service = CreateService(
            userId: 3,
            role: nameof(UserRole.Admin),
            tenantId: 5);

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
            userId: 1,
            role: nameof(UserRole.SuperAdmin),
            tenantId: 0,
            store: store.Object);

        var result = await service.ValidateContextAsync();

        Assert.False(result.IsValid);
        Assert.Equal("ContextRequired", result.ErrorCode);
    }

    private static TenantContextService CreateService(
        int userId,
        string role,
        int tenantId,
        ITenantContextStore? store = null) =>
        new(
            Mock.Of<ICurrentUserService>(u => u.UserId == userId && u.Role == role && u.TenantId == tenantId),
            store ?? Mock.Of<ITenantContextStore>(),
            Mock.Of<ITenantContextCollegeCatalog>(),
            Mock.Of<ITenantContextAccessor>(),
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IAuditService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<TenantContextService>>());
}
