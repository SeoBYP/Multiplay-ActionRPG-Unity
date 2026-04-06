using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class GameSessionPlayerRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Create_ShouldSaveToDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        long sessionId = 5001;
        long userId = 6001;

        // Act
        var player = await repository.CreateAsync(sessionId, userId);

        // Assert
        Assert.NotNull(player);
        Assert.Equal(sessionId, player.GameSessionId);
        Assert.Equal(userId, player.UserId);

        // Check DB
        var dbPlayer = await context.GameSessionPlayers.FindAsync(sessionId, userId);
        Assert.NotNull(dbPlayer);

        // Check Redis
        var redisKey = RedisKeys.GameSessionPlayer(sessionId, userId);
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(redisKey);
        Assert.NotEmpty(entries);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.GameSessionPlayerBySession(sessionId), userId);
        Assert.True(isMember);

        var mappingSessionId = await _fixture.RedisConnection.GetDatabase().StringGetAsync(RedisKeys.GameSessionPlayerByUser(userId));
        Assert.Equal(sessionId.ToString(), mappingSessionId.ToString());
    }

    [Fact]
    public async Task Read_HIT_ShouldReturnFromCacheWithoutDbQuery()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(5002, 6002);

        // Act
        context.GameSessionPlayers.Remove(player);
        await context.SaveChangesAsync();

        var players = await repository.GetPlayersByGameSessionIdAsync(5002);

        // Assert
        Assert.Single(players);
        Assert.Equal(6002, players[0].UserId);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(5003, 6003);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSessionPlayer(5003, 6003));
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSessionPlayerBySession(5003));

        var players = await repository.GetPlayersByGameSessionIdAsync(5003);

        // Assert
        Assert.Single(players);
        Assert.Equal(6003, players[0].UserId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSessionPlayer(5003, 6003));
        Assert.True(exists);
    }

    [Fact]
    public async Task Remove_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        await repository.CreateAsync(5004, 6004);

        // Act
        await repository.RemoveAsync(5004, 6004);

        // Assert
        var dbPlayer = await context.GameSessionPlayers.FindAsync(5004L, 6004L);
        Assert.Null(dbPlayer);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSessionPlayer(5004, 6004));
        Assert.False(exists);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.GameSessionPlayerBySession(5004), 6004);
        Assert.False(isMember);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(5005, 6005);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.GameSessionPlayer(5005, 6005));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
