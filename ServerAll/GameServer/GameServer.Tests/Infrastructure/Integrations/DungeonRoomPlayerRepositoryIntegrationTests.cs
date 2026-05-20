using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.User;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class DungeonRoomPlayerRepositoryIntegrationTests(RepositoryTestFixture fixture)
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
        var room = await roomRepo.CreateAsync(host.UserId, "Test Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        long roomId = room.RoomId;
        long userId = playerUser.UserId;

        // Act
        var player = await repository.CreateAsync(roomId, userId);

        // Assert
        Assert.NotNull(player);
        Assert.Equal(roomId, player.RoomId);
        Assert.Equal(userId, player.UserId);

        // Check DB
        var dbPlayer = await context.DungeonRoomPlayers.FindAsync(roomId, userId);
        Assert.NotNull(dbPlayer);

        // Check Redis
        var redisKey = RedisKeys.DungeonRoomPlayer(roomId, userId);
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(redisKey);
        Assert.NotEmpty(entries);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomPlayerByRoom(roomId), userId);
        Assert.True(isMember);

        var mappingRoomId = await _fixture.RedisConnection.GetDatabase().StringGetAsync(RedisKeys.DungeonRoomPlayerByUser(userId));
        Assert.Equal(roomId.ToString(), mappingRoomId.ToString());
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
        var room = await roomRepo.CreateAsync(host.UserId, "Hit Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(room.RoomId, playerUser.UserId);

        // Act
        context.DungeonRoomPlayers.Remove(player);
        await context.SaveChangesAsync();

        var players = await repository.GetPlayersByRoomIdAsync(room.RoomId);

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
        var room = await roomRepo.CreateAsync(host.UserId, "Miss Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(room.RoomId, playerUser.UserId);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, playerUser.UserId));
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoomPlayerByRoom(room.RoomId));

        var players = await repository.GetPlayersByRoomIdAsync(room.RoomId);

        // Assert
        Assert.Single(players);
        Assert.Equal(playerUser.UserId, players[0].UserId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, playerUser.UserId));
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
        var room = await roomRepo.CreateAsync(host.UserId, "Remove Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        await repository.CreateAsync(room.RoomId, playerUser.UserId);

        // Act
        await repository.RemoveAsync(room.RoomId, playerUser.UserId);

        // Assert
        var dbPlayer = await context.DungeonRoomPlayers.FindAsync(room.RoomId, playerUser.UserId);
        Assert.Null(dbPlayer);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, playerUser.UserId));
        Assert.False(exists);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomPlayerByRoom(room.RoomId), playerUser.UserId);
        Assert.False(isMember);
    }

    [Fact]
    public async Task RemoveByRoomId_ShouldRemoveAllFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var player1 = await userRepo.CreateAsync();
        var player2 = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "RemoveAll Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        await repository.CreateAsync(room.RoomId, player1.UserId);
        await repository.CreateAsync(room.RoomId, player2.UserId);

        // Act
        await repository.RemoveByRoomIdAsync(room.RoomId);

        // Assert
        var dbPlayers = await context.DungeonRoomPlayers.Where(p => p.RoomId == room.RoomId).ToListAsync();
        Assert.Empty(dbPlayers);

        var exists1 = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, player1.UserId));
        var exists2 = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, player2.UserId));
        Assert.False(exists1);
        Assert.False(exists2);
    }

    [Fact]
    public async Task GetByUserId_사용자의_현재_방을_반환한다()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var playerUser = await userRepo.CreateAsync();

        var roomRepo = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await roomRepo.CreateAsync(host.UserId, "GetByUser Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        await repository.CreateAsync(room.RoomId, playerUser.UserId);

        // Act
        var player = await repository.GetByUserIdAsync(playerUser.UserId);

        // Assert
        Assert.NotNull(player);
        Assert.Equal(room.RoomId, player!.RoomId);
        Assert.Equal(playerUser.UserId, player.UserId);
    }

    [Fact]
    public async Task GetByUserId_방에_없는_사용자는_null_반환한다()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);

        // Act
        var player = await repository.GetByUserIdAsync(user.UserId);

        // Assert
        Assert.Null(player);
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
        var room = await roomRepo.CreateAsync(host.UserId, "TTL Room");

        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(room.RoomId, playerUser.UserId);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.DungeonRoomPlayer(room.RoomId, playerUser.UserId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
