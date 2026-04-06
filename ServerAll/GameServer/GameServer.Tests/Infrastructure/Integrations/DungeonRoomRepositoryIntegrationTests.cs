using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class DungeonRoomRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Create_ShouldSaveToDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        long hostId = 1001;
        string roomName = "Test Room";

        // Act
        var room = await repository.CreateAsync(hostId, roomName);

        // Assert
        Assert.NotNull(room);
        Assert.True(room.RoomId > 0);

        // Check DB
        var dbRoom = await context.DungeonRooms.FindAsync(room.RoomId);
        Assert.NotNull(dbRoom);
        Assert.Equal(roomName, dbRoom.RoomName);

        // Check Redis
        var redisKey = RedisKeys.DungeonRoom(room.RoomId);
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(redisKey);
        Assert.NotEmpty(entries);
        Assert.Equal(roomName, entries.First(e => e.Name == "RoomName").Value.ToString());
        
        var isActive = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
        Assert.True(isActive);
    }

    [Fact]
    public async Task Read_HIT_ShouldReturnFromCacheWithoutDbQuery()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(1002, "Hit Room");

        // Act
        // DB에서 직접 삭제하여 캐시 히트를 증명 (DB에 없는데 반환되면 캐시에서 가져온 것)
        context.DungeonRooms.Remove(room!);
        await context.SaveChangesAsync();

        var cachedRoom = await repository.GetByIdAsync(room!.RoomId);

        // Assert
        Assert.NotNull(cachedRoom);
        Assert.Equal("Hit Room", cachedRoom.RoomName);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(1003, "Miss Room");

        // Act
        // Redis 캐시 강제 삭제
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoom(room!.RoomId));

        var dbRoom = await repository.GetByIdAsync(room.RoomId);

        // Assert
        Assert.NotNull(dbRoom);
        Assert.Equal("Miss Room", dbRoom.RoomName);

        // Re-cache 확인
        var redisKey = RedisKeys.DungeonRoom(room.RoomId);
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(redisKey);
        Assert.True(exists);
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndInvalidateCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(1004, "Original Room");

        // Act
        room!.UpdateRoomSettings(1004, 1, "Updated Room");
        await repository.UpdateAsync(room);

        // Assert
        // DB 확인
        var dbRoom = await context.DungeonRooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == room.RoomId);
        Assert.Equal("Updated Room", dbRoom!.RoomName);

        // Cache 무효화 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoom(room.RoomId));
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(1005, "Delete Room");

        // Act
        await repository.DeleteAsync(room!.RoomId);

        // Assert
        // DB 확인
        var dbRoom = await context.DungeonRooms.FindAsync(room.RoomId);
        Assert.Null(dbRoom);

        // Cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoom(room.RoomId));
        Assert.False(exists);
        
        var isActive = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
        Assert.False(isActive);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(1006, "TTL Room");

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.DungeonRoom(room!.RoomId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
        Assert.True(ttl.Value.TotalMinutes <= 30);
    }
}
