using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

public class RepositoryTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = null!;
    public RedisContainer RedisContainer { get; private set; } = null!;
    
    public string DbConnectionString => PostgreSqlContainer.GetConnectionString();
    public string RedisConnectionString => RedisContainer.GetConnectionString();

    private IConnectionMultiplexer? _redisConnection;
    public IConnectionMultiplexer RedisConnection => _redisConnection ?? throw new InvalidOperationException("Redis not initialized");

    public GameServerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameServerDbContext>()
            .UseNpgsql(DbConnectionString)
            .Options;
        return new GameServerDbContext(options);
    }

    public async Task InitializeAsync()
    {
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("gamedb")
            .WithUsername("gameuser")
            .WithPassword("gamepass123")
            .Build();

        RedisContainer = new RedisBuilder()
            .Build();

        await Task.WhenAll(PostgreSqlContainer.StartAsync(), RedisContainer.StartAsync());

        _redisConnection = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);

        // Run migrations/EnsureCreated
        using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_redisConnection != null)
        {
            await _redisConnection.DisposeAsync();
        }

        await Task.WhenAll(PostgreSqlContainer.StopAsync(), RedisContainer.StopAsync());
    }
}

[CollectionDefinition("RepositoryIntegrationTests")]
public class RepositoryCollection : ICollectionFixture<RepositoryTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
