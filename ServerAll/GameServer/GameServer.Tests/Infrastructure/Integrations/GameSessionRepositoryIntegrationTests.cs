using GameServer.Domain.Entities.GameSession;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Infrastructure.Domains.User;
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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        long roomId = room.RoomId;
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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Hit Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(room.RoomId, "127.0.0.1", 7778);

        // Act
        context.GameSessions.Remove(session);
        await context.SaveChangesAsync();

        var cachedSession = await repository.GetAsync(session.GameSessionId);

        // Assert
        Assert.NotNull(cachedSession);
        Assert.Equal(room.RoomId, cachedSession.RoomId);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Miss Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(room.RoomId, "127.0.0.1", 7779);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.GameSession(session.GameSessionId));

        var dbSession = await repository.GetAsync(session.GameSessionId);

        // Assert
        Assert.NotNull(dbSession);
        Assert.Equal(room.RoomId, dbSession.RoomId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSession(session.GameSessionId));
        Assert.True(exists);
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndInvalidateCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Update Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(room.RoomId, "127.0.0.1", 7780);

        // Act
        session.End();
        await repository.UpdateAsync(session);

        // Assert
        // DB 확인 (새 context 사용)
        using var assertContext = _fixture.CreateDbContext();
        var dbSession = await assertContext.GameSessions.AsNoTracking().FirstOrDefaultAsync(s => s.GameSessionId == session.GameSessionId);
        Assert.Equal(GameSessionStatus.Ended, dbSession!.Status);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.GameSession(session.GameSessionId));
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "Delete Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(room.RoomId, "127.0.0.1", 7781);

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
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "TTL Session Room");

        var repository = new GameSessionRepository(_fixture.RedisConnection, context, NullLogger<GameSessionRepository>.Instance);
        var session = await repository.CreateAsync(room.RoomId, "127.0.0.1", 7782);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.GameSession(session.GameSessionId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
