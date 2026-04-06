using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Infrastructure.Domains.User;
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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Session Player Room");

        var sessionRepo = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await sessionRepo.CreateAsync(room.RoomId, "127.0.0.1", 7777);

        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        long sessionId = session.GameSessionId;
        long userId = playerUser.UserId;

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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Hit Session Player Room");

        var sessionRepo = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await sessionRepo.CreateAsync(room.RoomId, "127.0.0.1", 7778);

        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(session.GameSessionId, playerUser.UserId);

        // Act
        context.GameSessionPlayers.Remove(player);
        await context.SaveChangesAsync();

        var players = await repository.GetPlayersByGameSessionIdAsync(session.GameSessionId);

        // Assert
        Assert.Single(players);
        Assert.Equal(playerUser.UserId, players[0].UserId);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Miss Session Player Room");

        var sessionRepo = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await sessionRepo.CreateAsync(room.RoomId, "127.0.0.1", 7779);

        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(session.GameSessionId, playerUser.UserId);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSessionPlayer(session.GameSessionId, playerUser.UserId));
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSessionPlayerBySession(session.GameSessionId));

        var players = await repository.GetPlayersByGameSessionIdAsync(session.GameSessionId);

        // Assert
        Assert.Single(players);
        Assert.Equal(playerUser.UserId, players[0].UserId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSessionPlayer(session.GameSessionId, playerUser.UserId));
        Assert.True(exists);
    }

    [Fact]
    public async Task Remove_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Remove Session Player Room");

        var sessionRepo = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await sessionRepo.CreateAsync(room.RoomId, "127.0.0.1", 7780);

        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        await repository.CreateAsync(session.GameSessionId, playerUser.UserId);

        // Act
        await repository.RemoveAsync(session.GameSessionId, playerUser.UserId);

        // Assert
        var dbPlayer = await context.GameSessionPlayers.FindAsync(session.GameSessionId, playerUser.UserId);
        Assert.Null(dbPlayer);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSessionPlayer(session.GameSessionId, playerUser.UserId));
        Assert.False(exists);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.GameSessionPlayerBySession(session.GameSessionId), playerUser.UserId);
        Assert.False(isMember);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "TTL Session Player Room");

        var sessionRepo = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await sessionRepo.CreateAsync(room.RoomId, "127.0.0.1", 7781);

        var repository = new GameSessionPlayerRepository(_fixture.RedisConnection, context, NullLogger<GameSessionPlayerRepository>.Instance);
        var player = await repository.CreateAsync(session.GameSessionId, playerUser.UserId);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.GameSessionPlayer(session.GameSessionId, playerUser.UserId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
