using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.TenantContext;
using Abhyanvaya.Infrastructure.TenantContext.ContextPersistence;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.TenantContext;

public sealed class ContextPersistenceProviderTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsValue()
    {
        var cache = new Mock<ICacheService>();
        TenantContextSnapshot? stored = null;
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<TenantContextSnapshot>(), It.IsAny<TimeSpan?>()))
            .Callback<string, TenantContextSnapshot, TimeSpan?>((_, value, _) => stored = value)
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.GetAsync<TenantContextSnapshot>("tenant-context:v1:9"))
            .ReturnsAsync(() => stored);

        var provider = new DistributedCacheContextPersistenceProvider(
            cache.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DistributedCacheContextPersistenceProvider>>());

        var snapshot = new TenantContextSnapshot
        {
            UserId = 9,
            Role = nameof(UserRole.SuperAdmin),
            TenantId = 1,
            ContextType = ContextType.College,
            CreatedUtc = DateTime.UtcNow,
            IsGlobal = false,
            ContextSource = "Test",
            SelectedCollegeId = 1,
        };

        await provider.SaveAsync("tenant-context:v1:9", snapshot, TimeSpan.FromHours(8));
        var loaded = await provider.LoadAsync<TenantContextSnapshot>("tenant-context:v1:9");

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SelectedCollegeId);
        Assert.Equal("DistributedCache", provider.ProviderName);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenMissing()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<object>("missing")).ReturnsAsync((object?)null);

        var provider = new DistributedCacheContextPersistenceProvider(
            cache.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DistributedCacheContextPersistenceProvider>>());

        Assert.False(await provider.ExistsAsync("missing"));
    }
}

public sealed class RecentContextServiceTests
{
    [Fact]
    public async Task RecordCollegeSelection_MovesDuplicateToTop_AndCapsAtMax()
    {
        var repo = new Mock<IRecentContextRepository>();
        var saved = new List<RecentCollegeEntry>();
        repo.Setup(r => r.GetRecentCollegesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved.ToList());
        repo.Setup(r => r.SaveRecentCollegesAsync(1, It.IsAny<IReadOnlyList<RecentCollegeEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<RecentCollegeEntry>, CancellationToken>((_, entries, _) =>
            {
                saved.Clear();
                saved.AddRange(entries);
            })
            .Returns(Task.CompletedTask);

        var service = new RecentContextService(
            repo.Object,
            Microsoft.Extensions.Options.Options.Create(new ContextPlatformOptions { RecentCollegesMax = 2 }));

        var college = new AvailableCollegeDto
        {
            Id = 1,
            TenantId = 1,
            Name = "A",
            Code = "A1",
            Status = "Active",
            AiEnabled = true,
        };

        await service.RecordCollegeSelectionAsync(1, college);
        await service.RecordCollegeSelectionAsync(1, college with { Id = 2, Code = "B1", Name = "B" });
        await service.RecordCollegeSelectionAsync(1, college);

        Assert.Equal(2, saved.Count);
        Assert.Equal(1, saved[0].CollegeId);
    }
}

public sealed class ContextExpirationServiceTests
{
    [Fact]
    public void IsExpired_ReturnsTrueAfterTimeout()
    {
        var service = new ContextExpirationService(
            Microsoft.Extensions.Options.Options.Create(new ContextPlatformOptions { ExpirationHours = 8 }));

        var snapshot = new TenantContextSnapshot
        {
            UserId = 1,
            Role = nameof(UserRole.SuperAdmin),
            TenantId = 1,
            ContextType = ContextType.College,
            CreatedUtc = DateTime.UtcNow.AddHours(-9),
            ExpiresUtc = DateTime.UtcNow.AddMinutes(-1),
            IsGlobal = false,
            ContextSource = "Test",
            SelectedCollegeId = 1,
        };

        Assert.True(service.IsExpired(snapshot));
    }
}

public sealed class ContextEventPublisherTests
{
    [Fact]
    public async Task PublishContextChanged_InvokesSubscriber()
    {
        var publisher = new InMemoryContextEventPublisher();
        var called = false;
        publisher.OnContextChanged(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await publisher.PublishContextChangedAsync(new TenantContextSnapshot
        {
            UserId = 1,
            Role = nameof(UserRole.SuperAdmin),
            TenantId = 1,
            ContextType = ContextType.College,
            CreatedUtc = DateTime.UtcNow,
            IsGlobal = false,
            ContextSource = "Test",
        });

        Assert.True(called);
    }
}
