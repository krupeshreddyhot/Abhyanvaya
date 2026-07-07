using Abhyanvaya.Application;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.DomainEvents;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Abhyanvaya.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL database for attendance integration tests.
/// Uses <c>ABHYANVAYA_TEST_CONNECTION</c>, Testcontainers when Docker is available, or local PostgreSQL.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("ABHYANVAYA_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            ConnectionString = configured;
        }
        else
        {
            try
            {
                _container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .Build();

                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
            }
            catch (Exception)
            {
                ConnectionString =
                    "Host=localhost;Port=5432;Database=abhyanvaya_integration_test;Username=postgres;Password=0127@Rupesh";
            }
        }

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    public ApplicationDbContext CreateDbContext(ICurrentUserService? currentUser = null) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options,
            currentUser,
            logger: null);

    public ServiceProvider CreateServiceProvider(TestCurrentUserService currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(ConnectionString));
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICurrentUserService>(_ => currentUser);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(nameof(PostgreSqlCollection))]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
