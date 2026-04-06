using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
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
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        long roomId = 2001;
        long userId = 3001;

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
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(2002, 3002);

        // Act
        context.DungeonRoomPlayers.Remove(player);
        await context.SaveChangesAsync();

        var players = await repository.GetPlayersByRoomIdAsync(2002);

        // Assert
        Assert.Single(players);
        Assert.Equal(3002, players[0].UserId);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(2003, 3003);

        // Act
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoomPlayer(2003, 3003));
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoomPlayerByRoom(2003));

        var players = await repository.GetPlayersByRoomIdAsync(2003);

        // Assert
        Assert.Single(players);
        Assert.Equal(3003, players[0].UserId);

        // Re-cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(2003, 3003));
        Assert.True(exists);
    }

    [Fact]
    public async Task Remove_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        await repository.CreateAsync(2004, 3004);

        // Act
        await repository.RemoveAsync(2004, 3004);

        // Assert
        var dbPlayer = await context.DungeonRoomPlayers.FindAsync(2004L, 3004L);
        Assert.Null(dbPlayer);

        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(2004, 3004));
        Assert.False(exists);
        
        var isMember = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomPlayerByRoom(2004), 3004);
        Assert.False(isMember);
    }

    [Fact]
    public async Task RemoveByRoomId_ShouldRemoveAllFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        await repository.CreateAsync(2005, 3005);
        await repository.CreateAsync(2005, 3006);

        // Act
        await repository.RemoveByRoomIdAsync(2005);

        // Assert
        var dbPlayers = await context.DungeonRoomPlayers.Where(p => p.RoomId == 2005).ToListAsync();
        Assert.Empty(dbPlayers);

        var exists1 = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(2005, 3005));
        var exists2 = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoomPlayer(2005, 3006));
        Assert.False(exists1);
        Assert.False(exists2);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);
        var player = await repository.CreateAsync(2006, 3007);

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.DungeonRoomPlayer(2006, 3007));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
    }
}
