using GameServer.Domain.Entities.GameSession;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class GameSessionRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Create_ShouldSaveToDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        long roomId = 4001;
        string socketIp = "127.0.0.1";
        int socketPort = 7777;

        // Act
        var session = await repository.CreateAsync(roomId, socketIp, socketPort);

        // Assert
        Assert.NotNull(session);
        Assert.True(session.GameSessionId > 0);

        // Check DB
        var dbSession = await context.GameSessions.FindAsync(session.GameSessionId);
        Assert.NotNull(dbSession);
        Assert.Equal(roomId, dbSession.RoomId);

        // Check Redis
        var redisKey = RedisKeys.GameSession(session.GameSessionId);
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(redisKey);
        Assert.NotEmpty(entries);
        
        var mappingSessionId = await _fixture.RedisConnection.GetDatabase().StringGetAsync(RedisKeys.GameSessionByRoom(roomId));
        Assert.Equal(session.GameSessionId.ToString(), mappingSessionId.ToString());
    }

    [Fact]
    public async Task Read_HIT_ShouldReturnFromCacheWithoutDbQuery()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(4002, "127.0.0.1", 7778);

        // Act
        context.GameSessions.Remove(session);
        await context.SaveChangesAsync();

        var cachedSession = await repository.GetAsync(session.GameSessionId);

        // Assert
        Assert.NotNull(cachedSession);
        Assert.Equal(4002, cachedSession.RoomId);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(4003, "127.0.0.1", 7779);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSession(session.GameSessionId));

        var dbSession = await repository.GetAsync(session.GameSessionId);

        // Assert
        Assert.NotNull(dbSession);
        Assert.Equal(4003, dbSession.RoomId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSession(session.GameSessionId));
        Assert.True(exists);
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndInvalidateCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(4004, "127.0.0.1", 7780);

        // Act
        session.End();
        await repository.UpdateAsync(session);

        // Assert
        var dbSession = await context.GameSessions.AsNoTracking().FirstOrDefaultAsync(s => s.GameSessionId == session.GameSessionId);
        Assert.Equal(GameSessionStatus.Ended, dbSession!.Status);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSession(session.GameSessionId));
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(4005, "127.0.0.1", 7781);

        // Act
        await repository.RemoveAsync(session.GameSessionId);

        // Assert
        var dbSession = await context.GameSessions.FindAsync(session.GameSessionId);
        Assert.Null(dbSession);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSession(session.GameSessionId));
        Assert.False(exists);
        
        var mappingExists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSessionByRoom(session.RoomId));
        Assert.False(mappingExists);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(4006, "127.0.0.1", 7782);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.GameSession(session.GameSessionId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
